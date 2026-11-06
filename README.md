# MealPlanner

Shopping list, offers and meal plan for one household. The requirements live in `SPEC.md` (Danish).
In production the API runs in IIS on the NUC against its existing SQL Server.

- **Phase 1 (foundation):** Aspire for local development, API, EF Core on SQL Server, Hangfire, seed data,
  cookie login for two users.
- **Phase 2 (offers and MCP):** daily Tjek fetch, ingredient matching, fuzzy offer search, manual mapping,
  API keys and an MCP server on `/mcp` with `search_offers` and `map_offer`.
- **Phase 3 (shopping list and trip):** Angular PWA in `web/` with the shared list (categories in REMA's walking
  order, type-ahead, offer badges, store flags, realtime via SignalR), offer search and the shopping trip;
  MCP tools `get_shopping_list`, `add_to_shopping_list`, `update_shopping_list_item` and `build_shopping_list`.
- **Phase 4 (recipes and meal plan):** Claude saves recipes (`add_recipe`), gets dishes ranked by the week's offers
  (`suggest_meals`), plans the week (`save_meal_plan`, `get_meal_plan`) and puts it on the list with amounts summed
  across dishes (`build_shopping_list` with `mealPlanWeek`). The app shows the meal plan and can put it on the list.
- **Phase 5 (staples):** "Plejer I at købe?" on the list: pinned items ("altid med") and items due by their buying
  rhythm (median days between purchases, due at 80 %), with offer badges; "har nok" (button or swipe) hides one for
  half its rhythm without touching the history. MCP tool `suggest_staples`. Suggestions start once an item has been
  ticked off on at least three different days.

## Structure

```
src/
  MealPlanner.AppHost/          Aspire orchestration (local development only)
  MealPlanner.ServiceDefaults/  OpenTelemetry, health checks
  MealPlanner.Api/
    Domain/                     Entities with behaviour (Offer validity, MealPlan days, Quantity: amounts that add up)
    Common/Data/                AppDbContext, SeedData, migrations, startup migration
    Common/Auth/                Identity + cookie, API key scheme, household claim, user seeding
    Common/Jobs/                Hangfire setup and dashboard protection
    Common/Tjek/                TjekClient (the only class that knows the Tjek API)
    Common/Matching/            IngredientMatcher, OfferRematcher
    Common/Search/              FuzzySearch
    Common/Categories/          CategoryClassifier (starter dictionary of ~500 words in SeedData)
    Common/Offers/              OfferLookup: today's offers for a shopping item
    Common/Stores/              StoreSelector (trip planning) and TripText (spec 12.6 text, texts in resx)
    Common/Lists/               Shopping list views, ShoppingListWriter (adding items), PurchaseRhythm (staples)
    Common/Meals/               MealScorer (spec 5.3), recipe and meal plan views
    Common/Realtime/            SignalR hub that tells the household's phones the list changed
    Common/Results/             Result type shared by endpoints and MCP tools
    Common/Web/                 Serves the Angular app from wwwroot
    Features/Auth/              Login, logout, current user, login page
    Features/ApiKeys/           Create, list, revoke API keys
    Features/Offers/            FetchOffersJob, SearchOffers, MapOffer
    Features/ShoppingList/      GetList, AddItems, UpdateItem, SetBought, RemoveItems, SuggestItems, BuildTrip
    Features/Recipes/           AddRecipe, GetRecipes, SuggestMeals
    Features/MealPlans/         SaveMealPlan, GetMealPlan, AddMealPlanToList
    Features/Staples/           GetStapleSuggestions, ManageStaples (snooze, pin, unpin)
    Mcp/                        MCP tools calling the feature handlers
tests/
  MealPlanner.Tests/            xUnit: offers, Tjek parsing, matcher (spec 12.4), classifier, trip (spec 12.6),
                                quantities (spec 5.5), meal scoring, meal plans and purchase rhythm
web/                            Angular 22 PWA (builds into src/MealPlanner.Api/wwwroot)
  src/app/i18n/da.json          Every Danish text in the app
```

Tests: `dotnet test --project tests/MealPlanner.Tests`.

## Run locally

