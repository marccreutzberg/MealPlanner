import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { formatPrice, formatRounded, validUntil } from '../../core/format';
import { ListStore } from '../../core/list-store';
import { Trip, TripItem, TripVisit } from '../../core/models';
import { TranslatePipe, translate } from '../../i18n/t.pipe';

interface TagGroup {
  tag: string | null;
  items: TripItem[];
}

/**
 * The shopping trip as a plain checklist per store, in driving order (spec 12.6).
 * Ticking an item marks it bought on the list and remembers the store.
 */
@Component({
  selector: 'app-trip-page',
  imports: [RouterLink, TranslatePipe],
  host: { class: 'page' },
  template: `
    <header class="app-bar">
      <h1>{{ 'trip.title' | t }}</h1>
      @if (trip(); as t) {
        @if (t.estimatedSaving > 0) {
          <p class="saving">{{ 'trip.saving' | t: { amount: rounded(t.estimatedSaving) } }}
            <span>{{ 'trip.savingNote' | t }}</span></p>
        }
        <div class="chips" role="tablist">
          <button role="tab" type="button" [attr.aria-selected]="selected() === null" (click)="selected.set(null)">
            {{ 'trip.all' | t }}
          </button>
          @for (v of withItems(); track v.storeId) {
            <button role="tab" type="button" [attr.aria-selected]="selected() === v.storeId" [style.--store]="v.color"
                    (click)="selected.set(v.storeId)">{{ v.store }}</button>
          }
        </div>
      }
    </header>

    <main>
      @if (loading()) {
        <p class="note" role="status">{{ 'trip.loading' | t }}</p>
      } @else if (trip(); as t) {
        @for (visit of shown(); track visit.storeId) {
          <section [style.--store]="visit.color">
            <h2>
              <span>{{ visit.store }}</span>
              @if (visit.subtotal > 0) { <span class="subtotal price">{{ 'trip.subtotal' | t: { amount: rounded(visit.subtotal) } }}</span> }
            </h2>
            @for (group of groups(visit); track group.tag) {
              @if (group.tag) { <h3>{{ group.tag }}</h3> }
              <ul>
                @for (item of group.items; track item.itemId) {
                  <li>
                    <label [class.bought]="item.isBought">
                      <input type="checkbox" [checked]="item.isBought" (change)="toggle(visit, item)" />
                      <span class="text">
                        <span>{{ label(item) }}</span>
                        @if (item.unitPrice !== null) {
                          <span class="price">{{ priceText(item) }}</span>
                        }
                        @if (item.lastValidDay) { <span class="until">{{ until(item.lastValidDay) }}</span> }
                      </span>
                    </label>
                  </li>
                }
              </ul>
            } @empty {
              <p class="note">{{ 'trip.noItems' | t }}</p>
            }
          </section>
        } @empty {
          <p class="note">{{ 'trip.empty' | t }}</p>
        }

        <div class="actions">
          <a class="button quiet" routerLink="/">{{ 'trip.back' | t }}</a>
          @if (withItems().length) {
            <button class="button" type="button" (click)="share(t)">{{ 'trip.share' | t }}</button>
          }
        </div>
        <p class="note" aria-live="polite">{{ shareStatus() }}</p>
      }
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    </main>
  `,
  styleUrl: './trip-page.scss',
})
export class TripPage implements OnInit {
  private readonly api = inject(Api);
  private readonly list = inject(ListStore);

  protected readonly trip = signal<Trip | null>(null);
  protected readonly loading = signal(true);
  protected readonly selected = signal<number | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly shareStatus = signal('');

  protected readonly withItems = computed(() => this.trip()?.visits.filter(v => v.items.length > 0) ?? []);
  protected readonly shown = computed(() =>
    this.selected() === null ? this.withItems() : this.withItems().filter(v => v.storeId === this.selected()));

  async ngOnInit(): Promise<void> {
    try {
      this.trip.set(await this.api.trip());
    } catch {
      this.error.set(translate('errors.generic'));
    } finally {
      this.loading.set(false);
    }
  }

  protected groups(visit: TripVisit): TagGroup[] {
    const groups: TagGroup[] = [];
    for (const item of visit.items) {
      let group = groups.find(g => g.tag === item.tag);
      if (!group) groups.push((group = { tag: item.tag, items: [] }));
      group.items.push(item);
    }
    // Items without a group first, like the text version.
    return groups.sort((a, b) => (a.tag === null ? -1 : b.tag === null ? 1 : 0));
  }

  protected label(item: TripItem): string {
    const name = item.variant ? `${item.name} (${item.variant})` : item.name;
    return item.quantity ? `${name}, ${item.quantity}` : name;
  }

  protected priceText(item: TripItem): string {
    const unit = formatPrice(item.unitPrice!);
    return item.count > 1 ? `${unit} × ${item.count} = ${formatPrice(item.linePrice!)}` : unit;
  }

  protected until(day: string): string {
    return validUntil(day);
  }

  protected rounded(amount: number): string {
    return formatRounded(amount);
  }

  protected async toggle(visit: TripVisit, item: TripItem): Promise<void> {
    const bought = !item.isBought;
    item.isBought = bought;
    this.trip.set({ ...this.trip()! });
    try {
      await this.api.setBought(item.itemId, bought, visit.storeId);
      void this.list.reload();
    } catch {
      item.isBought = !bought;
      this.trip.set({ ...this.trip()! });
      this.error.set(translate('errors.generic'));
    }
  }

  /** Shares the same text as build_shopping_list returns, so it looks the same in Notes or a message. */
  protected async share(trip: Trip): Promise<void> {
    try {
      if (navigator.share) {
        await navigator.share({ text: trip.text });
      } else {
        await navigator.clipboard.writeText(trip.text);
        this.shareStatus.set(translate('trip.copied'));
      }
    } catch {
      // The user closed the share sheet; nothing to do.
    }
  }
}
