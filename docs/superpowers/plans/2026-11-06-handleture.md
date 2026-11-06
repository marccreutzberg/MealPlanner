# Handleture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Saved shopping trips: create from the open list, choose stores, move one item, take new list items along, finish, and an archive that can put items back on the list — in the API, MCP and the Angular app.

**Architecture:** A trip is a snapshot (`ShoppingTrip` + `ShoppingTripStore` + `ShoppingTripItem`) that points at list items; the tick stays on `ShoppingListItem`. Store choice and distribution reuse `StoreSelector` (new public `Distribute`). One handler per use case in `Features/Trips`, shared logic in `Common/Trips`, MCP tools call the same handlers.

**Tech Stack:** .NET 10, EF Core 10 (SQL Server), xUnit v3, ModelContextProtocol.AspNetCore, Angular 22 (signals, standalone), SignalR.

**Spec:** `docs/superpowers/specs/2026-11-06-handleture-design.md`

## Global Constraints

- Code, names, comments in English; user-facing text Danish in `web/src/app/i18n/da.json`, never hardcoded.
- Comments short and in English. LF line endings as the files already have; `.ps1` with æøå need UTF-8 BOM.
- Every household query filters on the caller's `HouseholdId`; another household's trip answers 404.
- Changing a finished trip answers 400 with `Error.Invalid("The trip is finished")`.
- `StoreSelector.Choose` must return the same plans as today (existing `StoreSelectorTests` stay green).
- All trip changes call `ListNotifier.ListChangedAsync(householdId)`; no new SignalR message.
- No commits or pushes without Marc's OK (leave changes in the working tree).
- Node for `ng build`: prepend `C:\Users\MarcCreutzberg\AppData\Local\nvm\v24.21.0` to PATH.

## Review Focus

1. Pia adds "Kaffe" to the list while it is already an open line on the trip (amounts merged): the active trip shows the new quantity → Task 4 test `Active_trip_shows_live_list_values`.
2. "Fjern købte varer fra listen" during an active trip: bought trip items stay shown as bought and are bought in the archive → Task 3 test `Freeze_copies_list_values_and_bought_state` + Task 4 manual check.
3. Removing a store whose items were moved there by hand: those items go back into the distribution → Task 1 test `Distribute_moves_items_of_removed_store_home_or_to_cheapest`.
4. New items when the flagged store is not on the trip: they land in the home store, no new stop → Task 1 test `Distribute_sends_flagged_items_home_when_their_store_is_not_chosen`.
5. Creating a trip with an empty list: a trip with only the home store and no items, no crash → Task 4 test `Create_with_empty_list_gives_home_store_only`.

---

### Task 1: `StoreSelector.Distribute` and removal of the old trip

**Files:**
- Modify: `src/MealPlanner.Api/Common/Stores/StoreSelector.cs`
- Modify (revert uncommitted): `src/MealPlanner.Api/Domain/ShoppingListItem.cs` (remove `OnTripOn` and the `System.Linq.Expressions` using), delete `tests/MealPlanner.Tests/Domain/ShoppingListItemTests.cs`
- Delete: `src/MealPlanner.Api/Features/ShoppingList/BuildTrip.cs` (and its registration in `ShoppingListModule`) — the MCP tool that uses it is rewritten in Task 5; until then keep the build green by moving that tool body to Task 5 in the same working session.
- Test: `tests/MealPlanner.Tests/Common/StoreSelectorTests.cs`

**Interfaces:**
- Produces: `public static Plan Distribute(List<Store> stores, IReadOnlyCollection<int> chosenStoreIds, List<Item> items)` — visitable = every store whose stop key (stop group or own id) matches a chosen store or the home store; the home store is always visitable. Same assignment, saving and visit order as `Choose`. `Choose` calls `Distribute` after picking stops.
- Changed rule in `Assign`: an item whose `FlaggedStoreId` is not visitable goes to the home store with no offer (cannot happen in `Choose`, where flagged stores are always chosen).

