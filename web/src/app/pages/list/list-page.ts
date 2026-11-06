import { NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ListStore } from '../../core/list-store';
import { Realtime } from '../../core/realtime';
import { ListItem, StapleSuggestion, Store } from '../../core/models';
import { TranslatePipe, translate } from '../../i18n/t.pipe';
import { PriceTag } from '../../shared/price-tag';
import { EditSheet } from './edit-sheet';
import { StapleRow } from './staple-row';

/**
 * The week's shared list: what is still to buy, by category in the home store's walking order. Ticked items move
 * to a folded list at the bottom, like the Coop app; tap to correct an item.
 */
@Component({
  selector: 'app-list-page',
  imports: [NgTemplateOutlet, RouterLink, TranslatePipe, PriceTag, EditSheet, StapleRow],
  host: { class: 'page' },
  template: `
    <header class="app-bar">
      <h1>{{ 'list.title' | t }}</h1>
      <div class="chips" role="group" [attr.aria-label]="'list.allStores' | t">
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

      @for (category of toBuy(); track category.id) {
        <section [style.--cat]="category.color" [style.--cat-ink]="inkOn(category.color)">
          <h2>{{ category.name }}</h2>
          <ul>
            @for (item of category.items; track item.id) {
              <ng-container *ngTemplateOutlet="row; context: { $implicit: item }" />
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

      @if (toBuy().length) {
        <div class="actions">
          <a class="button find" routerLink="/tur">{{ 'list.findOffers' | t }}</a>
        </div>
      }

      @if (store.boughtTotal()) {
        <div class="bought-toggle">
          <button type="button" [attr.aria-expanded]="showBought()" aria-controls="bought-list"
                  (click)="toggleBoughtSection()">
            {{ (showBought() ? 'list.hideBought' : 'list.showBought') | t: { count: store.boughtTotal() } }}
            <span class="chevron" aria-hidden="true"></span>
          </button>
        </div>
        @if (showBought()) {
          <section id="bought-list" class="bought-list">
            <ul>
              @for (item of bought(); track item.id) {
                <ng-container *ngTemplateOutlet="row; context: { $implicit: item }" />
              }
            </ul>
            <div class="clear">
              @if (store.hasMoreBought()) {
                <button class="button quiet" type="button" [disabled]="loadingMore()" (click)="moreBought()">
                  {{ 'list.moreBought' | t }}
                </button>
              }
              <button class="button quiet" type="button" (click)="clearBought()">{{ 'list.clearBought' | t }}</button>
            </div>
          </section>
        }
      }
    </main>

    <!-- One row, used both in the list and among the bought items. -->
    <ng-template #row let-item>
      <li [class.bought]="item.isBought">
        <button class="text" type="button" (click)="editing.set(item)">
          <span class="name">{{ item.name }}@if (item.variant) {<span class="variant"> {{ item.variant }}</span>}</span>
          <span class="details">
            @if (item.quantity) { <span>{{ item.quantity }}</span> }
            @if (showFlag(item)) {
              <span class="flag" [style.--store]="colorOf(item.flag.store)">
                <span class="visually-hidden">{{ 'list.flagPrefix' | t }} </span>{{ item.flag.store }}
              </span>
            }
          </span>
          @if (item.bestOffer && !item.isBought) {
            <app-price-tag compact [amount]="item.bestOffer.price" [store]="item.bestOffer.store"
                           [color]="colorOf(item.bestOffer.store)" [lastValidDay]="item.bestOffer.lastValidDay"
                           [title]="item.bestOffer.heading" />
          }
        </button>
        <button class="check" type="button" (click)="store.toggleBought(item)"
                [attr.aria-pressed]="item.isBought"
                [attr.aria-label]="(item.isBought ? 'list.uncheck' : 'list.check') | t: { name: item.name }">
          <span aria-hidden="true"></span>
        </button>
      </li>
    </ng-template>

    <app-edit-sheet [item]="editing()" (closed)="editing.set(null)" />
  `,
  styleUrl: './list-page.scss',
})
export class ListPage implements OnInit {
  protected readonly store = inject(ListStore);
  protected readonly realtime = inject(Realtime);

  protected readonly showBought = signal(false);
  protected readonly storeFilter = signal<Store | null>(null);
  protected readonly editing = signal<ListItem | null>(null);

  /** Still to buy, grouped by category. Ticked items leave this list at once. */
  protected readonly toBuy = computed(() =>
    (this.store.list()?.categories ?? [])
      .map(c => ({ ...c, items: c.items.filter(i => !i.isBought && this.inFilter(i)) }))
      .filter(c => c.items.length > 0));

  /** Already bought, newest first, as far as they have been fetched. */
  protected readonly bought = computed(() => this.store.bought().filter(i => this.inFilter(i)));

  ngOnInit(): void {
    // The page opens with the bought items folded, so the store should not keep fetching them.
    this.store.hideBought();
    void this.store.init();
  }

  private inFilter(item: ListItem): boolean {
    const filter = this.storeFilter();
    return !filter || item.flag?.storeId === filter.id || (!item.flag && item.bestOffer?.store === filter.name);
  }

  /** The flag is only news when the price tag does not already name the same store. */
  protected showFlag(item: ListItem): boolean {
    return !!item.flag && item.flag.store !== item.bestOffer?.store;
  }

  /** Dark or white text on a category band, whichever reads better on that colour. */
  protected inkOn(hex: string): string {
    const channel = (i: number) => {
      const c = parseInt(hex.slice(i, i + 2), 16) / 255;
      return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
    };
    const luminance = 0.2126 * channel(1) + 0.7152 * channel(3) + 0.0722 * channel(5);
    // Contrast against white vs. against #1b2430 (luminance ~0.017).
    return 1.05 / (luminance + 0.05) >= (luminance + 0.05) / 0.067 ? '#ffffff' : '#1b2430';
  }

  protected colorOf(storeName: string): string | undefined {
    return this.store.stores().find(s => s.name === storeName)?.color;
  }

  protected async addStaple(staple: StapleSuggestion): Promise<void> {
    await this.store.add([{ name: staple.name, quantity: staple.quantity }]);
  }

  protected readonly loadingMore = signal(false);

  protected toggleBoughtSection(): void {
    const open = !this.showBought();
    this.showBought.set(open);
    if (open) void this.store.showBought();
    else this.store.hideBought();
  }

  protected async moreBought(): Promise<void> {
    this.loadingMore.set(true);
    try {
      await this.store.moreBought();
    } finally {
      this.loadingMore.set(false);
    }
  }

  protected async clearBought(): Promise<void> {
    if (confirm(translate('list.clearBoughtConfirm'))) await this.store.clearBought();
  }
}
