<#
    Compila QuickStash y arma el zip para Thunderstore.

    Uso:
        .\build.ps1
        .\build.ps1 -ValheimManaged "D:\Steam\steamapps\common\Valheim\valheim_Data\Managed"
        .\build.ps1 -ValheimPlugins "D:\Steam\steamapps\common\Valheim\BepInEx\plugins"

    -ValheimManaged  : carpeta Managed del juego. Por defecto usa ..\refs\Managed.
    -ValheimPlugins  : si se pasa, ademas copia el DLL ahi para probar al toque.
    -NoZip           : solo compila, sin armar el paquete.
#>
[CmdletBinding()]
param(
    [string]$ValheimManaged,
    [string]$ValheimPlugins,
    [switch]$NoZip
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'src\QuickStash.csproj'
$package = Join-Path $root 'package'

$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

$args = @('build', $project, '-c', 'Release', '-v', 'minimal')
if ($ValheimManaged) { $args += "-p:ValheimManaged=$ValheimManaged" }
if ($ValheimPlugins) { $args += "-p:ValheimPlugins=$ValheimPlugins" }

& $dotnet $args
if ($LASTEXITCODE -ne 0) { throw "La compilacion fallo con codigo $LASTEXITCODE" }

if ($NoZip) { return }

$manifest = Get-Content (Join-Path $package 'manifest.json') -Raw | ConvertFrom-Json
$version = $manifest.version_number
$zip = Join-Path $root "QuickStash-$version.zip"

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -CompressionLevel Optimal

Write-Output "Paquete listo: $zip"
