import { Component, input, output } from '@angular/core';
import { formatPrice, formatRounded, inkOn, validUntil } from '../../core/format';
import { TripItem, TripVisit } from '../../core/models';
import { TranslatePipe } from '../../i18n/t.pipe';

interface Department {
  name: string | null;
  color: string | null;
  items: TripItem[];
}

/**
 * A trip's stores in two columns, each under a band in the store's colour, like the list's categories.
 * Inside a store the items stand by department in the walking order, so nobody walks back and forth; the tag
 * ("Bagning") is a small label on the item.
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
          @for (group of departments(visit); track group.name) {
            @if (group.name) { <h3 [style.--department]="group.color">{{ group.name }}</h3> }
            <ul>
              @for (item of group.items; track item.id) {
                <li [class.bought]="mode() === 'active' && item.isBought">
                  <button class="text" type="button" [disabled]="mode() === 'active' && !item.itemId" (click)="press(item)">
                    <span class="name">{{ item.name }}@if (item.variant) {<span class="variant"> {{ item.variant }}</span>}</span>
                    <span class="details">
                      @if (item.quantity) { <span>{{ item.quantity }}</span> }
                      @if (item.unitPrice !== null) { <span class="price">{{ priceText(item) }}</span> }
                      @if (item.unitPrice !== null && item.priceLabel) { <span class="app">{{ item.priceLabel }}</span> }
                      @if (item.tag) { <span class="tag">{{ item.tag }}</span> }
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

  /** Departments in walking order; items without one last, like the text version. */
  protected departments(visit: TripVisit): Department[] {
    const groups: (Department & { sort: number })[] = [];
    for (const item of visit.items) {
      let group = groups.find(g => g.name === item.category);
      if (!group) {
        group = { name: item.category, color: item.categoryColor, sort: item.category ? item.categorySort : Number.MAX_SAFE_INTEGER, items: [] };
        groups.push(group);
      }
      group.items.push(item);
    }
    return groups.sort((a, b) => a.sort - b.sort);
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
