import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { TranslatePipe } from './i18n/t.pipe';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  template: `
    <router-outlet />
    @if (showNav()) {
      <nav [attr.aria-label]="'nav.label' | t">
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">{{ 'nav.list' | t }}</a>
        <a routerLink="/tilbud" routerLinkActive="active">{{ 'nav.offers' | t }}</a>
        <a routerLink="/madplan" routerLinkActive="active">{{ 'nav.meals' | t }}</a>
        <a routerLink="/tur" routerLinkActive="active">{{ 'nav.trip' | t }}</a>
      </nav>
    }
  `,
  styles: `
    nav {
      position: fixed;
      inset: auto 0 0;
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      background: var(--surface);
      border-top: 1px solid var(--line);
      padding-bottom: env(safe-area-inset-bottom);
      z-index: 10;
    }
    a {
      display: grid;
      place-items: center;
      min-height: 56px;
      color: var(--ink-soft);
      text-decoration: none;
      font-weight: 600;
    }
    a.active { color: var(--primary); box-shadow: inset 0 3px 0 var(--primary); }
  `,
})
export class App {
  private readonly router = inject(Router);
  private readonly url = toSignal(
    this.router.events.pipe(filter(e => e instanceof NavigationEnd), map(e => e.urlAfterRedirects)),
    { initialValue: this.router.url },
  );
  protected readonly showNav = computed(() => !this.url().startsWith('/login'));
}
