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
  /** The price without the app, when price is a cheaper app or member price. */
  priceWithoutApp?: number | null;
  /** "Lidl Plus", "Netto+" or "medlemspris" when price needs the app or membership. */
  appLabel?: string | null;
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
  isPinned: boolean;
}

export interface StapleSuggestion {
  name: string;
  quantity: string | null;
  reason: 'Pinned' | 'Rhythm';
  rhythmDays: number | null;
  daysSinceLast: number | null;
  lastBought: string | null;
  bestOffer: OfferBadge | null;
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
  /** Only items still to buy; bought items are fetched with BoughtPage. */
  categories: Category[];
  stores: Store[];
  allCategories: CategoryRef[];
  boughtCount: number;
}

export interface BoughtPage {
  items: ListItem[];
  total: number;
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
  /** The price with the chain's app or membership; null when there is none. */
  appPrice: number | null;
}

export interface OfferSearch {
  total: number;
  offers: Offer[];
}

export interface TripStorePrice {
  storeId: number;
  price: number;
  appLabel: string | null;
}

/** One item on a saved trip. itemId is the list item (for ticking); null when removed from the list or finished. */
export interface TripItem {
  id: number;
  itemId: number | null;
  name: string;
  variant: string | null;
  quantity: string | null;
  tag: string | null;
  isBought: boolean;
  movedByHand: boolean;
  offerHeading: string | null;
  unitPrice: number | null;
  count: number;
  linePrice: number | null;
  /** Department, in the home store's walking order (categorySort). */
  category: string | null;
  categoryColor: string | null;
  categorySort: number;
  /** "Lidl Plus", "Netto+" or "medlemspris" when unitPrice is an app or member price. */
  priceLabel: string | null;
  /** The item's best offer today per store, for "Flyt til" (active trips only). */
  storePrices: TripStorePrice[] | null;
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

export interface RecipeIngredient {
  text: string;
  quantity: string | null;
  isMain: boolean;
  ingredient: string | null;
}

export interface Recipe {
  id: number;
  name: string;
  servings: number;
  totalMinutes: number;
  isChildFriendly: boolean;
  method: string | null;
  ingredients: RecipeIngredient[];
}

export interface MealPlanDay {
  date: string;
  recipe: Recipe | null;
  onShoppingList: boolean;
}

export interface MealPlan {
  weekStart: string;
  weekEnd: string;
  days: MealPlanDay[];
}

export interface MealPlanToList {
  weekStart: string;
  recipes: string[];
  items: { name: string; quantity: string | null; recipes: string[] }[];
  skippedDays: number;
}

export interface TripStoreOption {
  storeId: number;
  name: string;
  color: string;
  isHomeStore: boolean;
  stopGroup: string | null;
  onTrip: boolean;
}

/** A saved shopping trip; finishedAt null = in progress. */
export interface Trip {
  id: number;
  createdAt: string;
  finishedAt: string | null;
  estimatedSaving: number;
  visits: TripVisit[];
  stores: TripStoreOption[];
  newItems: { count: number; names: string[] };
  text: string;
}

export interface TripSummary {
  id: number;
  createdAt: string;
  finishedAt: string | null;
  stores: string[];
  itemCount: number;
  boughtCount: number;
  total: number;
}

export interface TripPage {
  items: TripSummary[];
  total: number;
}