- [ ] **Step 1: Write failing tests**
  - `Distribute_uses_only_chosen_stores_plus_home`: list with offers at Netto and Lidl, chosen `[Netto.Id]` → visits are Netto and REMA only.
  - `Distribute_treats_stop_group_as_one_stop`: chosen `[Lidl.Id]`, item cheapest at Coop 365 → lands in Coop 365.
  - `Distribute_sends_flagged_items_home_when_their_store_is_not_chosen`: item flagged to Kvickly, chosen `[Netto.Id]` → in REMA with `Offer == null`.
  - `Distribute_moves_items_of_removed_store_home_or_to_cheapest`: item with offers at Kvickly 10 and Netto 12, chosen `[Netto.Id]` → Netto, price 12.
  - `Choose_is_unchanged_for_birthday_list`: existing acceptance tests still pass (no new code).
- [ ] **Step 2: Run** `dotnet test --filter-class "MealPlanner.Tests.Common.StoreSelectorTests"` (or `dotnet run` in the test project with `-class`) → new tests fail to compile.
- [ ] **Step 3: Implement** `Distribute`, refactor `Choose` to use it; remove `BuildTrip` and `OnTripOn` as listed.
- [ ] **Step 4: Run** `dotnet test` → all green.

### Task 2: Domain entities, EF configuration and migration

**Files:**
- Create: `src/MealPlanner.Api/Domain/ShoppingTrip.cs` (contains `ShoppingTrip`, `ShoppingTripStore`, `ShoppingTripItem`)
- Modify: `src/MealPlanner.Api/Common/Data/AppDbContext.cs` (DbSets `ShoppingTrips`, `ShoppingTripStores`, `ShoppingTripItems`)
- Create: `src/MealPlanner.Api/Common/Data/Configurations/TripConfigurations.cs`
- Create: migration `ShoppingTrips` via `dotnet ef migrations add ShoppingTrips --project src/MealPlanner.Api --output-dir Common/Data/Migrations`
- Test: `tests/MealPlanner.Tests/Domain/ShoppingTripTests.cs`

**Interfaces (Produces):**
```csharp
public class ShoppingTrip {
    int Id; int HouseholdId; Household Household; DateTimeOffset CreatedAt; string? CreatedByUserId; User? CreatedByUser;
    DateTimeOffset? FinishedAt; decimal EstimatedSaving;
    List<ShoppingTripStore> Stores; List<ShoppingTripItem> Items;
    bool IsActive => FinishedAt is null;
    void Finish(DateTimeOffset now); // freezes every item that still has its list item, then sets FinishedAt
}
public class ShoppingTripStore { int TripId; ShoppingTrip Trip; int StoreId; Store Store; }   // key (TripId, StoreId)
public class ShoppingTripItem {
    int Id; int TripId; ShoppingTrip Trip; int? ShoppingListItemId; ShoppingListItem? ShoppingListItem;
    int StoreId; Store Store; string Name; string? Quantity; string? Variant; string? Tag;
    int? OfferId; string? OfferHeading; decimal? UnitPrice; int Count; decimal? LinePrice; DateOnly? LastValidDay;
    bool MovedByHand; bool WasBought;
    void Freeze();   // copies Name, Quantity, Variant, Tag and WasBought = IsBought from ShoppingListItem (must be loaded); no-op when null
    void PlaceAt(int storeId, int? offerId, string? offerHeading, decimal? unitPrice, int count, DateOnly? lastValidDay, bool byHand);
}
```
- Config: `ShoppingTrip` unique index on `HouseholdId` filtered `"[FinishedAt] IS NULL"`; `Items`/`Stores` cascade from trip; `ShoppingTripItem.ShoppingListItem` with `OnDelete(DeleteBehavior.ClientSetNull)` (no DB cascade); string lengths as `ShoppingListItemConfiguration` (Name 200, Quantity 100, Variant 200, Tag 100, OfferHeading 300). Store/Household/User FKs are made Restrict by `AppDbContext`'s existing rule.

- [ ] **Step 1: Write failing tests**: `Finish_sets_time_and_freezes_items` (item linked to a bought list item → `WasBought == true`, name copied, `IsActive == false`); `Freeze_copies_list_values_and_bought_state`; `Freeze_without_list_item_keeps_snapshot`; `PlaceAt_by_hand_marks_item_moved`.
- [ ] **Step 2: Run** tests → compile failure.
- [ ] **Step 3: Implement** entities and configuration; add the migration; inspect it (three tables, filtered unique index, FK from trip item to list item with `ReferentialAction.NoAction`).
- [ ] **Step 4: Run** `dotnet build` and `dotnet test` → green.

