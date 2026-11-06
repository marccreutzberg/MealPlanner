import { Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ListStore } from '../../core/list-store';
import { ItemUpdate, ListItem } from '../../core/models';
import { TranslatePipe, translate } from '../../i18n/t.pipe';
import { Sheet } from '../../shared/sheet';

/** Correct an item: name, amount, brand/variant, trip group, category and where to buy it. */
@Component({
  selector: 'app-edit-sheet',
  imports: [FormsModule, Sheet, TranslatePipe],
  template: `
    <app-sheet [heading]="'edit.title' | t" [open]="!!item()" (closed)="closed.emit()">
      @if (item(); as current) {
        <form (ngSubmit)="save(current)">
          <label class="field">
            {{ 'edit.name' | t }}
            <input name="name" required [(ngModel)]="name" />
          </label>
          <div class="row">
            <label class="field">
              {{ 'edit.quantity' | t }}
              <input name="quantity" [(ngModel)]="quantity" />
            </label>
            <label class="field">
              {{ 'edit.tag' | t }}
              <input name="tag" [placeholder]="'edit.tagPlaceholder' | t" [(ngModel)]="tag" />
            </label>
          </div>
          <label class="field">
            {{ 'edit.variant' | t }}
            <input name="variant" [(ngModel)]="variant" aria-describedby="variant-hint" />
            <span id="variant-hint" class="hint">{{ 'edit.variantHint' | t }}</span>
          </label>
          <div class="row">
            <label class="field">
              {{ 'edit.category' | t }}
              <select name="category" [(ngModel)]="categoryId">
                @for (c of store.list()?.allCategories ?? []; track c.id) {
                  <option [ngValue]="c.id">{{ c.name }}</option>
                }
              </select>
            </label>
            <label class="field">
              {{ 'edit.store' | t }}
              <select name="store" [(ngModel)]="storeId">
                <option [ngValue]="null">{{ 'edit.noStore' | t }}</option>
                @for (s of store.stores(); track s.id) {
                  <option [ngValue]="s.id">{{ s.name }}</option>
                }
              </select>
            </label>
          </div>
          <label class="check">
            <input type="checkbox" name="remember" [(ngModel)]="remember" />
            {{ 'edit.rememberStore' | t }}
          </label>
          @if (error()) {
            <p class="error" role="alert">{{ error() }}</p>
          }
          <div class="actions">
            <button class="button danger" type="button" (click)="remove(current)">{{ 'edit.delete' | t }}</button>
            <span class="spacer"></span>
            <button class="button quiet" type="button" (click)="closed.emit()">{{ 'edit.cancel' | t }}</button>
            <button class="button" type="submit" [disabled]="!name.trim()">{{ 'edit.save' | t }}</button>
          </div>
        </form>
      }
    </app-sheet>
  `,
  styles: `
    .row { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
    .hint { font-size: 0.8rem; }
    .check { display: flex; align-items: center; gap: 10px; min-height: 44px; margin-bottom: 10px; }
    .check input { width: 22px; height: 22px; }
    .actions { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; }
    .spacer { flex: 1; }
    .button.danger { padding: 0 8px; }
    .error { color: var(--danger); }
  `,
})
export class EditSheet {
  protected readonly store = inject(ListStore);
  readonly item = input<ListItem | null>(null);
  readonly closed = output<void>();

  protected name = '';
  protected quantity = '';
  protected variant = '';
  protected tag = '';
  protected categoryId: number | null = null;
  protected storeId: number | null = null;
  protected remember = false;
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const item = this.item();
      if (!item) return;
      this.name = item.name;
      this.quantity = item.quantity ?? '';
      this.variant = item.variant ?? '';
      this.tag = item.tag ?? '';
      this.categoryId = item.categoryId;
      this.storeId = item.flag?.storeId ?? null;
      this.remember = false;
      this.error.set(null);
    });
  }

  protected async save(item: ListItem): Promise<void> {
    const update: ItemUpdate = {
      name: this.name.trim(),
      quantity: this.quantity.trim(),
      variant: this.variant.trim(),
      tag: this.tag.trim(),
      rememberStore: this.remember,
    };
    if (this.categoryId !== null && this.categoryId !== item.categoryId) update.categoryId = this.categoryId;
    if (this.storeId === null) update.clearFlag = item.flag !== null;
    else if (this.storeId !== item.flag?.storeId || item.flag?.source !== 'Manual') update.flaggedStoreId = this.storeId;
    try {
      await this.store.update(item.id, update);
      this.closed.emit();
    } catch {
      this.error.set(translate('errors.generic'));
    }
  }

  protected async remove(item: ListItem): Promise<void> {
    try {
      await this.store.remove(item.id);
      this.closed.emit();
    } catch {
      this.error.set(translate('errors.generic'));
    }
  }
}
