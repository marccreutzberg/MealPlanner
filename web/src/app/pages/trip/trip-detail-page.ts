import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { formatTripDate } from '../../core/format';
import { ListStore } from '../../core/list-store';
import { Trip, TripItem } from '../../core/models';
import { TranslatePipe, translate } from '../../i18n/t.pipe';
import { TripVisits } from './trip-visits';

/** A finished trip, read-only. Choose items and put them back on the list. */
@Component({
  selector: 'app-trip-detail-page',
  imports: [RouterLink, TranslatePipe, TripVisits],
  host: { class: 'page' },
  template: `
    <header class="app-bar">
      <h1>{{ trip() ? date(trip()!.finishedAt ?? trip()!.createdAt) : ('trip.history' | t) }}</h1>
      <p class="back"><a routerLink="/tur/arkiv">{{ 'trip.history' | t }}</a></p>
    </header>
    <main>
      @if (trip(); as t) {
        <app-trip-visits mode="archive" [visits]="withItems()" [selected]="selected()" (picked)="pick($event)" />
        <div class="actions">
          <button class="button" type="button" [disabled]="!selected().size || busy()" (click)="toList(t)">
            {{ 'trip.toList' | t: { count: selected().size } }}
          </button>
        </div>
      }
      <p class="note" aria-live="polite">{{ status() }}</p>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    </main>
  `,
  styles: `
    .back { margin: 2px 0 0; font-size: var(--step--1); }
    .back a { color: var(--primary); text-underline-offset: 3px; }
    main { max-width: 820px; margin: 0 auto; }
    .actions { display: flex; justify-content: flex-end; padding: 8px 6px 0; }
    .note { margin: 0; padding: 4px var(--gutter); color: var(--ink-soft); font-size: var(--step--1); }
    .note:empty { display: none; }
    .error { padding: 0 var(--gutter); color: var(--danger); }
  `,
})
export class TripDetailPage implements OnInit {
  private readonly api = inject(Api);
  private readonly list = inject(ListStore);
  private readonly router = inject(Router);

  /** Route parameter (component input binding). */
  readonly id = input.required<string>();

  protected readonly trip = signal<Trip | null>(null);
  protected readonly selected = signal<ReadonlySet<number>>(new Set());
  protected readonly busy = signal(false);
  protected readonly status = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly withItems = computed(() => this.trip()?.visits.filter(v => v.items.length > 0) ?? []);

  async ngOnInit(): Promise<void> {
    try {
      const trip = await this.api.tripById(Number(this.id()));
      // The trip in progress lives on its own page.
      if (!trip.finishedAt) {
        await this.router.navigate(['/tur']);
        return;
      }
      this.trip.set(trip);
    } catch {
      this.error.set(translate('errors.generic'));
    }
  }

  protected date(iso: string): string {
    return formatTripDate(iso);
  }

  protected pick(item: TripItem): void {
    const next = new Set(this.selected());
    if (!next.delete(item.id)) next.add(item.id);
    this.selected.set(next);
  }

  protected async toList(trip: Trip): Promise<void> {
    this.busy.set(true);
    try {
      const result = await this.api.tripToList(trip.id, [...this.selected()]);
      this.status.set(translate('trip.addedToList', { count: result.count }));
      this.selected.set(new Set());
      void this.list.reload();
    } catch {
      this.error.set(translate('errors.generic'));
    } finally {
      this.busy.set(false);
    }
  }
}
