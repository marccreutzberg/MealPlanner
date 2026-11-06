import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { TranslatePipe } from './i18n/t.pipe';
import { AddSheet } from './pages/list/add-sheet';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe, AddSheet],
  template: `
    <router-outlet />
    @if (showNav()) {
      <nav [attr.aria-label]="'nav.label' | t">
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">{{ 'nav.list' | t }}</a>
        <a routerLink="/tilbud" routerLinkActive="active">{{ 'nav.offers' | t }}</a>
        <!-- Adding an item sits in the middle of the bar so it never covers a row, and works from every page. -->
        <button class="add" type="button" (click)="adding.set(true)" [attr.aria-label]="'list.add' | t">
          <span aria-hidden="true"></span>
        </button>
        <a routerLink="/madplan" routerLinkActive="active">{{ 'nav.meals' | t }}</a>
        <a routerLink="/tur" routerLinkActive="active">{{ 'nav.trip' | t }}</a>
      </nav>
      <app-add-sheet [open]="adding()" (closed)="adding.set(false)" />
    }
  `,
  styles: `
    nav {
      position: fixed;
      inset: auto 0 0;
      display: grid;
      grid-template-columns: 1fr 1fr 58px 1fr 1fr;
      align-items: center;
      background: var(--surface);
      border-top: 1px solid var(--line);
      padding-bottom: env(safe-area-inset-bottom);
      z-index: 10;
    }
    a {
      display: grid;
      place-items: center;
      min-height: var(--nav-h);
      color: var(--ink-soft);
      text-decoration: none;
      font-size: var(--step--1);
      font-weight: 600;
    }
    a.active { color: var(--primary); box-shadow: inset 0 3px 0 var(--primary); }
    .add {
      justify-self: center;
      display: grid;
      place-items: center;
      width: 42px;
      height: 42px;
      padding: 0;
      border: 0;
      border-radius: 50%;
      background: var(--accent);
      box-shadow: 0 4px 12px color-mix(in srgb, var(--accent) 40%, transparent);
    }
    /* A drawn plus, so it sits optically centred whatever the font does. */
    .add span {
      position: relative;
      width: 18px;
      height: 18px;
    }
    .add span::before,
    .add span::after {
      content: '';
      position: absolute;
      inset: 8px 0;
      border-radius: 1px;
      background: var(--accent-ink);
    }
    .add span::after { inset: 0 8px; }
  `,
})
export class App {
  private readonly router = inject(Router);
  private readonly url = toSignal(
    this.router.events.pipe(filter(e => e instanceof NavigationEnd), map(e => e.urlAfterRedirects)),
    { initialValue: this.router.url },
  );
  protected readonly showNav = computed(() => !this.url().startsWith('/login'));
  protected readonly adding = signal(false);
}
