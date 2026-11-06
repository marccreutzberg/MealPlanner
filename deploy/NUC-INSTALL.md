# Installing MealPlanner on the NUC

For Claude Code (or Marc) on the NUC. MealPlanner is Marc and Pia's shared shopping list with this week's offers,
meal plan and an MCP server for Claude. It is an ASP.NET Core 10 app with an Angular PWA, hosted in **IIS** against
the **SQL Server that already runs on the NUC**, reached from the internet only through **Cloudflare Tunnel**.

There are two ways to get the app onto the NUC:

- **From the Git repository (recommended):** clone the private GitHub repo, build the package on the NUC, install.
  Updates are `git pull` + build + `update.ps1`. Needs the .NET 10 SDK, Node 24 and Git on the NUC:

  ```powershell
  winget install Microsoft.DotNet.SDK.10 CoreyButler.NVMforWindows Git.Git   # then open a new shell
  nvm install 24.21.0; nvm use 24.21.0
  git clone https://github.com/<owner>/MealPlanner.git C:\src\MealPlanner
  cd C:\src\MealPlanner
  .\deploy\build-package.ps1                 # -> artifacts\MealPlanner-deploy (app + scripts)
  cd artifacts\MealPlanner-deploy            # continue with step 1 below from this folder
  ```

- **From a package built elsewhere:** copy `artifacts\MealPlanner-deploy` from the dev machine.

Either way, the package folder contains:

| Path | What |
| --- | --- |
| `app/` | The published app (API + Angular). No .NET SDK or Node needed on the NUC. |
| `install.ps1` | First install: IIS features, app pool, site on port 8080, firewall, settings template. |
| `update.ps1` | New versions: takes the site offline, swaps the files, keeps the settings. |
| `appsettings.Production.template.json` | Connection string and the two users; copied to the site by `install.ps1`. |