Requirements: .NET SDK 10, the Aspire CLI (`dotnet tool install -g Aspire.Cli --version 13.6.4`), a local SQL Server
and Node 24.15+ for the Angular app (`nvm use 24.21.0`; see `web/.nvmrc`). No Docker is needed.

Once, install the web packages (Aspire does not, so `package-lock.json` is never rewritten):

```bash
cd web && npm ci
```

1. Check the connection string in `src/MealPlanner.AppHost/appsettings.Development.json`.
   It uses `np:localhost` (named pipes) because TCP is disabled on the dev machine. Change it if your SQL Server differs.
   The database `MealPlanner` is created automatically.

2. Add the two users as user secrets on the API project (never in Git):

   ```bash
   dotnet user-secrets set "Users:0:Email" "marc@example.com" --project src/MealPlanner.Api
   dotnet user-secrets set "Users:0:DisplayName" "Marc" --project src/MealPlanner.Api
   dotnet user-secrets set "Users:0:Password" "<at least 8 chars, upper, lower, digit, symbol>" --project src/MealPlanner.Api
   dotnet user-secrets set "Users:1:Email" "pia@example.com" --project src/MealPlanner.Api
   dotnet user-secrets set "Users:1:DisplayName" "Pia" --project src/MealPlanner.Api
   dotnet user-secrets set "Users:1:Password" "<...>" --project src/MealPlanner.Api
   ```

   Users are created once at startup. Changing a password in configuration later does not change an existing user.

3. Start everything:

   ```bash
   aspire run
   ```

   Or start `MealPlanner.AppHost` from Visual Studio / VS Code. The Aspire dashboard opens in the browser.
   The app is on <http://localhost:4200> (Angular dev server with live reload; it proxies `/api`, `/hubs`, `/account`
   and `/hangfire` to the API on :5169). After `npm run build` in `web/`, the API also serves the built app on :5169.

4. What happens at startup:
   - EF migrations are applied, then seed data is inserted into empty tables
     (12 categories, 6 chains, 47 ingredients with keywords and exclusions, ~500 category words, the Odder household). New dictionary words are added on every start.
   - The configured users are created.
   - Hangfire creates its tables in the `hangfire` schema and starts its server.

5. Hangfire dashboard: <http://localhost:5169/hangfire> (log in when asked).
   The offer fetch runs at 06:00 every day and again at 08:30 Thursday to Sunday, when new catalogues start.
   On a fresh database, open *Recurring jobs* and click *Trigger now* on `fetch-offers-daily` to get offers at once.

### Endpoints

`src/MealPlanner.Api/MealPlanner.Api.http` has ready-made requests for Visual Studio / VS Code.

