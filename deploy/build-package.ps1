<#
.SYNOPSIS
    Builds the deployment package for the NUC: the published app (API + Angular) plus the install/update scripts
    and instructions, as a folder and a zip in artifacts/.

.DESCRIPTION
    Run it in a normal (non-elevated) PowerShell; no "nvm use" is needed first. npm is taken from PATH when it is
    Node 24 or newer, otherwise from the nvm folder for the version in web/.nvmrc. Elevated and agent shells often
    lack nvm's PATH entry, which made the build fail with "npm run build exited with code 9009".

.PARAMETER NodeVersion
    The nvm-installed Node version to fall back to. Defaults to web/.nvmrc.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\deploy\build-package.ps1
#>
param([string]$NodeVersion)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$web = Join-Path $root "web"
$out = Join-Path $root "artifacts\MealPlanner-deploy"
$zip = Join-Path $root "artifacts\MealPlanner-deploy.zip"
$minNodeMajor = 24

function Get-NodeMajor {
    if (-not (Get-Command node.exe -ErrorAction SilentlyContinue)) { return 0 }
    $version = & node.exe --version
    if ($version -match '^v(\d+)\.') { return [int]$Matches[1] }
    return 0
}

if ((Get-NodeMajor) -lt $minNodeMajor) {
    if (-not $NodeVersion) { $NodeVersion = (Get-Content (Join-Path $web ".nvmrc") -TotalCount 1).Trim().TrimStart('v') }
    $nvmRoots = @($env:NVM_HOME, (Join-Path $env:LOCALAPPDATA "nvm"), (Join-Path $env:APPDATA "nvm")) | Where-Object { $_ }
    $candidates = $nvmRoots | ForEach-Object { Join-Path $_ "v$NodeVersion" }
    $nodeDir = $candidates | Where-Object { Test-Path (Join-Path $_ "npm.cmd") } | Select-Object -First 1
    if (-not $nodeDir) {
        throw "Node $minNodeMajor+ not found. Install it with 'nvm install $NodeVersion'. Looked in: $($candidates -join ', ')"
    }
    # Only for this script and the processes it starts; the machine's PATH is left alone.
    $env:Path = "$nodeDir;$env:Path"
}
Write-Host "Node $(& node.exe --version), npm $(& npm.cmd --version)"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null

# The Angular build goes first. Built during publish, it replaced wwwroot after .NET had listed the old files,
# and publish then failed looking for the previous build's file names.
Push-Location $web
try {
    if (-not (Test-Path "node_modules")) {
        & npm.cmd ci
        if ($LASTEXITCODE -ne 0) { throw "npm ci failed" }
    }
    & npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw "Angular build failed" }
}
finally {
    Pop-Location
}

dotnet publish (Join-Path $root "src\MealPlanner.Api") -c Release -o (Join-Path $out "app") -p:SkipWebBuild=true
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
# Settings with secrets are made on the server from the template, never shipped.
Remove-Item (Join-Path $out "app\appsettings.Development.json") -ErrorAction SilentlyContinue

Copy-Item (Join-Path $PSScriptRoot "install.ps1"), (Join-Path $PSScriptRoot "update.ps1"), (Join-Path $PSScriptRoot "update.cmd"),
    (Join-Path $PSScriptRoot "appsettings.Production.template.json"), (Join-Path $PSScriptRoot "NUC-INSTALL.md") $out

if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zip
Write-Host "Package: $zip" -ForegroundColor Green
