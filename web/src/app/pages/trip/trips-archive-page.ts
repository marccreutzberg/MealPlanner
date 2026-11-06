import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { formatRounded, formatTripDate } from '../../core/format';
import { TripSummary } from '../../core/models';
import { TranslatePipe, translate } from '../../i18n/t.pipe';

const PageSize = 20;

/** Finished trips, newest first, 20 at a time. */
@Component({
  selector: 'app-trips-archive-page',
  imports: [RouterLink, TranslatePipe],
  host: { class: 'page' },
  template: `
    <header class="app-bar">
      <h1>{{ 'trip.history' | t }}</h1>
    </header>
    <main>
      <ul>
        @for (trip of trips(); track trip.id) {
          <li>
            <a [routerLink]="['/tur', trip.id]">
              <span class="date">{{ date(trip.finishedAt ?? trip.createdAt) }}</span>
              <span class="stores">{{ trip.stores.join(', ') }}</span>
              <span class="facts">
                <span>{{ 'trip.summary' | t: { bought: trip.boughtCount, count: trip.itemCount } }}</span>
                @if (trip.total > 0) { <span>{{ 'trip.total' | t: { amount: rounded(trip.total) } }}</span> }
              </span>
            </a>
          </li>
        } @empty {
          @if (loaded()) { <li class="empty">{{ 'trip.historyEmpty' | t }}</li> }
        }
      </ul>
      @if (trips().length < total()) {
        <div class="more">
          <button class="button quiet" type="button" [disabled]="busy()" (click)="more()">{{ 'trip.more' | t }}</button>
        </div>
      }
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    </main>
  `,
  styles: `
    main { max-width: 820px; margin: 0 auto; padding: 6px 6px 0; }
    ul { list-style: none; margin: 0; padding: 0; background: var(--surface); border-radius: var(--radius-s); overflow: hidden; }
    li + li { border-top: 1px solid var(--line); }
    a { display: grid; gap: 1px; padding: 8px 10px; color: inherit; text-decoration: none; line-height: 1.25; }
    .date { font-weight: 600; }
    .stores, .facts { color: var(--ink-soft); font-size: var(--step--1); }
    .facts { display: flex; flex-wrap: wrap; gap: 0 8px; }
    .empty { padding: 32px var(--gutter); color: var(--ink-soft); text-align: center; }
    .more { display: flex; justify-content: center; padding: 8px 0; }
    .error { padding: 0 var(--gutter); color: var(--danger); }
  `,
})
export class TripsArchivePage implements OnInit {
  private readonly api = inject(Api);

  protected readonly trips = signal<TripSummary[]>([]);
  protected readonly total = signal(0);
  protected readonly loaded = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    await this.more();
    this.loaded.set(true);
  }

  protected date(iso: string): string {
    return formatTripDate(iso);
  }

  protected rounded(amount: number): string {
    return formatRounded(amount);
  }

  protected async more(): Promise<void> {
    this.busy.set(true);
    try {
      const page = await this.api.trips(this.trips().length, PageSize);
      this.trips.set([...this.trips(), ...page.items]);
      this.total.set(page.total);
    } catch {
      this.error.set(translate('errors.generic'));
    } finally {
      this.busy.set(false);
    }
  }
}
