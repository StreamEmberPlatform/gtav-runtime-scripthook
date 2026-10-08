#Requires -Version 5.1
<#
.SYNOPSIS
    StreamEmber Runtime (GTA V): build, package and (optionally) install.

.DESCRIPTION
    1. Version: VERSION (major.minor) + commits since it changed = patch (tools/StreamEmber.Build.psm1).
    2. MSBuild ScriptHookVDotNet.sln (Release|x64): runtime .asi + scripting API. No .pdb / .xml.
    3. dist\GTAV\ = the game-folder layout:
         StreamEmber.Runtime.GTAV.asi
         StreamEmber\Runtime\StreamEmber.Scripting.GTAV.dll
         StreamEmber\Config\Runtime.ini
         StreamEmber\Licenses\StreamEmber.Runtime.GTAV\*.txt
         StreamEmber\Manifests\StreamEmber.Runtime.GTAV.json
    4. artifacts\StreamEmber.Runtime.GTAV-<version>.zip (+ .sha256)
    5. -Deploy: copies dist\GTAV into the game folder (keeps Runtime.ini, disables upstream SHVDN files).

.EXAMPLE
    .\build.ps1
.EXAMPLE
    .\build.ps1 -Deploy -GamePath "D:\EpicGames\GTAV"
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    # Explicit product version (CI passes the computed one); default: computed, with a -dev suffix
    [string]$Version = '',
    [switch]$Deploy,
    # GTA V folder (GTA5.exe). Default: GTAV_GAME_PATH environment variable
    [string]$GamePath = '',
    # Overwrite the game's Runtime.ini with the template
    [switch]$ResetConfig
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$Root = $PSScriptRoot
Import-Module (Join-Path $Root 'tools\StreamEmber.Build.psm1') -Force

$Id = 'StreamEmber.Runtime.GTAV'
$Game = 'GTAV'
# Files a previous ScriptHookVDotNet (upstream or our old fork) left in the game folder: two .NET runtimes must
# never load at the same time.
$Conflicts = @('ScriptHookVDotNet.asi')
$Preserve = @('StreamEmber/Config/Runtime.ini')

if (-not $Version) { $Version = Get-SEVersion -RepositoryRoot $Root -Kind Dev }
Write-Host "StreamEmber Runtime (GTA V) $Version" -ForegroundColor Cyan

# --- Build ------------------------------------------------------------------------------------------------------
$msbuild = Find-SEMSBuild
Write-Host "MSBuild: $msbuild"
& $msbuild (Join-Path $Root 'ScriptHookVDotNet.sln') -restore -m -nologo -v:minimal `
    "-p:Configuration=$Configuration" '-p:Platform=x64' "-p:SE_VERSION=$Version"
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }

# --- Stage (game-folder layout) ---------------------------------------------------------------------------------
$bin = Join-Path $Root "bin\$Configuration"
$stage = Join-Path $Root "dist\$Game"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
$runtimeDir = Join-Path $stage 'StreamEmber\Runtime'
$configDir = Join-Path $stage 'StreamEmber\Config'
New-Item -ItemType Directory -Force -Path $runtimeDir, $configDir | Out-Null

Copy-Item (Join-Path $bin "$Id.asi") $stage
Copy-Item (Join-Path $bin 'StreamEmber.Scripting.GTAV.dll') $runtimeDir
Copy-Item (Join-Path $Root 'package\Config\Runtime.ini') $configDir
# Licenses (zlib: SHVDN; plus its third-party notices)
$licenseDir = Join-Path $stage "StreamEmber\Licenses\$Id"
New-Item -ItemType Directory -Force -Path $licenseDir | Out-Null
Copy-Item (Join-Path $Root 'LICENSE.txt') $licenseDir
Copy-Item (Join-Path $Root 'COPYRIGHT.md') (Join-Path $licenseDir 'COPYRIGHT.txt')
Copy-Item (Join-Path $Root 'THIRD-PARTY-NOTICES.md') (Join-Path $licenseDir 'THIRD-PARTY-NOTICES.txt')

New-SEManifest -StageDirectory $stage -Id $Id -Name 'StreamEmber Runtime (GTA V)' -Version $Version -Game $Game `
    -Preserve $Preserve -Conflicts $Conflicts -RepositoryRoot $Root `
    -Requires @([ordered]@{ file = 'ScriptHookV.dll'; name = 'Script Hook V (Alexander Blade)'; url = 'http://www.dev-c.com/gtav/scripthookv/' },
                [ordered]@{ file = 'dinput8.dll'; name = 'ASI Loader (Script Hook V package)'; url = 'http://www.dev-c.com/gtav/scripthookv/' }) | Out-Null

$zip = New-SEPackage -StageDirectory $stage -OutputDirectory (Join-Path $Root 'artifacts') -Id $Id -Version $Version
Write-Host "Package: $zip" -ForegroundColor Green

# --- Install ----------------------------------------------------------------------------------------------------
if ($Deploy) {
    $gameDir = if ($GamePath) { $GamePath } else { $env:GTAV_GAME_PATH }
    if (-not $gameDir) { throw 'Game folder unknown: pass -GamePath or set GTAV_GAME_PATH.' }
    Install-SEPackage -StageDirectory $stage -GameDirectory $gameDir -GameExecutable 'GTA5.exe' -ProcessName 'GTA5' `
        -Preserve $Preserve -Conflicts $Conflicts -ResetConfig:$ResetConfig
    foreach ($need in 'ScriptHookV.dll', 'dinput8.dll') {
        if (-not (Test-Path (Join-Path $gameDir $need))) { Write-Warning "$need missing in the game folder (Script Hook V, dev-c.com)." }
    }
    Write-Host "Installed into $gameDir" -ForegroundColor Green
}
