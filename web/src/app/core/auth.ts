import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Api } from './api';
import { CurrentUser } from './models';

@Injectable({ providedIn: 'root' })
export class Auth {
  private readonly api = inject(Api);
  readonly user = signal<CurrentUser | null>(null);

  /** Returns the signed-in user, or null when the cookie is missing or expired. */
  async ensureUser(): Promise<CurrentUser | null> {
    if (this.user()) return this.user();
    try {
      this.user.set(await this.api.me());
    } catch (error) {
      if (!(error instanceof HttpErrorResponse && error.status === 401)) throw error;
      this.user.set(null);
    }
    return this.user();
  }

  async login(email: string, password: string): Promise<void> {
    await this.api.login(email, password);
    this.user.set(await this.api.me());
  }
}

export const authGuard: CanActivateFn = async (_route, state) => {
  const user = await inject(Auth).ensureUser();
  return user ? true : inject(Router).createUrlTree(['/login'], { queryParams: { next: state.url } });
};
