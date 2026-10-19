#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateRange(1, 86400)][double]$DurationSeconds = 30,
    [ValidateRange(0.1, 3600)][double]$IntervalSeconds = 2,
    [ValidateRange(1, 1048576)][double]$BudgetMiB = 1536,
    [ValidateRange(0, 1048576)][double]$ReserveMiB = 512,
    [ValidateRange(1, 1048576)][double]$TargetAvailableMiB = 2048,
    [ValidateLength(1, 120)][string]$Scenario = 'idle',
    [ValidateSet('Desktop', 'Web')][string]$DiscordMode = 'Desktop',
    [string]$OutputDirectory = ''
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Get-AppBudgetGroup {
    param([string]$ProcessName)
    switch -Regex ($ProcessName) {
        '^chrome$' { return 'Chrome' }
        '^discord(?:canary|ptb|development)?$' { return 'Discord' }
        '^(steam|steamwebhelper|steamservice|gameoverlayui|steamerrorreporter|steamerrorreporter64)$' { return 'Steam' }
        default { return $null }
    }
}

function Initialize-AppBudgetNative {
    if ('AppBudget.NativeMemory' -as [type]) { return }
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace AppBudget {
    public struct MemoryResult {
        public bool Success;
        public ulong WorkingSetBytes;
        public ulong PrivateCommitBytes;
        public int ErrorCode;
    }
    public static class NativeMemory {
        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_MEMORY_COUNTERS_EX {
            public uint cb;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
            public UIntPtr PrivateUsage;
        }
        [DllImport("kernel32.dll", SetLastError=true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int id);
        [DllImport("kernel32.dll", SetLastError=true)]
        private static extern bool CloseHandle(IntPtr handle);
        [DllImport("psapi.dll", SetLastError=true)]
        private static extern bool GetProcessMemoryInfo(IntPtr handle,
            ref PROCESS_MEMORY_COUNTERS_EX counters, uint size);
        public static MemoryResult Read(int id) {
            MemoryResult result = new MemoryResult();
            IntPtr handle = OpenProcess(0x1000 | 0x0010, false, id);
            if (handle == IntPtr.Zero) handle = OpenProcess(0x1000, false, id);
            if (handle == IntPtr.Zero) {
                result.ErrorCode = Marshal.GetLastWin32Error();
                return result;
            }
            try {
                PROCESS_MEMORY_COUNTERS_EX counters = new PROCESS_MEMORY_COUNTERS_EX();
                counters.cb = (uint)Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS_EX));
                if (!GetProcessMemoryInfo(handle, ref counters, counters.cb)) {
                    result.ErrorCode = Marshal.GetLastWin32Error();
                    return result;
                }
                result.Success = true;
                result.WorkingSetBytes = counters.WorkingSetSize.ToUInt64();
                result.PrivateCommitBytes = counters.PrivateUsage.ToUInt64();
                return result;
            } finally { CloseHandle(handle); }
        }
    }
}
'@
}

function Get-AppBudgetSnapshot {
    Initialize-AppBudgetNative
    $result = New-Object 'System.Collections.Generic.List[object]'
    foreach ($process in @(Get-Process -ErrorAction Stop)) {
        try {
            $group = Get-AppBudgetGroup -ProcessName $process.ProcessName
            if (-not $group) { continue }
            $memory = [AppBudget.NativeMemory]::Read($process.Id)
            $cpuReadable = $false
            $cpuSeconds = $null
            $identity = $null
            try {
                $identity = '{0}:{1}' -f $process.Id, $process.StartTime.ToUniversalTime().Ticks
                $cpuSeconds = $process.TotalProcessorTime.TotalSeconds
                $cpuReadable = $true
            } catch { }
            $result.Add([pscustomobject]@{
                Group = $group
                Identity = $identity
                CpuSeconds = $cpuSeconds
                CpuReadable = $cpuReadable
                MemoryReadable = $memory.Success
                WorkingSetBytes = $memory.WorkingSetBytes
                PrivateCommitBytes = $memory.PrivateCommitBytes
            })
        } finally {
            $process.Dispose()
        }
    }
    return $result.ToArray()
}

