import { Component, booleanAttribute, computed, input } from '@angular/core';
import { endsSoon, formatPrice, validUntil } from '../core/format';

/**
 * The offer badge, drawn as a shelf-edge price label: store colour along the left edge, the price large,
 * store and last day small. The one decorative element in the app.
 */
@Component({
  selector: 'app-price-tag',
  template: `
    <span class="tag" [class.soon]="soon()" [class.compact]="compact()" [style.--store]="color()">
      <span class="price">{{ price() }}</span>
      <span class="meta">{{ store() }}</span>
      <!-- An app or member price says so: "Lidl Plus", "Netto+", "medlemspris". -->
      @if (app()) { <span class="meta app">{{ app() }}</span> }
      <!-- The compact tag only mentions the last day when it is close. -->
      @if (until() && (!compact() || soon())) {
        <span class="meta until">{{ until() }}</span>
      }
    </span>
  `,
  styles: `
    :host { display: inline-flex; }
    .tag {
      position: relative;
      display: grid;
      grid-template-columns: auto;
      justify-items: end;
      min-width: 56px;
      padding: 2px 7px 3px 10px;
      background: var(--tag);
      color: var(--tag-ink);
      border-radius: 3px 8px 8px 3px;
      box-shadow: inset 4px 0 0 var(--store, var(--primary));
      line-height: 1.1;
    }
    .price { font-size: 0.9375rem; font-weight: 800; font-variant-numeric: tabular-nums; }
    .meta { font-size: 0.6875rem; font-weight: 600; white-space: nowrap; opacity: 0.85; }
    .soon .until { color: #8a1c14; opacity: 1; }
    .app { font-weight: 800; opacity: 1; }
    /* One line under the item name, for the two-column list. */
    .tag.compact {
      display: inline-flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 0 5px;
      min-width: 0;
      padding: 0 5px 0 7px;
      border-radius: 2px 6px 6px 2px;
      box-shadow: inset 3px 0 0 var(--store, var(--primary));
    }
    .compact .price { font-size: var(--step--1); }
    .compact .meta { font-size: 0.625rem; }
  `,
})
export class PriceTag {
  readonly amount = input.required<number>();
  readonly store = input.required<string>();
  readonly color = input<string>();
  readonly lastValidDay = input<string | null>(null);
  /** Set when amount is an app or member price. */
  readonly app = input<string | null | undefined>(null);
  readonly compact = input(false, { transform: booleanAttribute });

  protected readonly price = computed(() => formatPrice(this.amount()));
  protected readonly until = computed(() => (this.lastValidDay() ? validUntil(this.lastValidDay()!) : null));
  protected readonly soon = computed(() => !!this.lastValidDay() && endsSoon(this.lastValidDay()!));
}
