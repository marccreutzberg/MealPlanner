import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ListStore } from '../../core/list-store';
import { Realtime } from '../../core/realtime';
import { ListItem, StapleSuggestion, Store } from '../../core/models';
import { TranslatePipe, translate } from '../../i18n/t.pipe';
import { PriceTag } from '../../shared/price-tag';
import { AddSheet } from './add-sheet';
import { EditSheet } from './edit-sheet';
import { StapleRow } from './staple-row';

/** The week's shared list: categories in the home store's walking order, tick to buy, tap to correct. */
@Component({
  selector: 'app-list-page',
  imports: [RouterLink, TranslatePipe, PriceTag, AddSheet, EditSheet, StapleRow],
  template: `
    <header>
      <div class="title-row">
        <h1>{{ 'list.title' | t }}</h1>
        <label class="toggle">
          <input type="checkbox" [checked]="hideBought()" (change)="hideBought.set(!hideBought())" />
          {{ 'list.hideBought' | t }}
        </label>
      </div>
      <div class="filters" role="group" [attr.aria-label]="'list.allStores' | t">
        <button type="button" [attr.aria-pressed]="storeFilter() === null" (click)="storeFilter.set(null)">
          {{ 'list.allStores' | t }}
        </button>
        @for (s of store.stores(); track s.id) {
          <button type="button" [attr.aria-pressed]="storeFilter()?.id === s.id" [style.--store]="s.color"
                  (click)="storeFilter.set(s)">{{ s.name }}</button>
        }
      </div>
    </header>

    @if (!realtime.connected()) {
      <p class="banner" role="status">{{ 'list.reconnecting' | t }}</p>
    }

    <main>
      @if (store.staples().length && !storeFilter()) {
        <section class="staples" aria-labelledby="staples-title">
          <h2 id="staples-title">{{ 'staples.title' | t }}</h2>
          @for (staple of store.staples(); track staple.name) {
            <app-staple-row [staple]="staple" [color]="staple.bestOffer ? colorOf(staple.bestOffer.store) : undefined"
                            (added)="addStaple(staple)" (snoozed)="store.snooze(staple.name)" />
          }
          <p class="hint">{{ 'staples.swipeHint' | t }}</p>
        </section>
      }

      @for (category of visible(); track category.id) {
        <section [style.--cat]="category.color">
          <h2>{{ category.name }}</h2>
          <ul>
            @for (item of category.items; track item.id) {
              <li [class.bought]="item.isBought">
                <button class="check" type="button" (click)="store.toggleBought(item)"
                        [attr.aria-pressed]="item.isBought"
                        [attr.aria-label]="(item.isBought ? 'list.uncheck' : 'list.check') | t: { name: item.name }">
                  <span aria-hidden="true"></span>
                </button>
                <button class="text" type="button" (click)="editing.set(item)">
                  <span class="name">{{ item.name }}@if (item.variant) {<span class="variant"> {{ item.variant }}</span>}</span>
                  <span class="details">
                    @if (item.quantity) { <span>{{ item.quantity }}</span> }
                    @if (item.flag) { <span class="flag">{{ 'list.flaggedTo' | t: { store: item.flag.store } }}</span> }
                  </span>
                </button>
                @if (item.bestOffer; as offer) {
                  <app-price-tag [amount]="offer.price" [store]="offer.store" [color]="colorOf(offer.store)"
                                 [lastValidDay]="offer.lastValidDay" [title]="offer.heading" />
                }
              </li>
            }
          </ul>
        </section>
      } @empty {
        @if (store.list()) {
          <p class="empty">
            {{ storeFilter() ? ('list.emptyFiltered' | t: { store: storeFilter()!.name }) : ('list.empty' | t) }}
          </p>
        }
      }

      @if (store.allItems().length) {
        <div class="actions">
          @if (store.boughtCount()) {
            <button class="button quiet" type="button" (click)="clearBought()">
              {{ 'list.clearBought' | t }} ({{ store.boughtCount() }})
            </button>
          }
          <a class="button find" routerLink="/tur">{{ 'list.findOffers' | t }}</a>
        </div>
      }
    </main>

    <button class="fab" type="button" (click)="adding.set(true)" [attr.aria-label]="'list.add' | t">
      <span aria-hidden="true">+</span>
    </button>

    <app-add-sheet [open]="adding()" (closed)="adding.set(false)" />
    <app-edit-sheet [item]="editing()" (closed)="editing.set(null)" />
  `,
  styleUrl: './list-page.scss',
})
export class ListPage implements OnInit {
  protected readonly store = inject(ListStore);
  protected readonly realtime = inject(Realtime);

  protected readonly hideBought = signal(false);
  protected readonly storeFilter = signal<Store | null>(null);
  protected readonly adding = signal(false);
  protected readonly editing = signal<ListItem | null>(null);

  protected readonly visible = computed(() => {
    const filter = this.storeFilter();
    const hide = this.hideBought();
    return (this.store.list()?.categories ?? [])
      .map(c => ({
        ...c,
        items: c.items.filter(i =>
          (!hide || !i.isBought) &&
          (!filter || i.flag?.storeId === filter.id || (!i.flag && i.bestOffer?.store === filter.name))),
      }))
      .filter(c => c.items.length > 0);
  });

  ngOnInit(): void {
    void this.store.init();
  }

  protected colorOf(storeName: string): string | undefined {
    return this.store.stores().find(s => s.name === storeName)?.color;
  }

  protected async addStaple(staple: StapleSuggestion): Promise<void> {
    await this.store.add([{ name: staple.name, quantity: staple.quantity }]);
  }

  protected async clearBought(): Promise<void> {
    if (confirm(translate('list.clearBoughtConfirm'))) await this.store.clearBought();
  }
}