function Get-AppBudgetHardware {
    $processors = @(Get-CimInstance -ClassName Win32_Processor)
    $system = Get-CimInstance -ClassName Win32_ComputerSystem
    $os = Get-CimInstance -ClassName Win32_OperatingSystem
    return [pscustomobject]@{
        OsCaption = $os.Caption
        OsVersion = $os.Version
        CpuModels = @($processors | ForEach-Object { $_.Name.Trim() })
        PhysicalCores = [int](($processors | Measure-Object NumberOfCores -Sum).Sum)
        LogicalProcessors = [int]$system.NumberOfLogicalProcessors
        InstalledMemoryMiB = [math]::Round($system.TotalPhysicalMemory / 1MB, 2)
        AvailableMemoryStartMiB = [math]::Round($os.FreePhysicalMemory / 1024, 2)
    }
}

function New-AppBudgetVerdict {
    param(
        [double]$PeakWorkingSetMiB,
        [double]$BudgetMiB,
        [bool]$AllAppsObservedEverySample,
        [bool]$AllMemoryReadsSucceeded
    )
    if ($PeakWorkingSetMiB -gt $BudgetMiB) { return 'over_observed_working_set_budget' }
    if (-not $AllAppsObservedEverySample -or -not $AllMemoryReadsSucceeded) {
        return 'incomplete_observation'
    }
    return 'within_observed_working_set_budget'
}

function Get-AppBudgetStats {
    param([object[]]$Rows, [string]$App)
    $selected = @($Rows | Where-Object { $_.App -eq $App })
    $cpuRows = @($selected | Where-Object { $null -ne $_.CpuPercentActualMachine })
    $cpuElapsed = 0.0
    $cpuCoreSeconds = 0.0
    $peakCpuActual = $null
    $peakCpuFour = $null
    foreach ($cpuRow in $cpuRows) {
        $cpuElapsed += $cpuRow.IntervalSeconds
        $cpuCoreSeconds += $cpuRow.CpuCoreSeconds
        if ($null -eq $peakCpuActual -or $cpuRow.CpuPercentActualMachine -gt $peakCpuActual) { $peakCpuActual = $cpuRow.CpuPercentActualMachine }
        if ($null -eq $peakCpuFour -or $cpuRow.CpuPercentFourCoreEquivalent -gt $peakCpuFour) { $peakCpuFour = $cpuRow.CpuPercentFourCoreEquivalent }
    }
    $cpuAverage = $null
    $cpuAverageFour = $null
    if ($cpuElapsed -gt 0) {
        $cpuAverage = [math]::Round(($cpuCoreSeconds / $cpuElapsed / $selected[0].LogicalProcessors) * 100, 3)
        $cpuAverageFour = [math]::Round(($cpuCoreSeconds / $cpuElapsed / 4) * 100, 3)
    }
    [pscustomobject]@{
        App = $App
        Samples = $selected.Count
        SamplesPresent = @($selected | Where-Object { $_.ProcessCount -gt 0 }).Count
        MemoryReadFailures = [int](($selected | Measure-Object MemoryReadFailures -Sum).Sum)
        PeakProcesses = [int](($selected | Measure-Object ProcessCount -Maximum).Maximum)
        AverageKnownWorkingSetMiB = [math]::Round(($selected | Measure-Object KnownWorkingSetMiB -Average).Average, 3)
        PeakKnownWorkingSetMiB = [math]::Round(($selected | Measure-Object KnownWorkingSetMiB -Maximum).Maximum, 3)
        AverageKnownPrivateCommitMiB = [math]::Round(($selected | Measure-Object KnownPrivateCommitMiB -Average).Average, 3)
        PeakKnownPrivateCommitMiB = [math]::Round(($selected | Measure-Object KnownPrivateCommitMiB -Maximum).Maximum, 3)
        AverageCpuPercentActualMachine = $cpuAverage
        PeakCpuPercentActualMachine = $peakCpuActual
        AverageCpuPercentFourCoreEquivalent = $cpuAverageFour
        PeakCpuPercentFourCoreEquivalent = $peakCpuFour
        CpuCompleteIntervals = @($cpuRows | Where-Object { $_.CpuCoverage -eq 'complete_between_snapshots' }).Count
        CpuPartialIntervals = @($cpuRows | Where-Object { $_.CpuCoverage -ne 'complete_between_snapshots' }).Count
    }
}

