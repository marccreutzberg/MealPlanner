import { Component, computed, input } from '@angular/core';
import { endsSoon, formatPrice, validUntil } from '../core/format';

/**
 * The offer badge, drawn as a shelf-edge price label: store colour along the left edge, the price large,
 * store and last day small. The one decorative element in the app.
 */
@Component({
  selector: 'app-price-tag',
  template: `
    <span class="tag" [class.soon]="soon()" [style.--store]="color()">
      <span class="price">{{ price() }}</span>
      <span class="meta">{{ store() }}</span>
      @if (until()) {
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
      min-width: 64px;
      padding: 4px 10px 5px 14px;
      background: var(--tag);
      color: var(--tag-ink);
      border-radius: 3px 8px 8px 3px;
      box-shadow: inset 5px 0 0 var(--store, var(--primary));
      line-height: 1.1;
    }
    .price { font-size: 1.15rem; font-weight: 800; font-variant-numeric: tabular-nums; }
    .meta { font-size: 0.72rem; font-weight: 600; white-space: nowrap; opacity: 0.85; }
    .soon .until { color: #8a1c14; opacity: 1; }
  `,
})
export class PriceTag {
  readonly amount = input.required<number>();
  readonly store = input.required<string>();
  readonly color = input<string>();
  readonly lastValidDay = input<string | null>(null);

  protected readonly price = computed(() => formatPrice(this.amount()));
  protected readonly until = computed(() => (this.lastValidDay() ? validUntil(this.lastValidDay()!) : null));
  protected readonly soon = computed(() => !!this.lastValidDay() && endsSoon(this.lastValidDay()!));
}
