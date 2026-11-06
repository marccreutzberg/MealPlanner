import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Api } from './api';
import { ItemUpdate, ListItem, NewItem, ShoppingList } from './models';
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
  readonly error = signal(false);
  readonly stores = computed(() => this.list()?.stores ?? []);
  readonly allItems = computed(() => this.list()?.categories.flatMap(c => c.items) ?? []);
  readonly boughtCount = computed(() => this.allItems().filter(i => i.isBought).length);

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
    this.loading ??= this.api.list()
      .then(list => {
        this.list.set(list);
        this.error.set(false);
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

  async toggleBought(item: ListItem, storeId?: number): Promise<void> {
    const bought = !item.isBought;
    this.patchItem(item.id, { isBought: bought });
    try {
      await this.api.setBought(item.id, bought, storeId);
    } catch {
      this.patchItem(item.id, { isBought: !bought });
      this.error.set(true);
    }
  }

  async remove(id: number): Promise<void> {
    await this.api.deleteItem(id);
    await this.reload();
  }

  async clearBought(): Promise<void> {
    await this.api.clearBought();
    await this.reload();
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