### Task 3: `Common/Trips` — planner, views, freezing and store names

**Files:**
- Create: `src/MealPlanner.Api/Common/Trips/TripPlanner.cs`, `TripViews.cs`, `TripFreezer.cs`, `StoreNames.cs`
- Modify: `src/MealPlanner.Api/Common/Stores/TripText.cs` only if needed to format from a `StoreSelector.Plan` built from saved rows (prefer building a `Plan` and reusing `Format`).
- Test: `tests/MealPlanner.Tests/Common/TripPlannerTests.cs`

**Interfaces (Produces):**
```csharp
// Pure helpers, testable without a database.
public static class TripPlanner {
    // List item -> planner item. Moved-by-hand trip items act as flagged to their store when that store is chosen.
    static StoreSelector.Item ToPlannerItem(ShoppingListItem item, OfferLookup offers, int? keepAtStoreId);
    // Applies a planned item to a trip item (store, offer snapshot, count, line price), keeping MovedByHand as given.
    static void Apply(ShoppingTripItem target, StoreSelector.PlannedItem planned, int storeId, bool byHand);
}
public sealed class TripService(AppDbContext db, TimeProvider clock) {   // DB work shared by the slices
    Task<ShoppingTrip?> FindAsync(int householdId, int tripId, CancellationToken ct);            // includes Stores, Items(+ShoppingListItem)
    Task<ShoppingTrip?> FindActiveAsync(int householdId, CancellationToken ct);
    Task<ShoppingTrip> CreateAsync(int householdId, string? userId, IReadOnlyCollection<int>? storeIds, CancellationToken ct); // finishes the active trip first
    Task SetStoresAsync(ShoppingTrip trip, IReadOnlyCollection<int> storeIds, CancellationToken ct);   // 5.3
    Task<Result<bool>> MoveAsync(ShoppingTrip trip, int tripItemId, int storeId, CancellationToken ct); // 5.4
    Task<int> AddNewItemsAsync(ShoppingTrip trip, CancellationToken ct);                            // 5.5, returns count added
    // All write methods set ShoppingListItem.PlannedStoreId for affected open items and SaveChanges.
}
public static class TripViews {
    sealed record TripItemDto(int Id, int? ItemId, string Name, string? Variant, string? Quantity, string? Tag, bool IsBought,
        bool MovedByHand, string? OfferHeading, decimal? UnitPrice, int Count, decimal? LinePrice, DateOnly? LastValidDay);
    sealed record VisitDto(int StoreId, string Store, string Color, bool IsHomeStore, decimal Subtotal, List<TripItemDto> Items);
    sealed record StoreOptionDto(int StoreId, string Name, string Color, bool IsHomeStore, string? StopGroup, bool OnTrip);
    sealed record NewItemsDto(int Count, List<string> Names);   // Names = first 3
    sealed record TripDto(int Id, DateTimeOffset CreatedAt, DateTimeOffset? FinishedAt, decimal EstimatedSaving,
        List<VisitDto> Visits, List<StoreOptionDto> Stores, NewItemsDto NewItems, string Text);
    sealed record TripSummaryDto(int Id, DateTimeOffset CreatedAt, DateTimeOffset? FinishedAt, List<string> Stores,
        int ItemCount, int BoughtCount, decimal Total);
    Task<TripDto> LoadAsync(AppDbContext db, ShoppingTrip trip, IStringLocalizer<TripTexts> texts, CancellationToken ct);
}
public static class TripFreezer {
    // Freezes trip items pointing at these list items and clears the link; call before ExecuteDelete on list items.
    Task FreezeAsync(AppDbContext db, IQueryable<ShoppingListItem> doomed, CancellationToken ct);
}
public static class StoreNames {
    // "Lidl" -> id among the household's active stores (case-insensitive contains, like ShoppingListWriter).
    Task<Result<List<int>>> ResolveAsync(AppDbContext db, int householdId, IEnumerable<string> names, CancellationToken ct);
}
```
- View rules: active trip → name/quantity/variant/tag/bought from the list item when linked, else the snapshot; unlinked + not `WasBought` → hidden. Finished trip → snapshot only. Visits: stores with items plus the home store; order as `StoreSelector` (others by subtotal desc, home last). New items = open list items of the active list not linked from the trip (only for active trips; `Count 0` otherwise). `Text` = `TripText.Format` on a `Plan` built from the visits.
- `CreateAsync`: open items only; `storeIds == null` → `Choose` with the household's `ThirdStoreThreshold`; else `Distribute(stores, storeIds)`. Trip stores = stores with items plus home (auto) or the requested ids plus home (manual). Saves `EstimatedSaving`.
- `SetStoresAsync`: home always kept; redistribute items not bought (bought ones stay); moved-by-hand items keep their store if still chosen (via `keepAtStoreId`), else `MovedByHand = false` and redistribute.
- `MoveAsync`: store must be an active household store (else `Error.Invalid`); adds `ShoppingTripStore` if missing; offer = cheapest current offer for the item in that store, else none; `MovedByHand = true`. Unknown trip item → `Error.NotFound`.
- `AddNewItemsAsync`: distribute new items over the trip's current stores only.

