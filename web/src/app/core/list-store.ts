import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Api } from './api';
import { ItemUpdate, ListItem, NewItem, ShoppingList, StapleSuggestion } from './models';

const BoughtPageSize = 20;
import { Realtime } from './realtime';

/**
 * The shared shopping list as signals. Reloads when the other phone changes something (SignalR) and after our own
 * changes; ticking an item updates the screen at once and rolls back if the server says no.
 */
@Injectable({ providedIn: 'root' })
export class ListStore {
  private readonly api = inject(Api);
  private readonly realtime = inject(Realtime);

  readonly list = signal<ShoppingList | null>(null);
  readonly staples = signal<StapleSuggestion[]>([]);
  readonly error = signal(false);
  readonly stores = computed(() => this.list()?.stores ?? []);
  /** Bought items fetched so far (newest first); empty while the bought section is folded. */
  readonly bought = signal<ListItem[]>([]);
  readonly boughtTotal = computed(() => this.list()?.boughtCount ?? 0);
  readonly hasMoreBought = computed(() => this.bought().length < this.boughtTotal());

  /** How many bought items are on screen, so a reload fetches the same amount. 0 = folded. */
  private boughtShown = 0;

  private loading: Promise<void> | null = null;

  constructor() {
    this.realtime.listChanged.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe(() => void this.reload());
  }

  async init(): Promise<void> {
    void this.realtime.start();
    if (!this.list()) await this.reload();
  }

  reload(): Promise<void> {
    // Several "changed" messages in a row only need one request.
    this.loading ??= Promise.all([this.api.list(), this.api.stapleSuggestions().catch(() => [])])
      .then(async ([list, staples]) => {
        this.list.set(list);
        this.staples.set(staples);
        this.error.set(false);
        if (this.boughtShown) await this.fetchBought(0, this.boughtShown, false);
      })
      .catch(() => this.error.set(true))
      .finally(() => (this.loading = null));
    return this.loading;
  }

  async add(items: NewItem[]): Promise<ListItem[]> {
    const result = await this.api.addItems(items);
    await this.reload();
    return result.items;
  }

  async update(id: number, update: ItemUpdate): Promise<void> {
    await this.api.updateItem(id, update);
    await this.reload();
  }

  /** Unfolds the bought items with the first page. */
  async showBought(): Promise<void> {
    await this.fetchBought(0, BoughtPageSize, false);
  }

  async moreBought(): Promise<void> {
    await this.fetchBought(this.bought().length, BoughtPageSize, true);
  }

  hideBought(): void {
    this.boughtShown = 0;
    this.bought.set([]);
  }

  /**
   * Ticking moves the item to the bought items at once; unticking moves it back. The server's answer
   * (and the reload it triggers) settles the real order.
   */
  async toggleBought(item: ListItem, storeId?: number): Promise<void> {
    const bought = !item.isBought;
    const list = this.list();
    if (bought) {
      this.patchItem(item.id, { isBought: true });
      if (this.boughtShown) this.bought.set([{ ...item, isBought: true, bestOffer: null }, ...this.bought()]);
    } else {
      this.bought.set(this.bought().filter(i => i.id !== item.id));
    }
    if (list) this.list.set({ ...this.list()!, boughtCount: list.boughtCount + (bought ? 1 : -1) });
    try {
      await this.api.setBought(item.id, bought, storeId);
      if (!bought) await this.reload();
    } catch {
      this.error.set(true);
      await this.reload();
    }
  }

  /** "Har nok": hidden at once, then confirmed by the server (which also tells the other phone). */
  async snooze(name: string): Promise<void> {
    this.staples.set(this.staples().filter(s => s.name !== name));
    try {
      await this.api.snoozeStaple(name);
    } catch {
      this.error.set(true);
      await this.reload();
    }
  }

  async setPinned(name: string, pinned: boolean, quantity: string | null): Promise<void> {
    await (pinned ? this.api.pin(name, quantity) : this.api.unpin(name));
    await this.reload();
  }

  async remove(id: number): Promise<void> {
    await this.api.deleteItem(id);
    await this.reload();
  }

  async clearBought(): Promise<void> {
    await this.api.clearBought();
    await this.reload();
  }

  private async fetchBought(skip: number, take: number, append: boolean): Promise<void> {
    try {
      const page = await this.api.bought(skip, take);
      this.bought.set(append ? [...this.bought(), ...page.items] : page.items);
      this.boughtShown = this.bought().length || take;
    } catch {
      this.error.set(true);
    }
  }

  private patchItem(id: number, patch: Partial<ListItem>): void {
    const list = this.list();
    if (!list) return;
    this.list.set({
      ...list,
      categories: list.categories.map(c => ({
        ...c,
        items: c.items.map(i => (i.id === id ? { ...i, ...patch } : i)),
      })),
    });
  }
}
