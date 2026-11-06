<#
.SYNOPSIS
    Builds the deployment package for the NUC on the dev machine: the published app (API + Angular) plus the
    install/update scripts and instructions, as a folder and a zip in artifacts/.

.EXAMPLE
    .\deploy\build-package.ps1
#>
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root "artifacts\MealPlanner-deploy"
$zip = Join-Path $root "artifacts\MealPlanner-deploy.zip"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null

# Publishing also builds web/ into wwwroot (needs Node 24: nvm use 24.21.0).
dotnet publish (Join-Path $root "src\MealPlanner.Api") -c Release -o (Join-Path $out "app")
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
# Settings with secrets are made on the server from the template, never shipped.
Remove-Item (Join-Path $out "app\appsettings.Development.json") -ErrorAction SilentlyContinue

Copy-Item (Join-Path $PSScriptRoot "install.ps1"), (Join-Path $PSScriptRoot "update.ps1"),
    (Join-Path $PSScriptRoot "appsettings.Production.template.json"), (Join-Path $PSScriptRoot "NUC-INSTALL.md") $out

if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zip
Write-Host "Package: $zip" -ForegroundColor Green
