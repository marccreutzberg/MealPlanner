import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../../core/api';
import { appLabel, formatPrice, startsOn } from '../../core/format';
import { ListStore } from '../../core/list-store';
import { Offer, OfferSearch, Period } from '../../core/models';
import { TranslatePipe, translate } from '../../i18n/t.pipe';
import { PriceTag } from '../../shared/price-tag';

/** Search this week's offers across the chains and put one on the list, flagged to its store. */
@Component({
  selector: 'app-offers-page',
  imports: [FormsModule, TranslatePipe, PriceTag],
  host: { class: 'page' },
  template: `
    <header class="app-bar">
      <h1>{{ 'offers.title' | t }}</h1>
      <label class="field search">
        <span class="visually-hidden">{{ 'offers.search' | t }}</span>
        <input type="search" [placeholder]="'offers.searchPlaceholder' | t" [ngModel]="text()"
               (ngModelChange)="onText($event)" enterkeyhint="search" />
      </label>
      <div class="segments" role="group" [attr.aria-label]="'offers.period' | t">
        @for (p of periods; track p) {
          <button type="button" [attr.aria-pressed]="period() === p" (click)="setPeriod(p)">{{ 'offers.' + p | t }}</button>
        }
      </div>
      <div class="chips" role="group">
        <button type="button" [attr.aria-pressed]="storeName() === null" (click)="setStore(null)">{{ 'offers.allStores' | t }}</button>
        @for (s of list.stores(); track s.id) {
          <button type="button" [attr.aria-pressed]="storeName() === s.name" [style.--store]="s.color" (click)="setStore(s.name)">
            {{ s.name }}
          </button>
        }
      </div>
    </header>

    <main>
      @if (result(); as r) {
        <p class="count" aria-live="polite">{{ 'offers.count' | t: { shown: r.offers.length, total: r.total } }}</p>
        <ul>
          @for (offer of r.offers; track offer.id) {
            <li>
              <div class="text">
                <span class="heading">{{ offer.heading }}</span>
                @if (offer.description) { <span class="description">{{ offer.description }}</span> }
                <span class="conditions">
                  @if (period() === 'upcoming') { <span>{{ starts(offer) }}</span> }
                  @if (offer.isEverydayPrice) { <span>{{ 'offers.everyday' | t }}</span> }
                  @if (offer.prePrice) { <span>{{ 'offers.prePrice' | t: { price: price(offer.prePrice) } }}</span> }
                  @if (offer.requiresAppOrMembership) { <span class="warn">{{ 'offers.requiresApp' | t }}</span> }
                  @if (offer.appPrice !== null && offer.appPrice < offer.price) {
                    <span>{{ 'offers.withoutApp' | t: { price: price(offer.price) } }}</span>
                  }
                  @if (offer.maxPerCustomer) { <span>{{ 'offers.maxPerCustomer' | t: { count: offer.maxPerCustomer } }}</span> }
                </span>
                <button class="button quiet add" type="button" [disabled]="added().has(offer.id)" (click)="addToList(offer)">
                  {{ (added().has(offer.id) ? 'offers.onList' : 'offers.addToList') | t }}
                </button>
              </div>
              <app-price-tag [amount]="offer.appPrice ?? offer.price" [store]="offer.store" [color]="colorOf(offer.store)"
                             [lastValidDay]="offer.validTo" [app]="offer.appPrice !== null ? appLabel(offer.store) : null" />
            </li>
          } @empty {
            <li class="empty">{{ 'offers.none' | t }}</li>
          }
        </ul>
      }
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    </main>
  `,
  styleUrl: './offers-page.scss',
})
export class OffersPage implements OnInit {
  private readonly api = inject(Api);
  protected readonly list = inject(ListStore);

  protected readonly periods: Period[] = ['today', 'week', 'upcoming'];
  protected readonly text = signal('');
  protected readonly period = signal<Period>('week');
  protected readonly storeName = signal<string | null>(null);
  protected readonly result = signal<OfferSearch | null>(null);
  protected readonly added = signal(new Set<number>());
  protected readonly error = signal<string | null>(null);
  private timer: ReturnType<typeof setTimeout> | undefined;
  private requestNo = 0;

  ngOnInit(): void {
    void this.list.init();
    void this.search();
  }

  protected onText(value: string): void {
    this.text.set(value);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.search(), 250);
  }

  protected setPeriod(period: Period): void {
    this.period.set(period);
    void this.search();
  }

  protected setStore(name: string | null): void {
    this.storeName.set(name);
    void this.search();
  }

  protected colorOf(store: string): string | undefined {
    return this.list.stores().find(s => s.name === store)?.color;
  }

  protected price(amount: number): string {
    return formatPrice(amount);
  }

  protected readonly appLabel = appLabel;

  protected starts(offer: Offer): string {
    return startsOn(offer.validFrom);
  }

  /** Today's offers are flagged to the offer; upcoming ones to the store (the offer is not valid yet). */
  protected async addToList(offer: Offer): Promise<void> {
    const name = offer.ingredients[0] ?? offer.heading;
    try {
      await this.list.add([this.period() === 'upcoming' ? { name, store: offer.store } : { name, offerId: offer.id }]);
      this.added.set(new Set([...this.added(), offer.id]));
    } catch {
      this.error.set(translate('errors.generic'));
    }
  }

  private async search(): Promise<void> {
    const no = ++this.requestNo;
    try {
      const result = await this.api.searchOffers(this.text(), this.storeName(), this.period());
      if (no === this.requestNo) {
        this.result.set(result);
        this.error.set(null);
      }
    } catch {
      if (no === this.requestNo) this.error.set(translate('errors.generic'));
    }
  }
}
