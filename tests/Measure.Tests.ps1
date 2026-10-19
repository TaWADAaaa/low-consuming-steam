#requires -Version 5.1
[CmdletBinding()]
param([switch]$SkipIntegration)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
. (Join-Path $PSScriptRoot '..\scripts\Measure-AppBudget.ps1')

function Assert-Equal {
    param($Actual, $Expected, [string]$Because)
    if ($Actual -ne $Expected) { throw "$Because; expected '$Expected', received '$Actual'." }
}

foreach ($name in @('chrome', 'CHROME')) { Assert-Equal (Get-AppBudgetGroup $name) 'Chrome' 'Chrome grouping' }
foreach ($name in @('Discord', 'DiscordCanary', 'DiscordPTB', 'discorddevelopment')) { Assert-Equal (Get-AppBudgetGroup $name) 'Discord' 'Discord grouping' }
foreach ($name in @('steam', 'steamwebhelper', 'steamservice', 'GameOverlayUI')) { Assert-Equal (Get-AppBudgetGroup $name) 'Steam' 'Steam grouping' }
foreach ($name in @('msedge', 'chrome_fake', 'DiscordHelper', 'game', 'steam-game')) { Assert-Equal (Get-AppBudgetGroup $name) $null 'Unrelated processes must be excluded' }

Assert-Equal (New-AppBudgetVerdict 1500 1536 $true $true) 'within_observed_working_set_budget' 'Complete observed budget'
Assert-Equal (New-AppBudgetVerdict 1536 1536 $true $true) 'within_observed_working_set_budget' 'Inclusive budget boundary'
Assert-Equal (New-AppBudgetVerdict 1600 1536 $false $false) 'over_observed_working_set_budget' 'Known reads alone can demonstrate over-budget use'
Assert-Equal (New-AppBudgetVerdict 100 1536 $false $true) 'incomplete_observation' 'Missing applications cannot pass'
Assert-Equal (New-AppBudgetVerdict 100 1536 $true $false) 'incomplete_observation' 'Unreadable memory cannot pass'

Initialize-AppBudgetNative
$selfMemory = [AppBudget.NativeMemory]::Read($PID)
Assert-Equal $selfMemory.Success $true 'Native memory query of collector process'
if ($selfMemory.WorkingSetBytes -le 0 -or $selfMemory.PrivateCommitBytes -le 0) { throw 'Native memory counters must be positive for this process.' }
$badProcessMemory = [AppBudget.NativeMemory]::Read(-1)
Assert-Equal $badProcessMemory.Success $false 'Invalid PID must not return successful counters'

if (-not $SkipIntegration) {
    $testOutput = Join-Path ([IO.Path]::GetTempPath()) ('app-budget-test-' + [guid]::NewGuid().ToString('N'))
    $measurement = Invoke-AppBudgetMeasurement -DurationSeconds 1 -IntervalSeconds 0.25 -BudgetMiB 1536 -ReserveMiB 512 -TargetAvailableMiB 2048 -Scenario 'automated-test' -OutputDirectory $testOutput
    $summary = Get-Content -LiteralPath $measurement.JsonPath -Raw | ConvertFrom-Json
    $samples = @(Import-Csv -LiteralPath $measurement.CsvPath)
    Assert-Equal $summary.SchemaVersion 1 'Schema version'
    Assert-Equal $summary.CollectorVersion '0.1.0' 'Collector version'
    Assert-Equal $summary.Scenario 'automated-test' 'Scenario round trip'
    Assert-Equal $summary.Metrics.Count 4 'Three applications plus aggregate'
    Assert-Equal $samples.Count ($summary.Samples * 4) 'Four rows per sample'
    if ($summary.ObservedDurationSeconds -lt 1) { throw 'Measurement finished before requested duration.' }
    if ($summary.Hardware.LogicalProcessors -lt 1) { throw 'Logical CPU count missing.' }
    foreach ($index in @($samples.Sample | Select-Object -Unique)) {
        $group = @($samples | Where-Object { $_.Sample -eq $index -and $_.App -ne 'Total' })
        $total = $samples | Where-Object { $_.Sample -eq $index -and $_.App -eq 'Total' }
        $sumMiB = ($group | Measure-Object KnownWorkingSetMiB -Sum).Sum
        if ([math]::Abs($sumMiB - [double]$total.KnownWorkingSetMiB) -gt 0.00001) { throw 'Aggregate working set does not equal the application sum.' }
        if ([int]$index -eq 0) {
            Assert-Equal $total.CpuCoverage 'baseline' 'First sample is baseline'
            Assert-Equal $total.CpuPercentActualMachine '' 'Baseline must not claim a CPU measurement'
        } else {
            $expectedCpu = [double]$total.CpuCoreSeconds / [double]$total.IntervalSeconds / [double]$total.LogicalProcessors * 100
            if ([math]::Abs($expectedCpu - [double]$total.CpuPercentActualMachine) -gt 0.01) { throw 'Normalized CPU does not match CPU-time delta.' }
        }
    }
    if (-not $summary.Coverage.AllAppsObservedEverySample -and $summary.Budget.Verdict -eq 'within_observed_working_set_budget') { throw 'Collector incorrectly passed absent apps.' }
    Write-Output "Integration artifacts: $testOutput"
    $originalSnapshot = (Get-Item Function:Get-AppBudgetSnapshot).ScriptBlock
    try {
        function Get-AppBudgetSnapshot {
            foreach ($app in @('Chrome', 'Steam')) {
                [pscustomobject]@{
                    Group = $app; Identity = $app; CpuSeconds = 1.0
                    CpuReadable = $true; MemoryReadable = $true
                    WorkingSetBytes = 100MB; PrivateCommitBytes = 150MB
                }
            }
        }
        $webOutput = Join-Path ([IO.Path]::GetTempPath()) ('app-budget-web-test-' + [guid]::NewGuid().ToString('N'))
        $webMeasurement = Invoke-AppBudgetMeasurement -DurationSeconds 1 -IntervalSeconds 0.5 -BudgetMiB 1536 -ReserveMiB 512 -TargetAvailableMiB 2048 -Scenario 'synthetic-web-test' -DiscordMode Web -OutputDirectory $webOutput
        $webSummary = Get-Content -LiteralPath $webMeasurement.JsonPath -Raw | ConvertFrom-Json
        Assert-Equal $webSummary.Budget.Verdict 'within_observed_working_set_budget' 'Web scenario uses declared Chrome session'
        Assert-Equal $webSummary.Coverage.AllRequiredAppsObservedEverySample $true 'Chrome and Steam required in web scenario'
        Assert-Equal $webSummary.Coverage.AllAppsObservedEverySample $false 'Absent desktop Discord is still reported'
        Assert-Equal $webSummary.Budget.ObservedPeakWorkingSetMiB 200 'Working set must not include private commit'
        Assert-Equal $webSummary.Coverage.DesktopDiscordObserved $false 'Desktop Discord status'
        $webTotal = $webSummary.Metrics | Where-Object { $_.App -eq 'Total' }
        Assert-Equal $webTotal.PeakKnownPrivateCommitMiB 300 'Private commit reported separately'
        Assert-Equal $webTotal.AverageCpuPercentActualMachine 0 'Unchanged CPU-time means idle CPU'
        Write-Output "Synthetic web artifacts: $webOutput"
    } finally {
        Set-Item Function:Get-AppBudgetSnapshot -Value $originalSnapshot
    }
}
Write-Output 'All measurement checks passed.'
