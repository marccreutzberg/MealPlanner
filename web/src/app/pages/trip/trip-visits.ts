import { Component, input, output } from '@angular/core';
import { formatPrice, formatRounded, inkOn, validUntil } from '../../core/format';
import { TripItem, TripVisit } from '../../core/models';
import { TranslatePipe } from '../../i18n/t.pipe';

interface TagGroup {
  tag: string | null;
  items: TripItem[];
}

/**
 * A trip's stores in two columns, each under a band in the store's colour, like the list's categories.
 * Active: tap the name to move the item, the circle to tick it. Archive: tap to choose items to put back on the list.
 */
@Component({
  selector: 'app-trip-visits',
  imports: [TranslatePipe],
  template: `
    <div class="columns">
      @for (visit of visits(); track visit.storeId) {
        <section [style.--band]="visit.color" [style.--band-ink]="inkOn(visit.color)">
          <h2>
            <span>{{ visit.store }}</span>
            @if (visit.subtotal > 0) { <span class="subtotal">{{ 'trip.subtotal' | t: { amount: rounded(visit.subtotal) } }}</span> }
          </h2>
          @for (group of groups(visit); track group.tag) {
            @if (group.tag) { <h3>{{ group.tag }}</h3> }
            <ul>
              @for (item of group.items; track item.id) {
                <li [class.bought]="mode() === 'active' && item.isBought">
                  <button class="text" type="button" [disabled]="mode() === 'active' && !item.itemId" (click)="press(item)">
                    <span class="name">{{ item.name }}@if (item.variant) {<span class="variant"> {{ item.variant }}</span>}</span>
                    <span class="details">
                      @if (item.quantity) { <span>{{ item.quantity }}</span> }
                      @if (item.unitPrice !== null) { <span class="price">{{ priceText(item) }}</span> }
                      @if (mode() === 'active' && item.lastValidDay && !item.isBought) { <span>{{ until(item.lastValidDay) }}</span> }
                      @if (mode() === 'archive' && !item.isBought) { <span class="not-bought">{{ 'trip.notBought' | t }}</span> }
                    </span>
                  </button>
                  @if (mode() === 'active') {
                    <button class="check" type="button" [disabled]="!item.itemId" (click)="toggled.emit({ item, visit })"
                            [attr.aria-pressed]="item.isBought"
                            [attr.aria-label]="(item.isBought ? 'list.uncheck' : 'list.check') | t: { name: item.name }">
                      <span aria-hidden="true"></span>
                    </button>
                  } @else {
                    <button class="pick" type="button" (click)="picked.emit(item)" [attr.aria-pressed]="selected().has(item.id)"
                            [attr.aria-label]="'trip.select' | t: { name: item.name }">
                      <span aria-hidden="true"></span>
                    </button>
                  }
                </li>
              }
            </ul>
          } @empty {
            <p class="note">{{ 'trip.noItems' | t }}</p>
          }
        </section>
      }
    </div>
  `,
  styleUrl: './trip-visits.scss',
})
export class TripVisits {
  readonly visits = input.required<TripVisit[]>();
  readonly mode = input<'active' | 'archive'>('active');
  readonly selected = input<ReadonlySet<number>>(new Set());
  readonly toggled = output<{ item: TripItem; visit: TripVisit }>();
  readonly opened = output<TripItem>();
  readonly picked = output<TripItem>();

  protected readonly inkOn = inkOn;

  protected press(item: TripItem): void {
    if (this.mode() === 'archive') this.picked.emit(item);
    else this.opened.emit(item);
  }

  /** Items without a group first, like the text version. */
  protected groups(visit: TripVisit): TagGroup[] {
    const groups: TagGroup[] = [];
    for (const item of visit.items) {
      let group = groups.find(g => g.tag === item.tag);
      if (!group) groups.push((group = { tag: item.tag, items: [] }));
      group.items.push(item);
    }
    return groups.sort((a, b) => (a.tag === null ? -1 : b.tag === null ? 1 : 0));
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
}
