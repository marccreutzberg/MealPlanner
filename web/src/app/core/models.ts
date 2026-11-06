// Mirrors the API's DTOs (camelCase JSON). Dates are "yyyy-MM-dd" strings.

export interface CurrentUser {
  email: string;
  displayName: string;
  householdId: number;
  householdName: string;
}

export type FlagSource = 'Manual' | 'Offer' | 'Preference';

export interface OfferBadge {
  offerId: number;
  store: string;
  heading: string;
  price: number;
  lastValidDay: string;
  requiresAppOrMembership: boolean;
}

export interface StoreFlag {
  storeId: number;
  store: string;
  source: FlagSource;
  lastValidDay: string | null;
}

export interface ListItem {
  id: number;
  name: string;
  quantity: string | null;
  variant: string | null;
  tag: string | null;
  categoryId: number | null;
  ingredient: string | null;
  isBought: boolean;
  flag: StoreFlag | null;
  bestOffer: OfferBadge | null;
  offerCount: number;
}

export interface Category {
  id: number;
  name: string;
  color: string;
  items: ListItem[];
}

export interface Store {
  id: number;
  name: string;
  color: string;
  isHomeStore: boolean;
  stopGroup: string | null;
}

export interface CategoryRef {
  id: number;
  name: string;
  color: string;
}

export interface ShoppingList {
  id: number;
  name: string;
  categories: Category[];
  stores: Store[];
  allCategories: CategoryRef[];
}

export interface NewItem {
  name: string;
  quantity?: string | null;
  variant?: string | null;
  tag?: string | null;
  store?: string | null;
  offerId?: number | null;
}

export interface ItemUpdate {
  name?: string;
  quantity?: string;
  variant?: string;
  tag?: string;
  categoryId?: number;
  flaggedStoreId?: number;
  clearFlag?: boolean;
  rememberStore?: boolean;
}

export interface Suggestion {
  text: string;
  timesBought: number;
}

export type Period = 'today' | 'week' | 'upcoming';

export interface Offer {
  id: number;
  store: string;
  heading: string;
  description: string | null;
  price: number;
  prePrice: number | null;
  validFrom: string;
  validTo: string;
  isEverydayPrice: boolean;
  requiresAppOrMembership: boolean;
  maxPerCustomer: number | null;
  ingredients: string[];
}

export interface OfferSearch {
  total: number;
  offers: Offer[];
}

export interface TripItem {
  itemId: number;
  name: string;
  variant: string | null;
  quantity: string | null;
  tag: string | null;
  isBought: boolean;
  offerId: number | null;
  offerHeading: string | null;
  unitPrice: number | null;
  count: number;
  linePrice: number | null;
  lastValidDay: string | null;
}

export interface TripVisit {
  storeId: number;
  store: string;
  color: string;
  isHomeStore: boolean;
  subtotal: number;
  items: TripItem[];
}

export interface Trip {
  visits: TripVisit[];
  estimatedSaving: number;
  text: string;
}
