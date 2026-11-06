#Requires -RunAsAdministrator
<#
.SYNOPSIS
    First-time install of MealPlanner in IIS on the NUC. Safe to run again; existing settings are kept.

.DESCRIPTION
    - Enables IIS with Application Initialization (needed so Hangfire's jobs run without visitors).
    - Checks that the ASP.NET Core Hosting Bundle is installed (it must be installed by hand first).
    - Creates the app pool: No Managed Code, AlwaysRunning, idle time-out 0, no periodic recycling.
      It runs as its own virtual account IIS AppPool\<Name> (no password), which SQL Server sees with Windows login;
      that account needs a SQL login and db_owner in the MealPlanner database (see NUC-INSTALL.md step 1).
      Pass -AppPoolCredential to run as a specific Windows account instead.
    - Creates the site on the given port with Preload Enabled, copies the app and opens the port in the firewall
      for the private network (LAN/WireGuard). The internet only reaches the app through Cloudflare Tunnel.
    - Writes appsettings.Production.json from the template if it does not exist yet. Fill it in before starting.

.EXAMPLE
    .\install.ps1                          # app pool runs as IIS AppPool\MealPlanner
    .\install.ps1 -SitePath D:\Web\MealPlanner -Port 8080
    .\install.ps1 -AppPoolCredential (Get-Credential)   # run as a specific Windows account
#>
param(
    [string]$SitePath = "C:\inetpub\MealPlanner",
    [int]$Port = 8080,
    [string]$Name = "MealPlanner",
    [pscredential]$AppPoolCredential
)

$ErrorActionPreference = "Stop"
$package = Join-Path $PSScriptRoot "app"
if (-not (Test-Path (Join-Path $package "MealPlanner.Api.dll"))) { throw "Cannot find the app in $package" }

Write-Host "1/6 IIS and Application Initialization"
foreach ($feature in "IIS-WebServerRole", "IIS-WebServer", "IIS-ApplicationInit", "IIS-ManagementConsole") {
    $state = (Get-WindowsOptionalFeature -Online -FeatureName $feature).State
    if ($state -ne "Enabled") {
        Write-Host "   enabling $feature"
        Enable-WindowsOptionalFeature -Online -FeatureName $feature -All -NoRestart | Out-Null
    }
}

Write-Host "2/6 ASP.NET Core Hosting Bundle"
# Visual Studio installs an ASP.NET Core 10 runtime and an older ASP.NET Core Module without the Hosting Bundle,
# so the module file and the runtime folder alone do not prove the bundle is there. Look for the bundle itself.
$uninstallKeys = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
$bundle = Get-ItemProperty $uninstallKeys -ErrorAction SilentlyContinue |
    Where-Object { $_.DisplayName -like "Microsoft .NET 10.* - Windows Server Hosting" }
$module = Join-Path $env:ProgramFiles "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
$runtime = Get-ChildItem (Join-Path $env:ProgramFiles "dotnet\shared\Microsoft.AspNetCore.App") -Directory -ErrorAction SilentlyContinue |
    Where-Object Name -like "10.*"
if (-not $bundle -or -not (Test-Path $module) -or -not $runtime) {
    throw "Install the ASP.NET Core 10 Hosting Bundle (https://dotnet.microsoft.com/download/dotnet/10.0), run 'iisreset', then run this script again."
}
Write-Host "   found $(@($bundle.DisplayName) -join ', '); ASP.NET Core Module $((Get-Item $module).VersionInfo.FileVersion)"

Import-Module WebAdministration

Write-Host "3/6 App pool $Name"
$poolPath = "IIS:\AppPools\$Name"
if (-not (Test-Path $poolPath)) { New-WebAppPool -Name $Name | Out-Null }
Set-ItemProperty $poolPath -Name managedRuntimeVersion -Value ""
Set-ItemProperty $poolPath -Name startMode -Value "AlwaysRunning"
Set-ItemProperty $poolPath -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
Set-ItemProperty $poolPath -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
if ($AppPoolCredential) {
    $poolAccount = $AppPoolCredential.UserName
    Set-ItemProperty $poolPath -Name processModel -Value @{
        identityType = 3   # SpecificUser
        userName     = $poolAccount
        password     = $AppPoolCredential.GetNetworkCredential().Password
    }
}
else {
    # The pool's own virtual account: no password to store or expire, and no rights beyond what is granted to it.
    $poolAccount = "IIS AppPool\$Name"
    Set-ItemProperty $poolPath -Name processModel -Value @{ identityType = 4; userName = ""; password = "" }   # ApplicationPoolIdentity
}
Write-Host "   runs as $poolAccount"

Write-Host "4/6 Files in $SitePath"
New-Item -ItemType Directory -Force $SitePath | Out-Null
# Copy the app but never overwrite the server's own settings.
robocopy $package $SitePath /E /XF appsettings.Production.json /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
$logs = Join-Path $SitePath "logs"
New-Item -ItemType Directory -Force $logs | Out-Null
# The site itself is readable for all users; only the stdout log folder needs write access for the pool.
icacls $logs /grant "${poolAccount}:(OI)(CI)M" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Could not give $poolAccount write access to $logs" }
$settings = Join-Path $SitePath "appsettings.Production.json"
if (-not (Test-Path $settings)) {
    Copy-Item (Join-Path $PSScriptRoot "appsettings.Production.template.json") $settings
    Write-Host "   wrote $settings from the template - FILL IT IN before the first start" -ForegroundColor Yellow
}

Write-Host "5/6 Site $Name on port $Port"
$sitePathIis = "IIS:\Sites\$Name"
if (-not (Test-Path $sitePathIis)) {
    New-Website -Name $Name -PhysicalPath $SitePath -ApplicationPool $Name -Port $Port | Out-Null
}
Set-ItemProperty $sitePathIis -Name applicationDefaults.preloadEnabled -Value $true
Set-ItemProperty $sitePathIis -Name physicalPath -Value $SitePath

Write-Host "6/6 Firewall: port $Port for the private network"
if (-not (Get-NetFirewallRule -DisplayName "$Name HTTP $Port" -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName "$Name HTTP $Port" -Direction Inbound -Protocol TCP -LocalPort $Port -Profile Private -Action Allow | Out-Null
}

Write-Host ""
Write-Host "Installed. Next: make sure $poolAccount has a SQL login with db_owner in the database (NUC-INSTALL.md step 1)," -ForegroundColor Green
Write-Host "fill in $settings, then 'Restart-WebAppPool $Name' and open http://localhost:$Port" -ForegroundColor Green
