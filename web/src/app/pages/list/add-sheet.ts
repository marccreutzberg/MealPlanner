import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../../core/api';
import { ListStore } from '../../core/list-store';
import { Suggestion } from '../../core/models';
import { TranslatePipe, translate } from '../../i18n/t.pipe';
import { Sheet } from '../../shared/sheet';

/**
 * Quick entry: type and press Enter, or tap a suggestion to put it on the list at once. The sheet stays open for
 * the next item.
 */
@Component({
  selector: 'app-add-sheet',
  imports: [FormsModule, Sheet, TranslatePipe],
  template: `
    <app-sheet [heading]="'add.title' | t" [open]="open()" (closed)="reset(); closed.emit()">
      <form (ngSubmit)="add()">
        <label class="field">
          {{ 'add.name' | t }}
          <input name="name" autocomplete="off" required [placeholder]="'add.namePlaceholder' | t"
                 [ngModel]="name()" (ngModelChange)="onName($event)" />
        </label>
        @if (suggestions().length) {
          <div class="suggestions" role="group" [attr.aria-label]="'add.suggestions' | t">
            @for (s of suggestions(); track s.text) {
              <!-- pointerdown is cancelled so the name field keeps focus and the phone keyboard stays up. -->
              <button type="button" (pointerdown)="$event.preventDefault()" (click)="pick(s)">{{ s.text }}</button>
            }
          </div>
        }
        <div class="row">
          <label class="field">
            {{ 'add.quantity' | t }}
            <input name="quantity" autocomplete="off" [placeholder]="'add.quantityPlaceholder' | t" [(ngModel)]="quantity" />
          </label>
          <label class="field">
            {{ 'add.variant' | t }}
            <input name="variant" autocomplete="off" [placeholder]="'add.variantPlaceholder' | t" [(ngModel)]="variant" />
          </label>
        </div>
        <p class="status" aria-live="polite">{{ status() }}</p>
        <div class="actions">
          <button class="button quiet" type="button" (click)="closed.emit()">{{ 'add.close' | t }}</button>
          <button class="button" type="submit" [disabled]="!name().trim() || busy()">{{ 'add.submit' | t }}</button>
        </div>
      </form>
    </app-sheet>
  `,
  styles: `
    .row { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
    .suggestions { display: flex; flex-wrap: wrap; gap: 6px; margin: -4px 0 12px; }
    .suggestions button {
      min-height: 34px; padding: 0 12px; border: 1px solid var(--line); border-radius: 999px;
      background: var(--paper); font-weight: 600;
    }
    .status { min-height: 1.4em; margin: 0 0 10px; color: var(--ink-soft); font-size: var(--step--1); }
    .actions { display: flex; justify-content: flex-end; gap: 10px; }
  `,
})
export class AddSheet {
  private readonly api = inject(Api);
  private readonly store = inject(ListStore);

  readonly open = input(false);
  readonly closed = output<void>();

  protected readonly name = signal('');
  protected quantity = '';
  protected variant = '';
  protected readonly suggestions = signal<Suggestion[]>([]);
  protected readonly status = signal('');
  protected readonly busy = signal(false);
  private timer: ReturnType<typeof setTimeout> | undefined;

  protected onName(value: string): void {
    this.name.set(value);
    clearTimeout(this.timer);
    if (value.trim().length < 2) {
      this.suggestions.set([]);
      return;
    }
    this.timer = setTimeout(async () => {
      const result = await this.api.suggestions(value).catch(() => []);
      // Ignore answers to text the user has already changed.
      if (this.name() === value) this.suggestions.set(result.filter(s => s.text.toLowerCase() !== value.trim().toLowerCase()));
    }, 150);
  }

  /** A known item: add it straight away, with whatever amount or variant has been typed. */
  protected pick(suggestion: Suggestion): void {
    clearTimeout(this.timer);
    this.name.set(suggestion.text);
    this.suggestions.set([]);
    void this.add();
  }

  protected async add(): Promise<void> {
    const name = this.name().trim();
    // A double tap on a suggestion must not add the item twice.
    if (!name || this.busy()) return;
    this.busy.set(true);
    try {
      await this.store.add([{ name, quantity: this.quantity.trim() || null, variant: this.variant.trim() || null }]);
      this.status.set(translate('add.added', { name }));
      this.reset(false);
    } catch {
      this.status.set(translate('errors.generic'));
    } finally {
      this.busy.set(false);
    }
  }

  protected reset(clearStatus = true): void {
    this.name.set('');
    this.quantity = '';
    this.variant = '';
    this.suggestions.set([]);
    if (clearStatus) this.status.set('');
  }
}
