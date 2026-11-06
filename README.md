# MealPlanner

Shopping list, offers and meal plan for one household. The requirements live in `SPEC.md` (Danish).
In production the API runs in IIS on the NUC against its existing SQL Server.

- **Phase 1 (foundation):** Aspire for local development, API, EF Core on SQL Server, Hangfire, seed data,
  cookie login for two users.
- **Phase 2 (offers and MCP):** daily Tjek fetch, ingredient matching, fuzzy offer search, manual mapping,
  API keys and an MCP server on `/mcp` with `search_offers` and `map_offer`.

## Structure

```
src/
  MealPlanner.AppHost/          Aspire orchestration (local development only)
  MealPlanner.ServiceDefaults/  OpenTelemetry, health checks
  MealPlanner.Api/
    Domain/                     Entities with behaviour (Offer validity and conditions, ApiKey.Revoke)
    Common/Data/                AppDbContext, SeedData, migrations, startup migration
    Common/Auth/                Identity + cookie, API key scheme, household claim, user seeding
    Common/Jobs/                Hangfire setup and dashboard protection
    Common/Tjek/                TjekClient (the only class that knows the Tjek API)
    Common/Matching/            IngredientMatcher, OfferRematcher
    Common/Search/              FuzzySearch
    Common/Results/             Result type shared by endpoints and MCP tools
    Features/Auth/              Login, logout, current user, login page
    Features/ApiKeys/           Create, list, revoke API keys
    Features/Offers/            FetchOffersJob, SearchOffers, MapOffer
    Mcp/                        MCP tools calling the feature handlers
tests/
  MealPlanner.Tests/            xUnit: offers, Tjek parsing, matcher (spec 12.4 cases), fuzzy search
```

Tests: `dotnet test --project tests/MealPlanner.Tests`.

## Run locally

Requirements: .NET SDK 10, the Aspire CLI (`dotnet tool install -g Aspire.Cli --version 13.6.4`) and a local SQL Server.
No Docker is needed.

1. Check the connection string in `src/MealPlanner.AppHost/appsettings.Development.json`.
   It uses `np:localhost` (named pipes) because TCP is disabled on the dev machine. Change it if your SQL Server differs.
   The database `MealPlanner` is created automatically.

2. Add the two users as user secrets on the API project (never in Git):

   ```bash
   dotnet user-secrets set "Users:0:Email" "marc@example.com" --project src/MealPlanner.Api
   dotnet user-secrets set "Users:0:DisplayName" "Marc" --project src/MealPlanner.Api
   dotnet user-secrets set "Users:0:Password" "<at least 10 chars, upper, lower, digit, symbol>" --project src/MealPlanner.Api
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

4. What happens at startup:
   - EF migrations are applied, then seed data is inserted into empty tables
     (12 categories, 6 chains, 47 ingredients with keywords and exclusions, ~200 category words, the Odder household).
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

4. **SQL Server login** for the app pool identity, so the connection string needs no password:

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

Copy `publish\` to the NUC. To avoid locked DLLs, put an `app_offline.htm` in `C:\inetpub\MealPlanner` first,
copy the files (never overwrite `appsettings.Production.json`), then delete `app_offline.htm`.
Migrations run automatically when the app starts.

## Not built yet

The Angular PWA, shopping list and trip planning with their MCP tools (phase 3), recipes and meal plans (phase 4),
purchase rhythm (phase 5). Also open from phase 2: kg-price normalisation ("pr. ½ kg", "Kg-pris maks.") is left for
trip planning in phase 3, and the secret-path fallback for MCP is only needed if *Request headers* is missing.

Backup is parked: spec section 9 says daily SQL backup copied to Seafile, but Seafile runs on the same NUC,
so it would not survive losing the machine. Needs an off-site target before it is worth building.