| Endpoint | Auth | |
| --- | --- | --- |
| `POST /api/auth/login` | – | `{ "email": "...", "password": "..." }`, sets the auth cookie |
| `POST /api/auth/logout` | cookie or key | |
| `GET /api/auth/me` | cookie or key | Current user and household |
| `GET /api/offers` | cookie or key | `text`, `store`, `period` (`today`, `week` = next 7 days, `upcoming`), `unmapped`, `limit` |
| `POST /api/offers/{id}/mapping` | cookie or key | `{ "ingredients": ["Smør"], "keyword": null, "category": "Mejeri" }` |
| `POST /api/api-keys` | cookie | `{ "name": "..." }`, returns the key **once** |
| `GET /api/api-keys` | cookie | Your keys (without the key itself) |
| `DELETE /api/api-keys/{id}` | cookie | Revoke |
| `GET /api/shopping-list` | cookie or key | The list by category with offer badges, stores and categories |
| `POST /api/shopping-list/items` | cookie or key | `{ "items": [{ "name": "Smør", "quantity": "5 stk.", "variant": "Milbona", "tag": "Bagning", "store": "lidl", "offerId": null }] }` |
| `PATCH /api/shopping-list/items/{id}` | cookie or key | Correct name, quantity, variant, tag, category (learned), store flag, `rememberStore` |
| `POST /api/shopping-list/items/{id}/bought` | cookie or key | `{ "bought": true, "storeId": 4 }`, saves the purchase |
| `DELETE /api/shopping-list/items/{id}` | cookie or key | |
| `POST /api/shopping-list/clear-bought` | cookie or key | Removes ticked items |
| `GET /api/shopping-list/suggestions?q=` | cookie or key | Type-ahead |
| `POST /api/shopping-list/trip` | cookie or key | Plans the trip and returns it per store and as text |
| `/hubs/shopping-list` | cookie | SignalR: `ListChanged` |
| `POST /api/recipes` | cookie or key | Save a recipe (same name replaces); lines with `text`, `quantity`, `isMain` |
| `GET /api/recipes`, `GET /api/recipes/{id}` | cookie or key | |
| `GET /api/recipes/suggestions` | cookie or key | `count`, `childFriendly`, `maxMinutes`, `from` |
| `GET /api/staples/suggestions` | cookie or key | Pinned and due items with offer badges |
| `POST /api/staples/snooze` | cookie or key | `{ "name": "Mælk" }`, "har nok" |
| `GET/PUT/DELETE /api/staples/pinned` | cookie or key | `{ "name": "Kaffe", "defaultQuantity": "1 pose" }`; DELETE takes `?name=` |
| `GET /api/meal-plans/{day}` | cookie or key | The week containing the day |
| `PUT /api/meal-plans/{day}` | cookie or key | `{ "days": [{ "date": "2026-11-09", "recipeId": 1 }] }` (or `"recipe": "Lasagne"`) |
| `POST /api/meal-plans/{day}/to-shopping-list` | cookie or key | `{ "force": false }`; days already on the list are skipped |
| `POST /mcp` | key | MCP (Streamable HTTP) |
| `GET /account/login` | – | Login page (used to reach the Hangfire dashboard) |
| `GET /hangfire` | cookie | Jobs dashboard, blocked through the tunnel |

### How offers are matched

`IngredientMatcher` uses each ingredient's keywords and exclusion words (seeded in `SeedData.cs`):

- A heading is split on `,`, `eller` and `el.`; each part is matched on its own, and exclusions are checked in that part.
- Keywords of four letters or less must be a whole word (`ris` does not hit `pris`), longer ones may be part of a
  compound word (`kartofler` in `bagekartofler`).
- `-lårfilet` after `kyllingebrystfilet` is read as `kyllingelårfilet`.

Manual mappings (`map_offer`) are never overwritten. With one ingredient, the first part of the heading is saved as a
new keyword and all offers are re-matched, so the next catalogue is matched automatically.
On 6/11 2026, 323 of 2,325 offers matched automatically; most of the rest are non-food, wine, sweets and Christmas items.

## Connect Claude (MCP)

1. Log in (`POST /api/auth/login`, or the login page) and create a key: `POST /api/api-keys` with a name.
   Copy the `key` from the response; it is not shown again. Each person makes their own key on their own account.
2. In Claude (private account, not Team): *Settings > Connectors > Add custom connector*, URL
   `https://<your public hostname>/mcp`, and under *Request headers* add `X-Api-Key: <the key>`.
3. Ask Claude e.g. "Hvilke kyllingetilbud er der i denne uge?" or "Find tilbud uden råvare og map dem".

If the private account has no *Request headers* field, see the fallback in spec section 9 (not built yet).
Revoke a key with `DELETE /api/api-keys/{id}`.

### Migrations

```bash
dotnet ef migrations add <Name> --project src/MealPlanner.Api --output-dir Common/Data/Migrations
```

They are applied automatically at the next startup.

## Run on the NUC (IIS)

The API is a plain ASP.NET Core app hosted in IIS, using the SQL Server that already runs on the NUC.
Cloudflare Tunnel (`cloudflared` as a Windows service) is the only way in from the internet.

### One-time setup

1. **IIS features:** install the [ASP.NET Core 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0)
   and the IIS feature *Application Initialization* (needed for Hangfire, see below). Restart IIS afterwards.

2. **App pool** `MealPlanner`:
   - .NET CLR version: *No Managed Code*.
   - Start Mode: **AlwaysRunning**, Idle Time-out: **0**.
   - Recycling: clear *Regular Time Interval* (default 1740 minutes).

   Hangfire runs inside the app. If IIS stops the app pool after 20 idle minutes or recycles it,
   no background jobs run until the next request. Phase 2's daily offer fetch at 06:00 depends on these settings.