- [ ] **Step 1: Write failing tests** (pure helpers): `ToPlannerItem_keeps_moved_item_at_its_store`; `ToPlannerItem_uses_list_flag_otherwise`; `Apply_copies_offer_snapshot_and_line_price` (count 2 × 9.95 = 19.90).
- [ ] **Step 2: Run** → fail. **Step 3: Implement** all four files. **Step 4: Run** `dotnet test` → green.

### Task 4: `Features/Trips` endpoints, freezing on delete, wiring

**Files:**
- Create: `src/MealPlanner.Api/Features/Trips/TripsModule.cs`, `CreateTrip.cs`, `GetActiveTrip.cs`, `GetTrip.cs`, `ListTrips.cs`, `SetTripStores.cs`, `MoveTripItem.cs`, `AddNewItemsToTrip.cs`, `FinishTrip.cs`, `AddTripItemsToList.cs`
- Modify: `src/MealPlanner.Api/Program.cs` (`AddTrips()`, `MapTrips()`), `Features/ShoppingList/RemoveItems.cs` (call `TripFreezer.FreezeAsync` before both deletes)

**Interfaces (Produces):** routes from spec 7, all `RequireAuthorization()` like `MapShoppingList`:
- `GET /api/trips/active` → `TripDto` or 204 · `POST /api/trips` body `{ storeIds?: int[] }` → `TripDto`
- `PUT /api/trips/{id}/stores` `{ storeIds }` · `POST /api/trips/{id}/items/{tripItemId}/store` `{ storeId }` · `POST /api/trips/{id}/new-items` → `TripDto`
- `POST /api/trips/{id}/finish` → `{ remaining: int }` (open list items left)
- `GET /api/trips?skip=0&take=20` → `{ items: TripSummaryDto[], total }` (finished only, newest first, take clamped 1..100)
- `GET /api/trips/{id}` → `TripDto` · `POST /api/trips/{id}/to-list` `{ tripItemIds }` → `{ added: int }` via `ShoppingListWriter.AddAsync` (name, quantity, variant, tag; no store)
- Handlers: `CreateTrip.Handler.HandleAsync(Command(List<int>? StoreIds), int householdId, string userId, ct) -> Task<Result<TripDto>>`; the rest `HandleAsync(..., int householdId, ct) -> Task<Result<T>>`.

- [ ] **Step 1:** Implement slices and wiring; `dotnet build` green.
- [ ] **Step 2:** Pure test where possible: `Create_with_empty_list_gives_home_store_only` and `Active_trip_shows_live_list_values` against `TripViews`' row mapping (extract the mapping into a static `TripViews.ToItem(ShoppingTripItem item, bool active)` so it is testable without a database).
- [ ] **Step 3:** Check SQL of the new-items and freeze queries with `ToQueryString()` in a temporary test (delete it afterwards).
- [ ] **Step 4:** `dotnet test` green.

