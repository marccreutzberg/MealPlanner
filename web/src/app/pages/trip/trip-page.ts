import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { formatRounded, formatTripDate } from '../../core/format';
import { ListStore } from '../../core/list-store';
import { Trip, TripItem, TripVisit } from '../../core/models';
import { Realtime } from '../../core/realtime';
import { TranslatePipe, translate } from '../../i18n/t.pipe';
import { MoveSheet } from './move-sheet';
import { StoresSheet } from './stores-sheet';
import { TripVisits } from './trip-visits';

/**
 * The shopping trip in progress (design 5): stores in driving order, tick items off (the same tick as on the list),
 * choose stores, move an item, take new list items along, finish. Without a trip: "Opret handletur".
 */
@Component({
  selector: 'app-trip-page',
  imports: [RouterLink, TranslatePipe, TripVisits, StoresSheet, MoveSheet],
  host: { class: 'page' },
  template: `
    <header class="app-bar">
      <div class="title-row">
        <h1>{{ 'trip.title' | t }}</h1>
        @if (trip()) {
          <button class="bar-button" type="button" [disabled]="busy()" (click)="choosingStores.set(true)">{{ 'trip.stores' | t }}</button>
        }
      </div>
      @if (trip(); as t) {
        <p class="meta">{{ date(t.createdAt) }}</p>
        @if (t.estimatedSaving > 0) {
          <p class="saving">{{ 'trip.saving' | t: { amount: rounded(t.estimatedSaving) } }} <span>{{ 'trip.savingNote' | t }}</span></p>
        }
        <div class="chips" role="group" [attr.aria-label]="'trip.stores' | t">
          <button type="button" [attr.aria-pressed]="selected() === null" (click)="selected.set(null)">{{ 'trip.all' | t }}</button>
          @for (v of withItems(); track v.storeId) {
            <button type="button" [attr.aria-pressed]="selected() === v.storeId" [style.--store]="v.color"
                    (click)="selected.set(v.storeId)">{{ v.store }}</button>
          }
        </div>
      }
    </header>

    <main>
      @if (loading()) {
        <p class="note" role="status">{{ 'trip.loading' | t }}</p>
      } @else if (trip(); as t) {
        @if (t.newItems.count) {
          <div class="new-items" role="status">
            <span>{{ newItemsText(t) }}</span>
            <button class="button" type="button" [disabled]="busy()" (click)="addNewItems()">{{ 'trip.newItemsAdd' | t }}</button>
          </div>
        }
        @if (shown().length) {
          <app-trip-visits mode="active" [visits]="shown()" (toggled)="toggle($event.item, $event.visit)" (opened)="moving.set($event)" />
        } @else {
          <p class="note">{{ 'trip.noItems' | t }}</p>
        }
        <div class="actions">
          <button class="button quiet" type="button" (click)="share(t)">{{ 'trip.share' | t }}</button>
          <button class="button" type="button" [disabled]="busy()" (click)="finish()">{{ 'trip.finish' | t }}</button>
        </div>
      } @else {
        <div class="none">
          <p>{{ 'trip.none' | t }}</p>
          <button class="button" type="button" [disabled]="busy()" (click)="create()">{{ 'trip.create' | t }}</button>
        </div>
      }
      <p class="note" aria-live="polite">{{ status() }}</p>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      <p class="history"><a routerLink="/tur/arkiv">{{ 'trip.history' | t }}</a></p>
    </main>

    <app-stores-sheet [trip]="choosingStores() ? trip() : null" (saved)="saveStores($event)" (closed)="choosingStores.set(false)" />
    <app-move-sheet [item]="moving()" [trip]="trip()" (moved)="move($event)" (closed)="moving.set(null)" />
  `,
  styleUrl: './trip-page.scss',
})
export class TripPage implements OnInit {
  private readonly api = inject(Api);
  private readonly list = inject(ListStore);
  private readonly realtime = inject(Realtime);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly trip = signal<Trip | null>(null);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly selected = signal<number | null>(null);
  protected readonly choosingStores = signal(false);
  protected readonly moving = signal<TripItem | null>(null);
  protected readonly status = signal('');
  protected readonly error = signal<string | null>(null);

  protected readonly withItems = computed(() => this.trip()?.visits.filter(v => v.items.length > 0) ?? []);
  protected readonly shown = computed(() =>
    this.selected() === null ? this.withItems() : this.withItems().filter(v => v.storeId === this.selected()));

  async ngOnInit(): Promise<void> {
    void this.list.init();
    // The other phone ticked something or changed the trip.
    this.realtime.listChanged.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => void this.load());
    await this.load();
    this.loading.set(false);
  }

  protected date(iso: string): string {
    return formatTripDate(iso);
  }

  protected rounded(amount: number): string {
    return formatRounded(amount);
  }

  protected newItemsText(trip: Trip): string {
    const more = trip.newItems.count - trip.newItems.names.length;
    const names = trip.newItems.names.join(', ') + (more > 0 ? ' ' + translate('trip.newItemsMore', { count: more }) : '');
    return translate('trip.newItems', { names });
  }

  protected create(): Promise<void> {
    return this.run(async () => {
      this.status.set('');
      this.trip.set(await this.api.createTrip());
      this.selected.set(null);
    });
  }

  /** Ticks on the list item, so the list and the other phone follow. */
  protected async toggle(item: TripItem, visit: TripVisit): Promise<void> {
    const bought = !item.isBought;
    this.patch(item.id, bought);
    try {
      await this.api.setBought(item.itemId!, bought, visit.storeId);
      void this.list.reload();
    } catch {
      this.patch(item.id, !bought);
      this.error.set(translate('errors.generic'));
    }
  }

  protected saveStores(storeIds: number[]): Promise<void> {
    this.choosingStores.set(false);
    return this.run(async () => this.trip.set(await this.api.setTripStores(this.trip()!.id, storeIds)));
  }

  protected move(storeId: number): Promise<void> {
    const item = this.moving()!;
    this.moving.set(null);
    return this.run(async () => this.trip.set(await this.api.moveTripItem(this.trip()!.id, item.id, storeId)));
  }

  protected addNewItems(): Promise<void> {
    return this.run(async () => this.trip.set(await this.api.addNewItemsToTrip(this.trip()!.id)));
  }

  protected finish(): Promise<void> {
    return this.run(async () => {
      const result = await this.api.finishTrip(this.trip()!.id);
      this.trip.set(null);
      this.status.set(translate('trip.finished', { count: result.remaining }));
      void this.list.reload();
    });
  }

  /** The same text Claude gets, so it looks the same in Notes or a message. */
  protected async share(trip: Trip): Promise<void> {
    try {
      if (navigator.share) {
        await navigator.share({ text: trip.text });
      } else {
        await navigator.clipboard.writeText(trip.text);
        this.status.set(translate('trip.copied'));
      }
    } catch {
      // The user closed the share sheet; nothing to do.
    }
  }

  private async load(): Promise<void> {
    try {
      this.trip.set(await this.api.activeTrip());
      this.error.set(null);
    } catch {
      this.error.set(translate('errors.generic'));
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await action();
    } catch {
      this.error.set(translate('errors.generic'));
    } finally {
      this.busy.set(false);
    }
  }

  private patch(tripItemId: number, bought: boolean): void {
    const trip = this.trip();
    if (!trip) return;
    this.trip.set({
      ...trip,
      visits: trip.visits.map(v => ({ ...v, items: v.items.map(i => (i.id === tripItemId ? { ...i, isBought: bought } : i)) })),
    });
  }
}
