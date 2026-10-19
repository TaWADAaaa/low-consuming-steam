#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Discord', 'Steam', 'Chrome', 'All')]
    [string]$Product = 'Steam',
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
$packageRoot = Split-Path $PSScriptRoot -Parent
$products = @('Discord', 'Steam', 'Chrome' | Where-Object { $Product -eq 'All' -or $_ -eq $Product })
$selectedProduct = if ($products.Count -eq 3) { 'All' } else { $products[0] }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler is missing. Use Windows 10/11 with .NET Framework 4.8.' }
$artifacts = Join-Path $packageRoot 'artifacts'
New-Item -Path $artifacts -ItemType Directory -Force | Out-Null

Add-Type -AssemblyName System.Drawing
if (-not ('TawopBuildIcons' -as [type])) {
    Add-Type 'using System; using System.Runtime.InteropServices; public static class TawopBuildIcons { [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr value); }'
}
$iconPath = Join-Path $packageRoot 'assets\shared\tawop.ico'
$bitmap = New-Object System.Drawing.Bitmap(64,64)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$handle = [IntPtr]::Zero
$icon = $null
try {
    $graphics.Clear([System.Drawing.Color]::FromArgb(16,32,27))
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(121,242,180))
    try { $graphics.FillRectangle($brush,14,15,36,8); $graphics.FillRectangle($brush,27,23,9,27); $graphics.FillRectangle($brush,43,42,8,8) }
    finally { $brush.Dispose() }
    $handle = $bitmap.GetHicon(); $icon = [System.Drawing.Icon]::FromHandle($handle)
    $stream = [IO.File]::Create($iconPath)
    try { $icon.Save($stream) } finally { $stream.Dispose() }
} finally {
    if ($icon) { $icon.Dispose() }; if ($handle -ne [IntPtr]::Zero) { [TawopBuildIcons]::DestroyIcon($handle) | Out-Null }
    $graphics.Dispose(); $bitmap.Dispose()
}

$sources = @(Get-ChildItem -LiteralPath (Join-Path $packageRoot 'src') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)
$common = @('/nologo','/langversion:5','/codepage:65001','/platform:anycpu','/optimize+','/reference:System.dll','/reference:System.Core.dll','/reference:System.Web.Extensions.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll')
$resourceFiles = [ordered]@{
    'discord\tawop.css' = 'Tawop.Assets.discord.tawop.css'
    'discord\preload.js' = 'Tawop.Assets.discord.preload.js'
    'steam\tawop.css' = 'Tawop.Assets.steam.tawop.css'
}
$resources = foreach ($relativePath in $resourceFiles.Keys) {
    $assetPath = Join-Path (Join-Path $packageRoot 'assets') $relativePath
    '/resource:' + $assetPath + ',' + $resourceFiles[$relativePath]
}
foreach ($app in $products) {
    $output = Join-Path $packageRoot ($app + 'Tawop.exe')
    $arguments = $common + $resources + @('/target:winexe',('/define:' + $app.ToUpperInvariant()),('/out:' + $output),('/win32icon:' + $iconPath),('/win32manifest:' + (Join-Path $packageRoot 'src\app.manifest'))) + $sources
    & $compiler @arguments
    if ($LASTEXITCODE -ne 0) { throw "Could not compile $app" }
    Write-Output "Built: $output"
}
if (-not $SkipTests) {
    $testSources = @(Get-ChildItem -LiteralPath (Join-Path $packageRoot 'tests') -Filter '*.cs' -File | Where-Object Name -ne 'GuiSmoke.cs' | Sort-Object Name | ForEach-Object FullName)
    $coreSources = @($sources | Where-Object { [IO.Path]::GetFileName($_) -ne 'Program.cs' })
    $testExe = Join-Path $artifacts 'Tawop.Tests.exe'
    & $compiler @common @resources '/target:exe' '/main:Tawop.Tests.Runner' ('/out:' + $testExe) @coreSources @testSources
    if ($LASTEXITCODE -ne 0) { throw 'Could not compile the tests.' }
    & $testExe $packageRoot
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    $guiTest = Join-Path $artifacts 'GuiSmoke.exe'
    & $compiler @common '/target:exe' ('/out:' + $guiTest) (Join-Path $packageRoot 'tests\GuiSmoke.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Could not compile the interface test.' }
    foreach ($app in $products) {
        & $guiTest (Join-Path $packageRoot ($app + 'Tawop.exe')) (Join-Path $artifacts ($app + '-preview.png'))
        if ($LASTEXITCODE -ne 0) { throw "Interface test failed for $app" }
    }
    $node = Get-Command node -ErrorAction SilentlyContinue
    if (-not $node) { throw 'Full preload validation requires Node.js on PATH. The release executables do not require Node.' }
    & $node.Source (Join-Path $packageRoot 'tests\Preload.Tests.js') $packageRoot
    if ($LASTEXITCODE -ne 0) { throw 'Preload media and style tests failed.' }
    $measurementTest = Join-Path $packageRoot 'tests\Measure.Tests.ps1'
    if (Test-Path -LiteralPath $measurementTest) {
        $windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
        & $windowsPowerShell -NoProfile -File $measurementTest -SkipIntegration
        if ($LASTEXITCODE -ne 0) { throw 'Performance collector tests failed.' }
    }
}
$records = foreach ($app in $products) {
    $path = Join-Path $packageRoot ($app + 'Tawop.exe')
    [pscustomobject]@{Name=[IO.Path]::GetFileName($path);Bytes=(Get-Item -LiteralPath $path).Length;SHA256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash;Kind='local patch tool; not a replacement client'}
}
[pscustomobject]@{
    Version = '0.4.0'
    SelectedProduct = $selectedProduct
    BuiltUtc = [DateTime]::UtcNow.ToString('o')
    Compiler = (Get-Item -LiteralPath $compiler).VersionInfo.FileVersion
    Executables = @($records)
    ClientEngineBuildsIncluded = $false
    MemoryTargetVerified = $false
    AutomatedTestsPassed = (-not $SkipTests)
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $packageRoot 'BUILD-MANIFEST.json') -Encoding UTF8
Write-Output 'Build and checks completed. These executables are local patch tools.'