### Task 5: MCP tools

**Files:**
- Modify: `src/MealPlanner.Api/Mcp/ShoppingListTools.cs` (`build_shopping_list` → creates a trip; returns `TripViews.TripDto`; new optional `List<string>? stores`)
- Create: `src/MealPlanner.Api/Mcp/TripTools.cs`: `get_trip(int? tripId)`, `set_trip_stores(List<string> stores)`, `move_trip_item(int tripItemId, string store)`, `add_new_items_to_trip()`, `finish_trip()` — all on the active trip except `get_trip` with an id; store names resolved with `StoreNames.ResolveAsync`; errors via `McpException` like `Unwrap`.
- Descriptions in English, say that `build_shopping_list` finishes an active trip and that ticking happens in the app.

- [ ] **Step 1:** Implement; `dotnet build` and `dotnet test` green.

### Task 6: Angular — trip page, sheets, archive, list button

**Files:**
- Modify: `web/src/app/core/models.ts` (Trip types mirroring `TripViews` DTOs), `core/api.ts` (`activeTrip()`, `createTrip(storeIds?)`, `setTripStores(id, storeIds)`, `moveTripItem(id, tripItemId, storeId)`, `addNewItemsToTrip(id)`, `finishTrip(id)`, `trips(skip, take)`, `tripById(id)`, `tripToList(id, tripItemIds)`; remove `trip()`), `app.routes.ts` (`tur`, `tur/arkiv`, `tur/:id`), `pages/list/list-page.ts` ("Opret handletur": if an active trip exists `confirm(translate('trip.replaceConfirm'))`, then create and navigate to `/tur`), `i18n/da.json`
- Rewrite: `web/src/app/pages/trip/trip-page.ts` + `.scss`
- Create: `pages/trip/trip-visits.ts` (renders visits; inputs `trip`, `readonly`, `selectable`; outputs `toggled(item)`, `opened(item)`, `selection` signal), `pages/trip/stores-sheet.ts` (one row per stop, home locked), `pages/trip/move-sheet.ts` ("Flyt til" store list), `pages/trip/trips-archive-page.ts` (+ "Hent flere"), `pages/trip/trip-detail-page.ts` (readonly visits with checkboxes + "Læg på listen")
- Trip page reloads on `Realtime.listChanged`; ticking calls `ListStore.toggleBought`-equivalent `api.setBought(itemId, bought, visit.storeId)` then reloads.
- i18n keys (new under `trip`): `title` "Handletur", `create` "Opret handletur", `none` "Ingen handletur i gang. Opret en, når listen er klar.", `replaceConfirm` "Der er en tur i gang. Afslut den og opret en ny?", `stores` "Butikker", `storesTitle` "Butikker på turen", `save` "Gem", `newItems` "Nye på listen: {names}", `newItemsMore` "+{count}", `newItemsAdd` "Tag med på turen", `moveTitle` "Flyt {name} til", `finish` "Afslut tur", `finished` "Turen er afsluttet. {count} varer bliver på listen.", `history` "Tidligere ture", `more` "Hent flere", `toList` "Læg på listen", `addedToList` "{count} varer er lagt på listen.", `createdOn` "{date}", `summary` "{bought} af {count} købt"; `list.findOffers` → `list.createTrip` "Opret handletur". Keep existing `trip.*` keys that stay in use; remove unused ones.

- [ ] **Step 1:** Implement; `ng build` green (Node 24 on PATH).

### Task 7: Verify and hand over

- [ ] Update the scratchpad mock server (`mock-api.js`) with the trip endpoints and test in the browser pane at 375×812, light and dark: create, choose stores, move item, new-items banner and add, tick on trip ↔ list, finish (message with count), archive + "Hent flere", detail + "Læg på listen".
- [ ] `dotnet test` and `ng build` green; run `deploy/build-package.ps1` once to prove the package builds.
- [ ] Update `SPEC.md` section 6 (MCP table) and section 8 (data model) with the trip tables and tools.
- [ ] Report to Marc: what was tested, what was not (real DB, phone), migration `ShoppingTrips` runs on start; ask before commit/push.
