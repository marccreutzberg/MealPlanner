#Requires -RunAsAdministrator
<#
.SYNOPSIS
    First-time install of MealPlanner in IIS on the NUC. Safe to run again; existing settings are kept.

.DESCRIPTION
    - Enables IIS with Application Initialization (needed so Hangfire's jobs run without visitors).
    - Checks that the ASP.NET Core Hosting Bundle is installed (it must be installed by hand first).
    - Creates the app pool: No Managed Code, AlwaysRunning, idle time-out 0, no periodic recycling,
      running as the given Windows account so SQL Server can be reached with Windows login.
    - Creates the site on the given port with Preload Enabled, copies the app and opens the port in the firewall
      for the private network (LAN/WireGuard). The internet only reaches the app through Cloudflare Tunnel.
    - Writes appsettings.Production.json from the template if it does not exist yet. Fill it in before starting.

.EXAMPLE
    .\install.ps1                          # prompts for the Windows account the app runs as
    .\install.ps1 -SitePath D:\Web\MealPlanner -Port 8080
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
$module = Join-Path $env:ProgramFiles "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
$runtime = Get-ChildItem (Join-Path $env:ProgramFiles "dotnet\shared\Microsoft.AspNetCore.App") -Directory -ErrorAction SilentlyContinue |
    Where-Object Name -like "10.*"
if (-not (Test-Path $module) -or -not $runtime) {
    throw "Install the ASP.NET Core 10 Hosting Bundle (https://dotnet.microsoft.com/download/dotnet/10.0), run 'iisreset', then run this script again."
}
Write-Host "   found ASP.NET Core $($runtime.Name -join ', ')"

Import-Module WebAdministration

Write-Host "3/6 App pool $Name"
if (-not $AppPoolCredential) {
    $AppPoolCredential = Get-Credential -Message "Windows account the app runs as (it needs access to SQL Server)" -UserName "$env:USERDOMAIN\$env:USERNAME"
}
$poolPath = "IIS:\AppPools\$Name"
if (-not (Test-Path $poolPath)) { New-WebAppPool -Name $Name | Out-Null }
Set-ItemProperty $poolPath -Name managedRuntimeVersion -Value ""
Set-ItemProperty $poolPath -Name startMode -Value "AlwaysRunning"
Set-ItemProperty $poolPath -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
Set-ItemProperty $poolPath -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
Set-ItemProperty $poolPath -Name processModel -Value @{
    identityType = 3   # SpecificUser
    userName     = $AppPoolCredential.UserName
    password     = $AppPoolCredential.GetNetworkCredential().Password
}

Write-Host "4/6 Files in $SitePath"
New-Item -ItemType Directory -Force $SitePath | Out-Null
# Copy the app but never overwrite the server's own settings.
robocopy $package $SitePath /E /XF appsettings.Production.json /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
New-Item -ItemType Directory -Force (Join-Path $SitePath "logs") | Out-Null
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
Write-Host "Installed. Next: fill in $settings, then 'Restart-WebAppPool $Name' and open http://localhost:$Port" -ForegroundColor Green
