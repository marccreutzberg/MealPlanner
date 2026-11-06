# MealPlanner

Shopping list, offers and meal plan for one household. The requirements live in `SPEC.md` (Danish).
Phase 1 (foundation) is in place: Aspire, API, EF Core on SQL Server, Hangfire, seed data and cookie login for two users.

## Structure

```
src/
  MealPlanner.AppHost/          Aspire orchestration (local run + compose generation for the NUC)
  MealPlanner.ServiceDefaults/  OpenTelemetry, health checks
  MealPlanner.Api/
    Domain/                     Entities
    Common/Data/                AppDbContext, SeedData, migrations, startup migration
    Common/Auth/                Identity + cookie setup, user seeding from configuration
    Common/Jobs/                Hangfire setup and dashboard protection
    Features/Auth/              Login, logout, current user, login page
deploy/                         Generated docker-compose.yaml and .env.example
```

## Run locally

Requirements: .NET SDK 10, the Aspire CLI (`dotnet tool install -g Aspire.Cli --version 13.6.4`) and a local SQL Server.
No Docker is needed locally: the AppHost points the API at your own SQL Server.

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

## Run on the NUC (Docker Compose)

The NUC runs Docker Desktop with Linux containers (WSL2); the SQL Server image only exists for Linux.
`deploy/docker-compose.yaml` contains three services:

- `sql`: SQL Server 2022 as Express (`MSSQL_PID=Express`), data in the `mealplanner-sql` volume.
- `api`: built from this repository, listening on port 8080 on the NUC.
- `cloudflared`: the Cloudflare Tunnel, the only way in from the internet.

### First time

```bash
git clone <repo> MealPlanner
cd MealPlanner
copy deploy\.env.example deploy\.env
```

Fill in `deploy\.env` (SQL password, the two users, tunnel token). Then:

```bash
docker compose -f deploy/docker-compose.yaml up -d --build
```

The API waits for SQL Server (it retries for about two minutes), applies migrations, seeds and creates the users.
Follow it with `docker compose -f deploy/docker-compose.yaml logs -f api`.

### New version

```bash
git pull
docker compose -f deploy/docker-compose.yaml up -d --build
```

### Cloudflare Tunnel

Create the tunnel in Cloudflare Zero Trust (Networks > Tunnels) and put its token in `.env`.
Add a public hostname pointing to `http://api:8080` (the compose service name).
The Hangfire dashboard answers 404 to anything that came through the tunnel, so reach it on the LAN or via WireGuard:
`http://<nuc>:8080/hangfire`.

### Changing the deployment

`deploy/docker-compose.yaml` is generated from `src/MealPlanner.AppHost/AppHost.cs`. Change the AppHost, then regenerate:

```bash
aspire publish --project src/MealPlanner.AppHost/MealPlanner.AppHost.csproj -o deploy
```

This also rewrites `deploy/.env` with empty values on the dev machine; that file is git-ignored.
Update `deploy/.env.example` if parameters were added.

## Not in phase 1

Tjek offers, matching, MCP and API keys (phase 2), the Angular PWA and the shopping list (phase 3),
recipes and meal plans (phase 4), purchase rhythm (phase 5). Daily SQL backup to Seafile is not set up yet.