Ground rules: do not change SQL Server security or configuration without asking Marc; Marc types all passwords himself
(the two users' passwords) - do not ask for them in chat.

## 0. No SQL Server yet? Install SQL Server Express

Only if `Get-Service MSSQL*` finds nothing. Ask Marc before installing. Express is free and its size limit
(10 GB, 50 GB in 2025) is plenty for MealPlanner.

```powershell
winget search "SQL Server" --source winget     # pick the newest Express (2025 if listed, else 2022)
winget install Microsoft.SQLServer.2022.Express --accept-package-agreements --accept-source-agreements
winget install Microsoft.Sqlcmd                 # the sqlcmd used in the checks below
```

Wanted result (check after install; fix with SQL Server Configuration Manager or a re-run of setup if not):
- Instance name `SQLEXPRESS` (service `MSSQL$SQLEXPRESS`), Windows authentication only, service started and set to
  Automatic.
- Marc's Windows account is `sysadmin`:
  `sqlcmd -S lpc:.\SQLEXPRESS -E -C -Q "SELECT IS_SRVROLEMEMBER('sysadmin')"` returns 1.
  If winget's silent install did not add him, run the Express setup once more with
  `/ACTION=Install /FEATURES=SQLENGINE /INSTANCENAME=SQLEXPRESS /SQLSYSADMINACCOUNTS="<DOMAIN\user>" /IACCEPTSQLSERVERLICENSETERMS /Q`
  (download the installer from https://www.microsoft.com/sql-server/sql-server-downloads, *Express* -> *Download Media* -> *Express Core*).
- TCP/IP is not needed: the app connects with shared memory (`lpc:`), and nothing outside the NUC should reach SQL Server.

The connection string's Data Source is then `lpc:.\SQLEXPRESS`.

## 1. Check the machine

```powershell
Get-Service MSSQL*                       # which SQL Server instance(s) run here
dotnet --list-runtimes                   # needs Microsoft.AspNetCore.App 10.x (from the Hosting Bundle)
```

- SQL Server instance -> Data Source for the connection string:
  - default instance (`MSSQLSERVER`): `lpc:.`
  - named instance, e.g. `MSSQL$SQLEXPRESS`: `lpc:.\SQLEXPRESS`
  `lpc:` is shared memory (same machine). Do not use plain `localhost`: when TCP is disabled, every new connection
  waits ~15 s before SqlClient falls back.
- The app runs as the app pool's own account `IIS AppPool\MealPlanner` (no password). It needs a SQL login and
  `db_owner` in the `MealPlanner` database; the app then creates its tables itself. Marc's Windows account sets this up
  once, so first check that it is `sysadmin`:

  ```powershell
  sqlcmd -S lpc:. -E -C -Q "SELECT IS_SRVROLEMEMBER('sysadmin') AS sysadmin"
  ```

  After `install.ps1` has created the app pool (step 2), and with Marc's OK (it changes SQL Server security):

  ```powershell
  sqlcmd -S lpc:. -E -C -I -b -Q "IF DB_ID(N'MealPlanner') IS NULL CREATE DATABASE [MealPlanner]; IF SUSER_ID(N'IIS APPPOOL\MealPlanner') IS NULL CREATE LOGIN [IIS APPPOOL\MealPlanner] FROM WINDOWS WITH DEFAULT_DATABASE = [MealPlanner];"
  sqlcmd -S lpc:. -E -C -I -b -d MealPlanner -Q "IF USER_ID(N'IIS APPPOOL\MealPlanner') IS NULL CREATE USER [IIS APPPOOL\MealPlanner] FOR LOGIN [IIS APPPOOL\MealPlanner]; ALTER ROLE db_owner ADD MEMBER [IIS APPPOOL\MealPlanner];"
  ```

  The login gets no server roles. (Use `sqlcmd -I` for any statement that writes; without it SQL Server refuses writes
  to tables with filtered indexes.)
- If `Microsoft.AspNetCore.App 10.x` is missing: Marc installs the **ASP.NET Core 10 Hosting Bundle** from
  https://dotnet.microsoft.com/download/dotnet/10.0, then runs `iisreset`.
  Visual Studio also installs that runtime (and an older ASP.NET Core Module) without the bundle, so check the bundle
  itself: *Settings -> Apps* must list **Microsoft .NET 10.x - Windows Server Hosting** (`install.ps1` checks this too).

## 2. Install (Marc runs this in an elevated PowerShell)

```powershell
cd <this folder>
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

(`-ExecutionPolicy Bypass` applies to this one call only; Windows PowerShell refuses to run scripts on a machine whose
policy is still *Restricted*. The same goes for `update.ps1`.)

The app pool runs as `IIS AppPool\MealPlanner`. That account is what SQL Server sees, so Windows login works without a
password anywhere. To run it as a specific Windows account instead, pass `-AppPoolCredential (Get-Credential)`; a
Microsoft account needs its account password there, not the PIN. The script can be run again safely.

What it sets, and why:
- App pool *No Managed Code*, **AlwaysRunning**, idle time-out **0**, no periodic recycling, site **Preload Enabled**,
  IIS feature **Application Initialization**. Hangfire runs inside the app; without these IIS stops it when nobody
  uses the app and the 06:00 offer fetch does not run.
- Site on port **8080**, firewall open for the *private* network only (LAN and WireGuard).

## 3. Settings

Edit `C:\inetpub\MealPlanner\appsettings.Production.json` (it is never overwritten by updates):
- `ConnectionStrings:mealplanner`: the Data Source from step 1.
- `Users`: Marc's and Pia's e-mail and password. Marc types the passwords. Rules: at least 8 characters with upper case,
  lower case, a digit and a symbol. Users are created on the first start only; afterwards the passwords can be removed
  from the file (changing them here later has no effect).

Then:

```powershell
Restart-WebAppPool MealPlanner
```

## 4. Check it works

1. Open http://localhost:8080 - the login page "Log ind" should show. Log in as Marc.
2. Database: `sqlcmd -S lpc:. -E -C -d MealPlanner -Q "SELECT MigrationId FROM __EFMigrationsHistory"` shows 3 migrations,
   and `SELECT COUNT(*) FROM Categories` is 12.
3. Offers: open http://localhost:8080/hangfire -> *Recurring jobs* -> tick `fetch-offers-daily` -> *Trigger now*.
   After a minute, *Succeeded* shows and `SELECT COUNT(*) FROM Offers` is about 2,000-2,500. The app's *Tilbud* tab shows them.
4. Hangfire -> *Servers* shows one server. It must stay there after an hour without visitors (that proves AlwaysRunning works).

If something fails:

| Symptom | Cause |
| --- | --- |
| HTTP 500.19 | Hosting Bundle missing, or installed without `iisreset` afterwards. |
| HTTP 500.30 | The app could not start. Set `stdoutLogEnabled="true"` in `C:\inetpub\MealPlanner\web.config`, recycle, read `logs\stdout_*.log`; set it back afterwards. Also check *Event Viewer -> Application*. |
| HTTP 503, WAS events 5021/5057 | The app pool's Windows account or password is wrong; the pool is disabled. Fix it and `Start-WebAppPool MealPlanner`. |
| "Login failed for user ..." | The app pool account has no SQL login, or no rights to create/use the database (step 1). |
| Every request takes ~15 s | Data Source uses TCP while TCP is disabled; use `lpc:`. |
| Jobs only run when someone uses the app | App pool not AlwaysRunning, site not Preload Enabled, or Application Initialization not installed. |

## 5. Cloudflare Tunnel (Marc, in the Cloudflare dashboard)

1. Zero Trust -> Networks -> Tunnels -> *Create a tunnel* -> Cloudflared -> Windows. Run the shown command in an
   elevated PowerShell on the NUC (`cloudflared.exe service install <token>`); it runs as a Windows service.
2. *Public hostname*: the chosen hostname -> Service `HTTP` `localhost:8080`.
3. A WAF rule that allows `/mcp` only from Anthropic's IP ranges (https://docs.anthropic.com/en/api/ip-addresses).
   The API key is still required on top.

The Hangfire dashboard answers 404 to anything that came through the tunnel; use it on the LAN or via WireGuard
(`http://<nuc>:8080/hangfire`).

## 6. Phones

Open `https://<hostname>` on each phone and log in. iPhone: Safari -> Share -> *Føj til hjemmeskærm*.
Android: Chrome -> menu -> *Installer app*. (Installing needs HTTPS, so it does not work on `http://<nuc>:8080`.)

## 7. Claude connector (each person, own key)

Log in on `https://<hostname>`, then create a key from the browser's developer console on that page:

```js
await (await fetch('/api/api-keys', { method: 'POST', headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ name: 'Claude' }) })).json()
```

Copy `key` (shown only once). In Claude (private account, not Team): Settings -> Connectors -> *Add custom connector*,
URL `https://<hostname>/mcp`, and under *Request headers* `X-Api-Key: <the key>`. Revoke with
`fetch('/api/api-keys/<id>', { method: 'DELETE' })`.

## Updating to a new version

From the repository on the NUC:

```powershell
cd C:\src\MealPlanner
git pull
.\deploy\build-package.ps1
.\artifacts\MealPlanner-deploy\update.ps1     # elevated PowerShell
```

(Or build the package on the dev machine and copy it over.) Migrations run when the app starts; settings are kept.