3. **Site** `MealPlanner`: physical path e.g. `C:\inetpub\MealPlanner`, app pool `MealPlanner`,
   binding `http` on port 8080, **Preload Enabled = True** (Advanced Settings).

4. **SQL Server access with Windows login** (`Integrated Security=true`, no password in the connection string).
   The app connects as the app pool's identity. Choose one:

   - **Simplest:** set the app pool's *Identity* (Advanced Settings) to your own Windows account. It then has the
     same SQL Server access you have, and nothing needs to be created in SQL Server.
   - **Separate identity:** keep `ApplicationPoolIdentity` and give `IIS APPPOOL\MealPlanner` its own login:

   ```sql
   CREATE LOGIN [IIS APPPOOL\MealPlanner] FROM WINDOWS;
   -- After the first start has created the database:
   USE MealPlanner;
   CREATE USER [IIS APPPOOL\MealPlanner] FOR LOGIN [IIS APPPOOL\MealPlanner];
   ALTER ROLE db_owner ADD MEMBER [IIS APPPOOL\MealPlanner];
   ```

   The app creates the database itself on first start, which needs `dbcreator` for that one start:
   `ALTER SERVER ROLE dbcreator ADD MEMBER [IIS APPPOOL\MealPlanner];` (remove it again afterwards if you like),
   or create an empty `MealPlanner` database by hand first.

5. **Settings with secrets** in `C:\inetpub\MealPlanner\appsettings.Production.json` (only on the NUC, git-ignored):

   ```json
   {
     "ConnectionStrings": {
       "mealplanner": "Data Source=np:localhost;Initial Catalog=MealPlanner;Integrated Security=true;TrustServerCertificate=true"
     },
     "Users": [
       { "Email": "marc@example.com", "DisplayName": "Marc", "Password": "<...>" },
       { "Email": "pia@example.com", "DisplayName": "Pia", "Password": "<...>" }
     ]
   }
   ```

   `np:localhost` uses named pipes; with TCP disabled on SQL Server, `localhost` costs about 15 s per new connection under IIS.
   The passwords are only read on the very first start; you can remove them from the file afterwards.

6. **Cloudflare Tunnel:** in Cloudflare Zero Trust (Networks > Tunnels) create a tunnel, install the Windows connector
   with the command it shows, and add a public hostname pointing to `http://localhost:8080`.
   The Hangfire dashboard answers 404 to anything that came through the tunnel, so reach it on the LAN or via WireGuard:
   `http://<nuc>:8080/hangfire`.
   Restrict `/mcp` to Anthropic's IP ranges with a Cloudflare WAF rule (spec 9); the API key is still required.

### Deploy a new version

```bash
dotnet publish src/MealPlanner.Api -c Release -o publish
```

Publishing also builds the Angular app (`npm ci` if needed, then `npm run build`) into `wwwroot`, so Node 24 must be
active. Add `-p:SkipWebBuild=true` to publish only the API.

Copy `publish\` to the NUC. To avoid locked DLLs, put an `app_offline.htm` in `C:\inetpub\MealPlanner` first,
copy the files (never overwrite `appsettings.Production.json`), then delete `app_offline.htm`.
Migrations run automatically when the app starts.

## Not built yet

All five phases of v1 are built. Not in v1 by design (spec 2): editing recipes in the app, more households,
recipe import from URLs.

Known gaps:
- Prices are compared per package as printed. Kg prices ("pr. ½ kg", "Kg-pris maks.") and sizes are not normalised,
  so a small Fanta can win over a 1,5 l bottle.
- Decided not to call Claude Haiku for unknown words (spec 4, point 4). They land in "Andet"; moving one in the app,
  or letting Claude Desktop go through "Andet" with `get_shopping_list` and `update_shopping_list_item(categoryId)`,
  saves a learned rule so the word lands right next time.
- Chosen type-ahead corrections are not saved as aliases yet ("hyteost" -> hytteost).
- The MCP secret-path fallback is only needed if *Request headers* is missing on the private Claude accounts.

Backup is parked: spec section 9 says daily SQL backup copied to Seafile, but Seafile runs on the same NUC,
so it would not survive losing the machine. Needs an off-site target before it is worth building.