function Invoke-AppBudgetMeasurement {
    param(
        [double]$DurationSeconds, [double]$IntervalSeconds,
        [double]$BudgetMiB, [double]$ReserveMiB, [double]$TargetAvailableMiB,
        [string]$Scenario, [ValidateSet('Desktop', 'Web')][string]$DiscordMode = 'Desktop',
        [string]$OutputDirectory
    )
    if ($env:OS -ne 'Windows_NT') { throw 'This collector requires Windows.' }
    if (($BudgetMiB + $ReserveMiB) -gt $TargetAvailableMiB) {
        throw 'BudgetMiB + ReserveMiB must not exceed TargetAvailableMiB.'
    }
    $hardware = Get-AppBudgetHardware
    if ($hardware.LogicalProcessors -lt 1) { throw 'Cannot determine the logical CPU count.' }
    Initialize-AppBudgetNative
    $startUtc = [DateTime]::UtcNow
    if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        $OutputDirectory = Join-Path (Join-Path $PSScriptRoot '..\results') ($startUtc.ToString('yyyyMMddTHHmmssfffZ'))
    }
    $directory = New-Item -ItemType Directory -Path $OutputDirectory -Force
    $csvPath = Join-Path $directory.FullName 'samples.csv'
    $jsonPath = Join-Path $directory.FullName 'summary.json'
    if ((Test-Path -LiteralPath $csvPath) -or (Test-Path -LiteralPath $jsonPath)) {
        throw 'Output files already exist; choose a new OutputDirectory to preserve the previous measurement.'
    }
    $rows = New-Object 'System.Collections.Generic.List[object]'
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $previous = @{}
    $previousElapsed = $null
    $sample = 0
    do {
        $captureStart = $watch.Elapsed.TotalSeconds
        $timestamp = [DateTime]::UtcNow.ToString('o')
        $snapshot = @(Get-AppBudgetSnapshot)
        $captureMs = ($watch.Elapsed.TotalSeconds - $captureStart) * 1000
        $interval = 0.0
        if ($null -ne $previousElapsed) { $interval = $captureStart - $previousElapsed }
        $current = @{}
        foreach ($entry in $snapshot) {
            if ($entry.CpuReadable) { $current[$entry.Identity] = $entry }
        }
        $sampleRows = New-Object 'System.Collections.Generic.List[object]'
        foreach ($app in @('Chrome', 'Discord', 'Steam')) {
            $members = @($snapshot | Where-Object { $_.Group -eq $app })
            $knownMemory = @($members | Where-Object { $_.MemoryReadable })
            $workingSetBytes = 0.0
            $privateCommitBytes = 0.0
            foreach ($memoryEntry in $knownMemory) {
                $workingSetBytes += $memoryEntry.WorkingSetBytes
                $privateCommitBytes += $memoryEntry.PrivateCommitBytes
            }
            $cpuDelta = 0.0
            $matched = 0
            $untracked = 0
            foreach ($member in $members) {
                if ($member.CpuReadable -and $previous.ContainsKey($member.Identity)) {
                    $delta = $member.CpuSeconds - $previous[$member.Identity].CpuSeconds
                    if ($delta -ge 0) { $cpuDelta += $delta; $matched++ } else { $untracked++ }
                } else { $untracked++ }
            }
            $exited = @($previous.Values | Where-Object { $_.Group -eq $app -and -not $current.ContainsKey($_.Identity) }).Count
            $cpuActual = $null
            $cpuFour = $null
            $coverage = 'baseline'
            if ($interval -gt 0) {
                $cpuActual = [math]::Round($cpuDelta / $interval / $hardware.LogicalProcessors * 100, 3)
                $cpuFour = [math]::Round($cpuDelta / $interval / 4 * 100, 3)
                $coverage = 'complete_between_snapshots'
                if ($untracked -gt 0 -or $exited -gt 0) { $coverage = 'partial_lower_bound' }
            }
            $sampleRows.Add([pscustomobject][ordered]@{
                Sample = $sample
                TimestampUtc = $timestamp
                ElapsedSeconds = [math]::Round($captureStart, 6)
                IntervalSeconds = [math]::Round($interval, 6)
                Scenario = $Scenario
                App = $app
                ProcessCount = $members.Count
                MemoryMeasuredProcesses = $knownMemory.Count
                MemoryReadFailures = $members.Count - $knownMemory.Count
                KnownWorkingSetMiB = [math]::Round(($workingSetBytes / 1MB), 6)
                KnownPrivateCommitMiB = [math]::Round(($privateCommitBytes / 1MB), 6)
                CpuCoreSeconds = [math]::Round($cpuDelta, 6)
                CpuPercentActualMachine = $cpuActual
                CpuPercentFourCoreEquivalent = $cpuFour
                CpuCoverage = $coverage
                CpuMatchedProcesses = $matched
                CpuUntrackedProcesses = $untracked
                CpuExitedProcesses = $exited
                LogicalProcessors = $hardware.LogicalProcessors
                SnapshotCaptureMilliseconds = [math]::Round($captureMs, 3)
            })
        }
        $total = $sampleRows[0].PSObject.Copy()
        $total.App = 'Total'
        foreach ($field in @('ProcessCount', 'MemoryMeasuredProcesses', 'MemoryReadFailures', 'KnownWorkingSetMiB', 'KnownPrivateCommitMiB', 'CpuCoreSeconds', 'CpuMatchedProcesses', 'CpuUntrackedProcesses', 'CpuExitedProcesses')) {
            $total.$field = ($sampleRows | Measure-Object -Property $field -Sum).Sum
        }
        if ($interval -gt 0) {
            $total.CpuPercentActualMachine = [math]::Round($total.CpuCoreSeconds / $interval / $hardware.LogicalProcessors * 100, 3)
            $total.CpuPercentFourCoreEquivalent = [math]::Round($total.CpuCoreSeconds / $interval / 4 * 100, 3)
            $total.CpuCoverage = 'complete_between_snapshots'
            if (@($sampleRows | Where-Object { $_.CpuCoverage -ne 'complete_between_snapshots' }).Count -gt 0) {
                $total.CpuCoverage = 'partial_lower_bound'
            }
        }
        foreach ($row in $sampleRows) { $rows.Add($row) }
        $rows.Add($total)
        $previous = $current
        $previousElapsed = $captureStart
        $sample++
        if ($captureStart -ge $DurationSeconds) { break }
        $nextTime = [math]::Min(($sample * $IntervalSeconds), $DurationSeconds)
        $sleepMs = [math]::Max(0, ($nextTime - $watch.Elapsed.TotalSeconds) * 1000)
        if ($sleepMs -ge 1) { Start-Sleep -Milliseconds ([int][math]::Ceiling($sleepMs)) }
    } while ($true)
    $watch.Stop()
    $statistics = @('Chrome', 'Discord', 'Steam', 'Total') | ForEach-Object {
        Get-AppBudgetStats -Rows $rows.ToArray() -App $_
    }
    $requiredGroups = @('Chrome', 'Steam')
    if ($DiscordMode -eq 'Desktop') { $requiredGroups += 'Discord' }
    $allAppsEverySample = @($statistics | Where-Object { $_.App -ne 'Total' -and $_.SamplesPresent -ne $_.Samples }).Count -eq 0
    $allRequiredEverySample = @($statistics | Where-Object { $requiredGroups -contains $_.App -and $_.SamplesPresent -ne $_.Samples }).Count -eq 0
    $allMemoryReads = @($rows | Where-Object { $_.App -eq 'Total' -and $_.MemoryReadFailures -gt 0 }).Count -eq 0
    $peakWorkingSet = ($rows | Where-Object { $_.App -eq 'Total' } | Measure-Object KnownWorkingSetMiB -Maximum).Maximum
    $endOs = Get-CimInstance -ClassName Win32_OperatingSystem
    $summary = [pscustomobject][ordered]@{
        SchemaVersion = 1
        CollectorVersion = '0.1.0'
        PowerShellVersion = $PSVersionTable.PSVersion.ToString()
        CollectorIs64BitProcess = [Environment]::Is64BitProcess
        Scenario = $Scenario
        DiscordMode = $DiscordMode
        StartUtc = $startUtc.ToString('o')
        EndUtc = [DateTime]::UtcNow.ToString('o')
        RequestedDurationSeconds = $DurationSeconds
        ObservedDurationSeconds = [math]::Round($previousElapsed, 3)
        RequestedIntervalSeconds = $IntervalSeconds
        Samples = $sample
        Hardware = $hardware
        AvailableMemoryEndMiB = [math]::Round($endOs.FreePhysicalMemory / 1024, 2)
        Budget = [pscustomobject]@{
            TargetAvailableMiB = $TargetAvailableMiB
            AppsWorkingSetBudgetMiB = $BudgetMiB
            ReserveMiB = $ReserveMiB
            ObservedPeakWorkingSetMiB = $peakWorkingSet
            ObservedBudgetHeadroomMiB = [math]::Round($BudgetMiB - $peakWorkingSet, 3)
            Verdict = New-AppBudgetVerdict -PeakWorkingSetMiB $peakWorkingSet -BudgetMiB $BudgetMiB -AllAppsObservedEverySample $allRequiredEverySample -AllMemoryReadsSucceeded $allMemoryReads
        }
        Coverage = [pscustomobject]@{
            AllAppsObservedEverySample = $allAppsEverySample
            AllRequiredAppsObservedEverySample = $allRequiredEverySample
            AllMemoryReadsSucceeded = $allMemoryReads
            Applications = @('Chrome', 'Discord', 'Steam')
            RequiredProcessGroups = $requiredGroups
            DiscordWebPresence = 'Not inspected. Web mode is a user-declared scenario; Discord traffic is included in Chrome memory if that web session is open.'
            FunctionalVerification = 'Manual verification pending: login, messages, microphone, voice call, audio and game launch. Memory verdict does not certify functionality.'
            DesktopDiscordObserved = @($statistics | Where-Object { $_.App -eq 'Discord' -and $_.SamplesPresent -gt 0 }).Count -gt 0
            SteamIncludes = @('steam', 'steamwebhelper', 'steamservice', 'GameOverlayUI', 'steamerrorreporter', 'steamerrorreporter64')
            DiscordIncludes = @('Discord', 'DiscordCanary', 'DiscordPTB', 'DiscordDevelopment')
            ChromeIncludes = @('chrome')
            ExecutableIdentity = 'Process names only; no executable signature/path validation.'
        }
        Metrics = @($statistics)
        Definitions = [pscustomobject]@{
            WorkingSet = 'GetProcessMemoryInfo.WorkingSetSize: resident pages including shared pages. Aggregate may double-count shared memory; known values are lower bounds if reads fail.'
            PrivateCommit = 'GetProcessMemoryInfo.PrivateUsage: private committed bytes; not resident/private working set. Never add this metric to working set.'
            PrivateWorkingSet = 'Not measured. Private commit is not used as a substitute.'
            CpuActualMachine = '100 * sum(matched process CPU-time deltas) / elapsed wall seconds / actual logical processors.'
            CpuFourCoreEquivalent = '100 * sum(matched process CPU-time deltas) / elapsed wall seconds / 4. Arithmetic equivalent only; may exceed 100%; no hardware performance prediction.'
            CpuCoverage = 'Baseline has no delta. New, exited or unreadable processes make intervals partial lower bounds. Processes born and terminated between snapshots cannot be detected.'
            Averages = 'Memory averages are arithmetic sample means; CPU averages are weighted by observed interval duration, including partial intervals.'
            Verdict = 'Observed aggregate working set only. A within-budget result requires every required process group in every sample and all memory reads succeeding. Desktop mode requires Chrome, Discord and Steam; Web mode requires Chrome and Steam and assumes the declared Discord web session. Desktop Discord is still counted when present. No guarantee of functionality, responsiveness, paging behavior, GPU usage, game memory or CPU performance on a 4-core PC.'
            Target = 'Available RAM for apps and other tasks after Windows; this target does not include Windows itself. Reserve is a planning margin, not an allocation or enforced limit.'
        }
    }
    $rows.ToArray() | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
    return [pscustomobject]@{
        CsvPath = $csvPath
        JsonPath = $jsonPath
        Verdict = $summary.Budget.Verdict
        PeakWorkingSetMiB = $peakWorkingSet
        AllAppsObservedEverySample = $allAppsEverySample
        AllRequiredAppsObservedEverySample = $allRequiredEverySample
        AllMemoryReadsSucceeded = $allMemoryReads
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    Invoke-AppBudgetMeasurement -DurationSeconds $DurationSeconds -IntervalSeconds $IntervalSeconds -BudgetMiB $BudgetMiB -ReserveMiB $ReserveMiB -TargetAvailableMiB $TargetAvailableMiB -Scenario $Scenario -DiscordMode $DiscordMode -OutputDirectory $OutputDirectory
}
