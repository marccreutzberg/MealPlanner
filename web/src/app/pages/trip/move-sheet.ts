import { Component, computed, input, output } from '@angular/core';
import { formatPrice } from '../../core/format';
import { Trip, TripItem, TripStorePrice } from '../../core/models';
import { TranslatePipe } from '../../i18n/t.pipe';
import { Sheet } from '../../shared/sheet';

/**
 * "Flyt til": puts one item in another store on this trip, e.g. when REMA is sold out. Each store shows the item's
 * best offer there today, so it can be moved where it is on offer.
 */
@Component({
  selector: 'app-move-sheet',
  imports: [Sheet, TranslatePipe],
  template: `
    <app-sheet [heading]="'trip.moveTitle' | t: { name: item()?.name ?? '' }" [open]="!!item()" (closed)="closed.emit()">
      <p class="hint">{{ 'trip.moveHint' | t }}</p>
      <ul>
        @for (store of trip()?.stores ?? []; track store.storeId) {
          <li>
            <button type="button" [disabled]="store.storeId === currentStoreId()" (click)="moved.emit(store.storeId)">
              <span class="dot" aria-hidden="true" [style.background]="store.color"></span>
              <span class="name">{{ store.name }}</span>
              @if (priceIn(store.storeId); as offer) {
                <span class="offer" [class.cheapest]="offer.price === cheapest()">
                  @if (offer.price === cheapest() && hasChoice()) { <span class="best">{{ 'trip.cheapest' | t }}</span> }
                  <span class="price">{{ price(offer.price) }}</span>
                  @if (offer.appLabel) { <span class="app">{{ offer.appLabel }}</span> }
                </span>
              } @else if (item()?.storePrices) {
                <span class="none">{{ 'trip.noOffer' | t }}</span>
              }
              @if (store.storeId === currentStoreId()) { <span class="current">{{ 'trip.current' | t }}</span> }
            </button>
          </li>
        }
      </ul>
    </app-sheet>
  `,
  styles: `
    .hint { margin: 0 0 8px; color: var(--ink-soft); font-size: var(--step--1); }
    ul { list-style: none; margin: 0; padding: 0; }
    button {
      display: flex; align-items: center; gap: 10px; width: 100%; min-height: var(--tap); padding: 0 4px;
      border: 0; border-top: 1px solid var(--line); background: none; text-align: left;
    }
    li:first-child button { border-top: 0; }
    button:disabled { cursor: default; }
    .name { flex: 1; }
    .current, .none { color: var(--ink-soft); font-size: var(--step--1); }
    .offer { display: inline-flex; align-items: baseline; gap: 5px; }
    .price { font-weight: 800; font-variant-numeric: tabular-nums; }
    .app { font-size: var(--step--1); font-weight: 700; }
    .best {
      padding: 0 5px; border-radius: 999px; background: var(--tag); color: var(--tag-ink);
      font-size: 0.6875rem; font-weight: 800;
    }
    .dot { width: 10px; height: 10px; border-radius: 50%; box-shadow: 0 0 0 1px color-mix(in srgb, var(--ink) 30%, transparent); }
  `,
})
export class MoveSheet {
  readonly item = input<TripItem | null>(null);
  readonly trip = input<Trip | null>(null);
  readonly moved = output<number>();
  readonly closed = output<void>();

  protected readonly currentStoreId = computed(() => {
    const item = this.item();
    return this.trip()?.visits.find(v => v.items.some(i => i.id === item?.id))?.storeId ?? null;
  });

  protected readonly cheapest = computed(() => {
    const prices = this.item()?.storePrices ?? [];
    return prices.length ? Math.min(...prices.map(p => p.price)) : null;
  });

  /** "billigst" only means something when more than one store has the item on offer. */
  protected readonly hasChoice = computed(() => (this.item()?.storePrices?.length ?? 0) > 1);

  protected priceIn(storeId: number): TripStorePrice | null {
    return this.item()?.storePrices?.find(p => p.storeId === storeId) ?? null;
  }

  protected price(amount: number): string {
    return formatPrice(amount);
  }
}
