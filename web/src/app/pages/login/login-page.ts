import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Auth } from '../../core/auth';
import { TranslatePipe, translate } from '../../i18n/t.pipe';

@Component({
  selector: 'app-login-page',
  imports: [FormsModule, TranslatePipe],
  template: `
    <main>
      <form (ngSubmit)="submit()">
        <h1>{{ 'login.title' | t }}</h1>
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
        <label class="field">
          {{ 'login.email' | t }}
          <input type="email" name="email" autocomplete="username" required [(ngModel)]="email" />
        </label>
        <label class="field">
          {{ 'login.password' | t }}
          <input type="password" name="password" autocomplete="current-password" required [(ngModel)]="password" />
        </label>
        <button class="button" type="submit" [disabled]="busy()">{{ 'login.submit' | t }}</button>
      </form>
    </main>
  `,
  styles: `
    main { min-height: 100dvh; display: grid; place-items: center; padding: var(--gutter); }
    form { width: min(380px, 100%); }
    h1 { font-size: var(--step-2); margin: 0 0 20px; }
    .button { width: 100%; margin-top: 6px; }
    .error { color: var(--danger); margin: 0 0 14px; }
  `,
})
export class LoginPage {
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected email = '';
  protected password = '';
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.login(this.email, this.password);
      await this.router.navigateByUrl(this.route.snapshot.queryParamMap.get('next') ?? '/');
    } catch (e) {
      const status = e instanceof HttpErrorResponse ? e.status : 0;
      this.error.set(translate(status === 429 ? 'login.locked' : status === 401 ? 'login.failed' : 'errors.generic'));
    } finally {
      this.busy.set(false);
    }
  }
}
