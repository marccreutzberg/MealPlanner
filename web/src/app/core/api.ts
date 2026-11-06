import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  BoughtPage, CurrentUser, ItemUpdate, ListItem, MealPlan, MealPlanToList, NewItem, OfferSearch, Period, ShoppingList, StapleSuggestion, Suggestion, Trip, TripPage,
} from './models';

/** Typed access to the MealPlanner API. Same origin, so the auth cookie goes along by itself. */
@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);

  me() {
    return firstValueFrom(this.http.get<CurrentUser>('/api/auth/me'));
  }

  login(email: string, password: string) {
    return firstValueFrom(this.http.post<void>('/api/auth/login', { email, password, rememberMe: true }));
  }

  logout() {
    return firstValueFrom(this.http.post<void>('/api/auth/logout', {}));
  }

  list() {
    return firstValueFrom(this.http.get<ShoppingList>('/api/shopping-list'));
  }

  /** Bought items, most recently bought first. */
  bought(skip: number, take: number) {
    return firstValueFrom(this.http.get<BoughtPage>('/api/shopping-list/bought', { params: { skip, take } }));
  }

  addItems(items: NewItem[]) {
    return firstValueFrom(this.http.post<{ items: ListItem[] }>('/api/shopping-list/items', { items }));
  }

  updateItem(id: number, update: ItemUpdate) {
    return firstValueFrom(this.http.patch<ListItem>(`/api/shopping-list/items/${id}`, update));
  }

  setBought(id: number, bought: boolean, storeId?: number) {
    return firstValueFrom(this.http.post<boolean>(`/api/shopping-list/items/${id}/bought`, { bought, storeId }));
  }

  deleteItem(id: number) {
    return firstValueFrom(this.http.delete<void>(`/api/shopping-list/items/${id}`));
  }

  clearBought() {
    return firstValueFrom(this.http.post<{ deleted: number }>('/api/shopping-list/clear-bought', {}));
  }

  suggestions(q: string) {
    return firstValueFrom(this.http.get<Suggestion[]>('/api/shopping-list/suggestions', { params: { q, limit: 6 } }));
  }

  /** The trip in progress, or null (204). */
  activeTrip() {
    return firstValueFrom(this.http.get<Trip | null>('/api/trips/active'));
  }

  /** Finishes the trip in progress, if any, and makes a new one. No stores = the planner chooses. */
  createTrip(storeIds?: number[]) {
    return firstValueFrom(this.http.post<Trip>('/api/trips', { storeIds: storeIds ?? null }));
  }

  setTripStores(tripId: number, storeIds: number[]) {
    return firstValueFrom(this.http.put<Trip>(`/api/trips/${tripId}/stores`, { storeIds }));
  }

  moveTripItem(tripId: number, tripItemId: number, storeId: number) {
    return firstValueFrom(this.http.post<Trip>(`/api/trips/${tripId}/items/${tripItemId}/store`, { storeId }));
  }

  addNewItemsToTrip(tripId: number) {
    return firstValueFrom(this.http.post<Trip>(`/api/trips/${tripId}/new-items`, {}));
  }

  finishTrip(tripId: number) {
    return firstValueFrom(this.http.post<{ remaining: number }>(`/api/trips/${tripId}/finish`, {}));
  }

  /** Finished trips, newest first. */
  trips(skip: number, take: number) {
    return firstValueFrom(this.http.get<TripPage>('/api/trips', { params: { skip, take } }));
  }

  tripById(tripId: number) {
    return firstValueFrom(this.http.get<Trip>(`/api/trips/${tripId}`));
  }

  tripToList(tripId: number, tripItemIds: number[]) {
    return firstValueFrom(this.http.post<{ count: number }>(`/api/trips/${tripId}/to-list`, { tripItemIds }));
  }

  stapleSuggestions() {
    return firstValueFrom(this.http.get<StapleSuggestion[]>('/api/staples/suggestions'));
  }

  snoozeStaple(name: string) {
    return firstValueFrom(this.http.post<string>('/api/staples/snooze', { name }));
  }

  pin(name: string, defaultQuantity: string | null) {
    return firstValueFrom(this.http.put<void>('/api/staples/pinned', { name, defaultQuantity }));
  }

  unpin(name: string) {
    return firstValueFrom(this.http.delete<void>('/api/staples/pinned', { params: { name } }));
  }

  mealPlan(week: string) {
    return firstValueFrom(this.http.get<MealPlan>(`/api/meal-plans/${week}`));
  }

  mealPlanToList(week: string) {
    return firstValueFrom(this.http.post<MealPlanToList>(`/api/meal-plans/${week}/to-shopping-list`, { force: false }));
  }

  searchOffers(text: string, store: string | null, period: Period, limit = 60) {
    let params = new HttpParams().set('period', period).set('limit', limit);
    if (text.trim()) params = params.set('text', text.trim());
    if (store) params = params.set('store', store);
    return firstValueFrom(this.http.get<OfferSearch>('/api/offers', { params }));
  }
}
