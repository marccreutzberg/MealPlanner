#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Puts a new version of MealPlanner in place on the NUC.

.DESCRIPTION
    Takes the site offline with app_offline.htm (so IIS releases the DLLs), mirrors the new app over the old one
    without touching appsettings.Production.json or logs, and brings the site back. Database migrations run
    automatically when the app starts.

.EXAMPLE
    .\update.ps1
#>
param(
    [string]$SitePath = "C:\inetpub\MealPlanner",
    [string]$Name = "MealPlanner"
)

$ErrorActionPreference = "Stop"
$package = Join-Path $PSScriptRoot "app"
if (-not (Test-Path (Join-Path $package "MealPlanner.Api.dll"))) { throw "Cannot find the app in $package" }
if (-not (Test-Path (Join-Path $SitePath "appsettings.Production.json"))) { throw "No appsettings.Production.json in $SitePath - run install.ps1 first" }

$offline = Join-Path $SitePath "app_offline.htm"
Set-Content $offline "<!doctype html><meta charset=utf-8><title>Opdaterer</title><p>Indkøbslisten opdateres. Prøv igen om et øjeblik.</p>" -Encoding UTF8
Start-Sleep -Seconds 3   # let IIS shut the app down

try {
    robocopy $package $SitePath /MIR /XF appsettings.Production.json app_offline.htm /XD logs /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
}
finally {
    Remove-Item $offline -ErrorAction SilentlyContinue
}

Import-Module WebAdministration
Restart-WebAppPool $Name
Write-Host "Updated. The app migrates the database on start; open http://localhost:8080 to check it." -ForegroundColor Green
