# Installing MealPlanner on the NUC

For Claude Code (or Marc) on the NUC. MealPlanner is Marc and Pia's shared shopping list with this week's offers,
meal plan and an MCP server for Claude. It is an ASP.NET Core 10 app with an Angular PWA, hosted in **IIS** against
the **SQL Server that already runs on the NUC**, reached from the internet only through **Cloudflare Tunnel**.

This folder is a ready-built package, not the source code:

| Path | What |
| --- | --- |
| `app/` | The published app (API + Angular). No .NET SDK or Node needed on the NUC. |
| `install.ps1` | First install: IIS features, app pool, site on port 8080, firewall, settings template. |
| `update.ps1` | New versions: takes the site offline, swaps the files, keeps the settings. |
| `appsettings.Production.template.json` | Connection string and the two users; copied to the site by `install.ps1`. |

Ground rules: do not change SQL Server security or configuration without asking Marc; Marc types all passwords himself
(the Windows password for the app pool and the two users' passwords) - do not ask for them in chat.

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
- Marc's Windows account needs to be able to create the database once (the app creates it and its tables itself):

  ```powershell
  sqlcmd -S lpc:. -E -C -Q "SELECT IS_SRVROLEMEMBER('sysadmin') AS sysadmin, IS_SRVROLEMEMBER('dbcreator') AS dbcreator"
  ```

  If both are 0, ask Marc: either give his account `dbcreator`, or create an empty database `MealPlanner` that he owns.
  (Use `sqlcmd -I` for any statement that writes; without it SQL Server refuses writes to tables with filtered indexes.)
- If `Microsoft.AspNetCore.App 10.x` is missing: Marc installs the **ASP.NET Core 10 Hosting Bundle** from
  https://dotnet.microsoft.com/download/dotnet/10.0, then runs `iisreset`.

## 2. Install (Marc runs this in an elevated PowerShell)

```powershell
cd <this folder>
.\install.ps1
```

It asks for the Windows account the app runs as (default: Marc's own). That account is what SQL Server sees, so
Windows login works without a password in the connection string. The script can be run again safely.

What it sets, and why:
- App pool *No Managed Code*, **AlwaysRunning**, idle time-out **0**, no periodic recycling, site **Preload Enabled**,
  IIS feature **Application Initialization**. Hangfire runs inside the app; without these IIS stops it when nobody
  uses the app and the 06:00 offer fetch does not run.
- Site on port **8080**, firewall open for the *private* network only (LAN and WireGuard).

## 3. Settings

Edit `C:\inetpub\MealPlanner\appsettings.Production.json` (it is never overwritten by updates):
- `ConnectionStrings:mealplanner`: the Data Source from step 1.
- `Users`: Marc's and Pia's e-mail and password. Marc types the passwords. Rules: at least 10 characters with upper case,
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

On the dev machine: `.\deploy\build-package.ps1` (needs Node 24: `nvm use 24.21.0`). Copy the new package folder to the
NUC and run `.\update.ps1` in an elevated PowerShell. Migrations run when the app starts; settings are kept.
