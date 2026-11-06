import { Component, computed, input, output, signal } from '@angular/core';
import { StapleSuggestion } from '../../core/models';
import { TranslatePipe, translate } from '../../i18n/t.pipe';
import { PriceTag } from '../../shared/price-tag';

const SwipeThreshold = 96;

/**
 * One "Plejer I at købe?" suggestion. Swipe sideways for "har nok" (spec 4); the buttons do the same for anyone
 * who does not swipe.
 */
@Component({
  selector: 'app-staple-row',
  imports: [TranslatePipe, PriceTag],
  host: {
    '[style.transform]': 'transform()',
    '[style.opacity]': 'opacity()',
    '[class.dragging]': 'dragging()',
    '(pointerdown)': 'start($event)',
    '(pointermove)': 'move($event)',
    '(pointerup)': 'end()',
    '(pointercancel)': 'cancel()',
  },
  template: `
    <div class="text">
      <span class="name">{{ staple().name }}@if (staple().quantity) {<span class="quantity">, {{ staple().quantity }}</span>}</span>
      <span class="why">{{ why() }}</span>
      <span class="buttons">
        <button class="add" type="button" (click)="added.emit()" [attr.aria-label]="'staples.addNamed' | t: { name: staple().name }">
          {{ 'staples.add' | t }}
        </button>
        <button class="snooze" type="button" (click)="snoozed.emit()" [attr.aria-label]="'staples.snoozeNamed' | t: { name: staple().name }">
          {{ 'staples.snooze' | t }}
        </button>
      </span>
    </div>
    @if (staple().bestOffer; as offer) {
      <app-price-tag [amount]="offer.price" [store]="offer.store" [color]="color()" [lastValidDay]="offer.lastValidDay" />
    }
  `,
  styles: `
    :host {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 10px var(--gutter);
      border-top: 1px solid var(--line);
      background: var(--surface);
      touch-action: pan-y;
      transition: transform 160ms ease-out, opacity 160ms ease-out;
    }
    :host(.dragging) { transition: none; }
    .text { flex: 1; min-width: 0; display: grid; gap: 2px; }
    .name { font-weight: 600; }
    .quantity { font-weight: 400; color: var(--ink-soft); }
    .why { font-size: var(--step--1); color: var(--ink-soft); }
    .buttons { display: flex; gap: 8px; margin-top: 4px; }
    .buttons button {
      min-height: 36px; padding: 0 14px; border-radius: 999px; font-size: var(--step--1); font-weight: 600;
    }
    .add { border: 0; background: var(--primary); color: var(--primary-ink); }
    .snooze { border: 1px solid var(--line); background: transparent; color: var(--ink-soft); }
  `,
})
export class StapleRow {
  readonly staple = input.required<StapleSuggestion>();
  readonly color = input<string>();
  readonly added = output<void>();
  readonly snoozed = output<void>();

  protected readonly offset = signal(0);
  protected readonly dragging = signal(false);
  private startX: number | null = null;

  protected readonly transform = computed(() => (this.offset() ? `translateX(${this.offset()}px)` : null));
  protected readonly opacity = computed(() => (this.offset() ? 1 - Math.min(Math.abs(this.offset()) / 240, 0.6) : null));

  protected readonly why = computed(() => {
    const s = this.staple();
    return s.reason === 'Pinned' || s.rhythmDays === null
      ? translate('staples.pinned')
      : translate('staples.rhythm', { days: s.rhythmDays, since: s.daysSinceLast });
  });

  protected start(event: PointerEvent): void {
    if ((event.target as HTMLElement).closest('button')) return;
    this.startX = event.clientX;
    this.dragging.set(true);
    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
  }

  protected move(event: PointerEvent): void {
    if (this.startX !== null) this.offset.set(event.clientX - this.startX);
  }

  protected end(): void {
    const swiped = Math.abs(this.offset()) > SwipeThreshold;
    this.cancel();
    if (swiped) this.snoozed.emit();
  }

  protected cancel(): void {
    this.startX = null;
    this.dragging.set(false);
    this.offset.set(0);
  }
}
