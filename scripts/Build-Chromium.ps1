#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,
    [string]$PackagePath,
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Require-Tool([string]$Name) {
    $tool = Get-Command $Name -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $tool) { throw "Falta $Name en PATH. Prepara depot_tools siguiendo README_CHROMIUM.md." }
    return $tool.Source
}

function Is-Within([string]$Path, [string]$Root) {
    $full = [IO.Path]::GetFullPath($Path)
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    return $full.Equals($base, [StringComparison]::OrdinalIgnoreCase) -or
        $full.StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoReparse([string]$Path, [string]$Root) {
    $cursor = Get-Item -LiteralPath $Path -Force
    while ($cursor -and (Is-Within $cursor.FullName $Root)) {
        if (($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Se rechazo un enlace o reparse point del paquete: $($cursor.FullName)"
        }
        if ($cursor.FullName.Equals($Root, [StringComparison]::OrdinalIgnoreCase)) { break }
        if ($cursor.PSIsContainer) { $cursor = $cursor.Parent }
        else { $cursor = $cursor.Directory }
    }
}

if ($env:OS -ne 'Windows_NT') { throw 'Este script compila Chromium para Windows x64.' }
$src = (Resolve-Path -LiteralPath $SourcePath).Path.TrimEnd('\', '/')
if ($src -match '\s') { throw 'El checkout de Chromium requiere una ruta sin espacios, por ejemplo C:\src\chromium\src.' }
$brandingFile = Join-Path $src 'chrome\app\theme\chromium\BRANDING'
$prefsFile = Join-Path $src 'components\performance_manager\user_tuning\prefs.cc'
foreach ($path in @((Join-Path $src '.gn'), $brandingFile, $prefsFile)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "No parece un checkout Chromium completo: falta $path" }
}
$branding = [IO.File]::ReadAllText($brandingFile)
$prefs = [IO.File]::ReadAllText($prefsFile)
foreach ($key in @('PRODUCT_FULLNAME', 'PRODUCT_SHORTNAME', 'PRODUCT_INSTALLER_FULLNAME', 'PRODUCT_INSTALLER_SHORTNAME')) {
    $expected = if ($key -like '*INSTALLER*') { 'ChromeTawop Installer' } else { 'ChromeTawop' }
    if ([regex]::Matches($branding, "(?m)^$key=$([regex]::Escape($expected))\r?$").Count -ne 1) {
        throw 'Aplica primero el parche de fuentes con ChromeTawop.exe.'
    }
}
if ([regex]::Matches($prefs, 'kMemorySaverModeState\s*,\s*static_cast<int>\(MemorySaverModeState::kEnabled\)').Count -ne 1 -or
    [regex]::Matches($prefs, 'kMemorySaverModeAggressiveness\s*,\s*static_cast<int>\(MemorySaverModeAggressiveness::kAggressive\)').Count -ne 1) {
    throw 'El parche de ahorro de memoria esta ausente o no coincide con Tawop v3.'
}

$git = Require-Tool 'git'
$gitRootOutput = @(& $git -C $src rev-parse --show-toplevel)
if ($LASTEXITCODE -ne 0 -or $gitRootOutput.Count -ne 1) { throw 'No se pudo verificar la raiz Git de Chromium.' }
$gitRoot = [IO.Path]::GetFullPath($gitRootOutput[0]).TrimEnd('\', '/')
if (-not $gitRoot.Equals($src, [StringComparison]::OrdinalIgnoreCase)) { throw 'SourcePath debe ser la raiz Git src de Chromium.' }
$revisionOutput = @(& $git -C $src rev-parse HEAD)
if ($LASTEXITCODE -ne 0 -or $revisionOutput.Count -ne 1 -or $revisionOutput[0] -notmatch '^[a-fA-F0-9]{40}$') {
    throw 'No se pudo obtener una revision Git valida.'
}
$revision = $revisionOutput[0]
$gn = Require-Tool 'gn'
$autoninja = Require-Tool 'autoninja'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) { throw 'Falta Visual Studio 2026 con herramientas C++ (vswhere no encontrado).' }
$vsPaths = @(& $vswhere -products '*' -version '[18.0,19.0)' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 Microsoft.VisualStudio.Component.VC.ATLMFC -property installationPath)
if ($LASTEXITCODE -ne 0 -or $vsPaths.Count -eq 0) { throw 'Se requiere Visual Studio 2026 >=18.0 con C++ x64 y ATL/MFC.' }
$sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10'
$sdkInclude = Join-Path $sdkRoot 'Include\10.0.28000.2270\um\Windows.h'
if (-not (Test-Path -LiteralPath $sdkInclude -PathType Leaf)) { throw 'Falta Windows 11 SDK 10.0.28000.2270, requerido por el baseline inspeccionado.' }
$debugHelp = Join-Path $sdkRoot 'Debuggers\x64\dbghelp.dll'
if (-not (Test-Path -LiteralPath $debugHelp -PathType Leaf)) { throw 'Faltan SDK Debugging Tools x64.' }
$debugVersion = (Get-Item -LiteralPath $debugHelp).VersionInfo
$debugNumeric = [version]::new($debugVersion.FileMajorPart, $debugVersion.FileMinorPart, $debugVersion.FileBuildPart, $debugVersion.FilePrivatePart)
if ($debugNumeric -lt [version]'10.0.26100.3323') { throw 'Se requieren SDK Debugging Tools >=10.0.26100.3323.' }

$out = Join-Path $src 'out\TawopBrowser'
$argsText = @'
is_debug = false
is_component_build = false
is_official_build = false
is_chrome_branded = false
target_cpu = "x64"
symbol_level = 0
blink_symbol_level = 0
v8_symbol_level = 0
use_remoteexec = false
'@
$argsText = $argsText.Trim() + "`n"
$argsFile = Join-Path $out 'args.gn'
if (Test-Path -LiteralPath $argsFile) {
    if ([IO.File]::ReadAllText($argsFile).Replace("`r`n", "`n") -ne $argsText) {
        throw 'out\TawopBrowser\args.gn contiene otra configuracion. Se conserva; usa otro checkout o resuelve esa diferencia manualmente.'
    }
}
if (-not $PackagePath) {
    $PackagePath = Join-Path (Split-Path -Parent $src) ('ChromeTawop-native-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$package = [IO.Path]::GetFullPath($PackagePath)
if ((Is-Within $package $src) -or (Is-Within $src $package)) { throw 'El paquete debe estar fuera del checkout y no puede ser su directorio padre.' }
if (Test-Path -LiteralPath $package) { throw 'PackagePath ya existe. El script no sobrescribe paquetes.' }

[PSCustomObject]@{
    SourcePath = $src
    SourceRevision = $revision
    BuildOutput = $out
    PackagePath = $package
    VisualStudio = $vsPaths[0]
    SDK = '10.0.28000.2270'
    ToolchainReady = $true
    NativeBrowserBuilt = $false
} | ConvertTo-Json
if ($CheckOnly) { return }

$previousWinToolchain = $env:DEPOT_TOOLS_WIN_TOOLCHAIN
$previousUpdate = $env:DEPOT_TOOLS_UPDATE
try {
    $env:DEPOT_TOOLS_WIN_TOOLCHAIN = '0'
    $env:DEPOT_TOOLS_UPDATE = '0'
    if (-not (Test-Path -LiteralPath $out)) { [void][IO.Directory]::CreateDirectory($out) }
    Assert-NoReparse $out $src
    if (-not (Test-Path -LiteralPath $argsFile)) { [IO.File]::WriteAllText($argsFile, $argsText, [Text.UTF8Encoding]::new($false)) }
    Push-Location -LiteralPath $src
    try {
        & $gn gen 'out\TawopBrowser'
        if ($LASTEXITCODE -ne 0) { throw "gn gen fallo con codigo $LASTEXITCODE." }
        & $autoninja -C 'out\TawopBrowser' chrome
        if ($LASTEXITCODE -ne 0) { throw "La compilacion de Chromium fallo con codigo $LASTEXITCODE." }
        $runtimeDeps = @(& $gn desc 'out\TawopBrowser' '//chrome:chrome' runtime_deps)
        if ($LASTEXITCODE -ne 0 -or $runtimeDeps.Count -eq 0) { throw 'GN no pudo calcular las dependencias de ejecucion. El build se conserva sin empaquetar.' }
    }
    finally { Pop-Location }

    $exe = Join-Path $out 'chrome.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'El compilador termino sin generar chrome.exe.' }
    $files = @{}
    foreach ($relative in $runtimeDeps) {
        $relative = $relative.Trim()
        if (-not $relative) { continue }
        if ([IO.Path]::IsPathRooted($relative)) { throw "GN devolvio una ruta absoluta inesperada: $relative" }
        $itemPath = [IO.Path]::GetFullPath((Join-Path $out $relative))
        if (-not (Is-Within $itemPath $out)) { throw "GN requiere un archivo externo al build: $relative. Se conserva el build; revisa el empaquetado." }
        if (-not (Test-Path -LiteralPath $itemPath)) { throw "Falta una dependencia generada: $relative" }
        Assert-NoReparse $itemPath $out
        $item = Get-Item -LiteralPath $itemPath -Force
        if ($item.PSIsContainer) {
            foreach ($child in @(Get-ChildItem -LiteralPath $itemPath -Recurse -Force)) {
                Assert-NoReparse $child.FullName $out
                if (-not $child.PSIsContainer) { $files[$child.FullName] = $true }
            }
        } else { $files[$itemPath] = $true }
    }
    $files[$exe] = $true

    [void][IO.Directory]::CreateDirectory($package)
    foreach ($file in @($files.Keys | Sort-Object)) {
        $relative = $file.Substring($out.TrimEnd('\').Length + 1)
        $target = Join-Path $package $relative
        if (-not (Is-Within $target $package)) { throw 'Se rechazo una ruta de destino inesperada.' }
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $target))
        Copy-Item -LiteralPath $file -Destination $target
    }
    Copy-Item -LiteralPath (Join-Path $src 'LICENSE') -Destination (Join-Path $package 'LICENSE.chromium')
    [IO.File]::WriteAllLines((Join-Path $package 'runtime-deps.txt'), [string[]]$runtimeDeps, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $package 'Tawop-build.json'), (@{
        project = 'ChromeTawop'
        source_revision = $revision
        branding_sha256 = (Get-FileHash -LiteralPath $brandingFile -Algorithm SHA256).Hash
        defaults_sha256 = (Get-FileHash -LiteralPath $prefsFile -Algorithm SHA256).Hash
        browser_executable = 'chrome.exe'
        native_build_completed = $true
        runtime_smoke_test_passed = $false
        memory_target_verified = $false
        note = 'Build nativo de Chromium modificado. Validar inicio, navegacion, avisos de terceros y rendimiento antes de distribuir.'
    } | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    Write-Host "Build nativo generado: $exe"
    Write-Host "Candidato portable: $package"
    Write-Host 'El ejecutable interno conserva chrome.exe. El inicio del navegador y el objetivo de 2 GB requieren pruebas.'
}
finally {
    $env:DEPOT_TOOLS_WIN_TOOLCHAIN = $previousWinToolchain
    $env:DEPOT_TOOLS_UPDATE = $previousUpdate
}
