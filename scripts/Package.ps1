#requires -Version 5.1
[CmdletBinding()]
param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$packageRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$product = 'Steam'
$repository = 'low-consuming-steam'
$executable = 'SteamTawop.exe'

function Assert-PlainPath([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if (([IO.File]::Exists($current) -or [IO.Directory]::Exists($current)) -and
            (([IO.File]::GetAttributes($current) -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            throw 'El paquete no admite enlaces ni reparse points.'
        }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}

Assert-PlainPath $packageRoot
if (-not $OutputPath) {
    $OutputPath = Join-Path (Split-Path $packageRoot -Parent) ($repository + '-v0.3.0-experimental.zip')
}
$output = [IO.Path]::GetFullPath($OutputPath)
Assert-PlainPath $output
if (Test-Path -LiteralPath $output) {
    throw 'El ZIP ya existe. Elegí otra ruta para conservar la descarga anterior.'
}
$manifestPath = Join-Path $packageRoot 'BUILD-MANIFEST.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'Primero ejecutá COMPILAR.cmd o scripts/Build.ps1 con el producto de este repositorio.'
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.Version -ne '0.3.0' -or $manifest.SelectedProduct -ne $product -or
    @($manifest.Executables).Count -ne 1 -or $manifest.Executables[0].Name -ne $executable) {
    throw 'El manifiesto debe contener únicamente el ejecutable de este proyecto.'
}
if ($manifest.AutomatedTestsPassed -ne $true -or $manifest.ClientEngineBuildsIncluded -ne $false -or
    $manifest.MemoryTargetVerified -ne $false) {
    throw 'La descarga requiere pruebas aprobadas y un estado de rendimiento experimental coherente.'
}
$actualHash = (Get-FileHash -LiteralPath (Join-Path $packageRoot $executable) -Algorithm SHA256).Hash
if ($actualHash -ne $manifest.Executables[0].SHA256) {
    throw 'El ejecutable cambió después de la compilación.'
}
$relativeFiles = @(
    '.gitattributes'
    '.github/FUNDING.yml'
    '.github/workflows/build.yml'
    '.gitignore'
    'assets/discord/preload.js'
    'assets/discord/tawop.css'
    'assets/shared/tawop.ico'
    'assets/shared/tawop.svg'
    'assets/steam/tawop.css'
    'BUILD-MANIFEST.json'
    'COMPILAR.cmd'
    'CONTRIBUTING.md'
    'docs/ARQUITECTURA.md'
    'docs/RENDIMIENTO.md'
    'docs/USER_GUIDE.en.md'
    'docs/VALIDACION.md'
    'DONATIONS.md'
    'LICENSE'
    'PRIVACY.md'
    'PROJECT.json'
    'README.en.md'
    'README.es.md'
    'README.md'
    'README_CHROMIUM.md'
    'scripts/Build.ps1'
    'scripts/Build-Chromium.ps1'
    'scripts/Measure-AppBudget.ps1'
    'scripts/Package.ps1'
    'src/app.manifest'
    'src/AsarArchive.cs'
    'src/AssetStore.cs'
    'src/ChromeMod.cs'
    'src/DiscordMod.cs'
    'src/ModCore.cs'
    'src/Program.cs'
    'src/SteamMod.cs'
    'SteamTawop.cmd'
    'SteamTawop.exe'
    'tests/Asar.Tests.cs'
    'tests/AssetStore.Tests.cs'
    'tests/Chrome.Tests.cs'
    'tests/Core.Tests.cs'
    'tests/GuiSmoke.cs'
    'tests/Measure.Tests.ps1'
    'tests/Preload.Tests.js'
    'tests/Review.Tests.cs'
    'tests/Runner.cs'
    'tests/Steam.Tests.cs'
    'THIRD_PARTY_NOTICES.md'
)
if (Test-Path -LiteralPath (Join-Path $packageRoot 'PRIVACY-AUDIT.md') -PathType Leaf) {
    $relativeFiles += 'PRIVACY-AUDIT.md'
}
$files = New-Object 'System.Collections.Generic.List[string]'
foreach ($relative in $relativeFiles) {
    if ($relative -match '(^|/)(artifacts|\.git|\.tawop|user-data|node_modules|results|backups|profiles|reports|releases)(/|$)' -or
        [IO.Path]::IsPathRooted($relative) -or $relative -match '(^|/)\.\.(/|$)') {
        throw 'La lista de publicación contiene una ruta no permitida.'
    }
    $path = [IO.Path]::GetFullPath((Join-Path $packageRoot $relative))
    if (-not $path.StartsWith($packageRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Un archivo está fuera del proyecto.'
    }
    Assert-PlainPath $path
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Falta el archivo publicable $relative"
    }
    $files.Add($path)
}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$outputParent = Split-Path $output -Parent
Assert-PlainPath $outputParent
New-Item -Path $outputParent -ItemType Directory -Force | Out-Null
Assert-PlainPath $outputParent
$stream = New-Object IO.FileStream($output, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
$archive = $null
try {
    $archive = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    foreach ($file in ($files | Sort-Object -Unique)) {
        $relative = $file.Substring($packageRoot.TrimEnd('\').Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $file, ($repository + '/' + $relative), [IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally {
    if ($archive) {
        $archive.Dispose()
    }
    $stream.Dispose()
}
[pscustomobject]@{
    Repository = $repository
    Product = $product
    Zip = $output
    Files = $files.Count
    Bytes = (Get-Item -LiteralPath $output).Length
    SHA256 = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
    Published = $false
} | ConvertTo-Json
