import { Component, computed, effect, input, output, signal } from '@angular/core';
import { Trip } from '../../core/models';
import { TranslatePipe } from '../../i18n/t.pipe';
import { Sheet } from '../../shared/sheet';

interface Stop {
  key: string;
  label: string;
  color: string;
  home: boolean;
  storeIds: number[];
}

/** Which stores are on the trip. Stores next to each other (Lidl / Coop 365) are one stop; the home store is locked. */
@Component({
  selector: 'app-stores-sheet',
  imports: [Sheet, TranslatePipe],
  template: `
    <app-sheet [heading]="'trip.storesTitle' | t" [open]="!!trip()" (closed)="closed.emit()">
      <p class="hint">{{ 'trip.storesHint' | t }}</p>
      <ul>
        @for (stop of stops(); track stop.key) {
          <li>
            <label>
              <input type="checkbox" [checked]="stop.home || chosen().has(stop.key)" [disabled]="stop.home"
                     (change)="toggle(stop.key)" />
              <span class="dot" aria-hidden="true" [style.background]="stop.color"></span>
              {{ stop.label }}
            </label>
          </li>
        }
      </ul>
      <div class="actions">
        <button class="button quiet" type="button" (click)="closed.emit()">{{ 'trip.cancel' | t }}</button>
        <button class="button" type="button" (click)="save()">{{ 'trip.save' | t }}</button>
      </div>
    </app-sheet>
  `,
  styles: `
    .hint { margin: 0 0 8px; color: var(--ink-soft); font-size: var(--step--1); }
    ul { list-style: none; margin: 0 0 12px; padding: 0; }
    label { display: flex; align-items: center; gap: 10px; min-height: var(--tap); border-top: 1px solid var(--line); }
    li:first-child label { border-top: 0; }
    input { width: 18px; height: 18px; margin: 0; accent-color: var(--primary); }
    .dot { width: 10px; height: 10px; border-radius: 50%; box-shadow: 0 0 0 1px color-mix(in srgb, var(--ink) 30%, transparent); }
    .actions { display: flex; justify-content: flex-end; gap: 8px; }
  `,
})
export class StoresSheet {
  readonly trip = input<Trip | null>(null);
  readonly saved = output<number[]>();
  readonly closed = output<void>();

  protected readonly stops = computed<Stop[]>(() => {
    const stops: Stop[] = [];
    for (const store of this.trip()?.stores ?? []) {
      const key = store.stopGroup ?? `store-${store.storeId}`;
      let stop = stops.find(s => s.key === key);
      if (!stop) stops.push((stop = { key, label: store.name, color: store.color, home: false, storeIds: [] }));
      else stop.label += ` / ${store.name}`;
      stop.home ||= store.isHomeStore;
      stop.storeIds.push(store.storeId);
    }
    return stops.sort((a, b) => Number(b.home) - Number(a.home));
  });

  protected readonly chosen = signal(new Set<string>());

  constructor() {
    // Start from the stores on the trip each time the sheet opens.
    effect(() => {
      const trip = this.trip();
      if (!trip) return;
      const onTrip = new Set(trip.stores.filter(s => s.onTrip).map(s => s.storeId));
      this.chosen.set(new Set(this.stops().filter(s => s.storeIds.some(id => onTrip.has(id))).map(s => s.key)));
    });
  }

  protected toggle(key: string): void {
    const next = new Set(this.chosen());
    if (!next.delete(key)) next.add(key);
    this.chosen.set(next);
  }

  protected save(): void {
    this.saved.emit(this.stops().filter(s => s.home || this.chosen().has(s.key)).flatMap(s => s.storeIds));
  }
}
