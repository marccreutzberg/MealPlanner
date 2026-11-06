# MealPlanner

Shopping list, offers and meal plan for one household. The requirements live in `SPEC.md` (Danish).
Phase 1 (foundation) is in place: Aspire for local development, API, EF Core on SQL Server, Hangfire, seed data
and cookie login for two users. In production the API runs in IIS on the NUC against its existing SQL Server.

## Structure

```
src/
  MealPlanner.AppHost/          Aspire orchestration (local development only)
  MealPlanner.ServiceDefaults/  OpenTelemetry, health checks
  MealPlanner.Api/
    Domain/                     Entities
    Common/Data/                AppDbContext, SeedData, migrations, startup migration
    Common/Auth/                Identity + cookie setup, user seeding from configuration
    Common/Jobs/                Hangfire setup and dashboard protection
    Features/Auth/              Login, logout, current user, login page
```

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

### Useful endpoints

| Endpoint | |
| --- | --- |
| `POST /api/auth/login` | `{ "email": "...", "password": "..." }`, sets the auth cookie |
| `POST /api/auth/logout` | |
| `GET /api/auth/me` | Current user and household |
| `GET /account/login` | Login page (used to reach the Hangfire dashboard) |
| `GET /hangfire` | Jobs dashboard, login required, blocked through the tunnel |

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

### Deploy a new version

```bash
dotnet publish src/MealPlanner.Api -c Release -o publish
```

Copy `publish\` to the NUC. To avoid locked DLLs, put an `app_offline.htm` in `C:\inetpub\MealPlanner` first,
copy the files (never overwrite `appsettings.Production.json`), then delete `app_offline.htm`.
Migrations run automatically when the app starts.

## Not in phase 1

Tjek offers, matching, MCP and API keys (phase 2), the Angular PWA and the shopping list (phase 3),
recipes and meal plans (phase 4), purchase rhythm (phase 5).

Backup is parked: spec section 9 says daily SQL backup copied to Seafile, but Seafile runs on the same NUC,
so it would not survive losing the machine. Needs an off-site target before it is worth building.
