# StreamEmber build script for the SHVDN fork (GTAVScriptHookRuntime).
# Builds ScriptHookVDotNet.sln (x64) and collects the runtime files into builds\<version>\.
param(
    [string]$Version = '3.7.0.192',
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) { throw 'vswhere.exe not found. Install Visual Studio 2022 or later.' }
$msbuild = & $vswhere -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\amd64\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild.exe not found.' }
Write-Host "MSBuild: $msbuild"

& $msbuild ScriptHookVDotNet.sln -restore -m -nologo -v:minimal `
    "-p:Configuration=$Configuration" '-p:Platform=x64' "-p:SHVDN_VERSION=$Version"
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }

$bin = Join-Path $PSScriptRoot "bin\$Configuration"
$out = Join-Path $PSScriptRoot "builds\$Version"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$files = @('ScriptHookVDotNet.asi', 'ScriptHookVDotNet2.dll', 'ScriptHookVDotNet3.dll',
           'ScriptHookVDotNet.pdb', 'ScriptHookVDotNet2.pdb', 'ScriptHookVDotNet3.pdb',
           'ScriptHookVDotNet2.xml', 'ScriptHookVDotNet3.xml')
foreach ($f in $files) {
    $p = Join-Path $bin $f
    if (Test-Path $p) { Copy-Item $p $out -Force } else { Write-Warning "Missing: $f" }
}
Copy-Item (Join-Path $PSScriptRoot 'ScriptHookVDotNet.ini') $out -Force
Write-Host "Done: $out"
