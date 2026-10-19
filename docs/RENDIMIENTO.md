# Performance budget

**The combined 2 GiB / four-core target has not been demonstrated.** This protocol defines acceptance. The tool does not impose a hard memory limit, and a simpler style does not automatically reduce client-engine memory.

## What changed in this revision

The earlier Discord visual/preload patch was found installed during an uncontrolled desktop observation, with about 1 GiB of aggregate Discord working set. Steam was unpatched in that observation. The complete [diagnostic note](BASELINE.md) records the mixed installation state and measurement limits. It is not a before-and-after result.

The new Discord policy acts before fetching recognized decorative CDN resources, requests smaller static recognized avatar/icon/emoji forms, suppresses image animation and disables spellcheck. The image policy also makes animated images in messages still. Its preload pauses eligible muted autoplay-loop decorative videos in the foreground as well as the background. Voice, streams, controlled video, authentication, API and attachment requests are not rewritten. These are feature tradeoffs, and their real-client effect still needs measurement.

The new Steam policy enables existing native low bandwidth, low performance, disabled library community content and reduced motion paths through a supported settings getter, alongside CSS changes. The browser engine remains. Missing community library content or motion is an intended tradeoff.

Chrome's new GUI profile affects installed Google Chrome 126 or later through four current-user performance policies. It enables native Maximum Memory Saver, disables background mode after the last window closes, and disables predictive page loading. Inactive tabs can reload more often and require more network traffic; there is no RAM cap or forced active-tab discard. The tool keeps Chrome open and does not read browser profiles or change security settings. Reload chrome://policy or restart normally when convenient. These policies may show Chrome's managed indicator, and existing local managed performance settings are preserved. The separate advanced Chromium source patch remains unchanged. [Native Maximum policy](https://chromium.googlesource.com/chromium/src/+/HEAD/components/policy/resources/templates/policy_definitions/Miscellaneous/MemorySaverModeSavings.yaml).

The tool now supports cooperative Stop during work. That improves control over an operation, not the client engines' memory budget. Cancellation is checked during bounded work; an atomic file replacement or operating-system request must finish, and recovery cannot be canceled. No hard cancellation latency or freedom from client hangs is certified.

## Target

Start with 2048 MiB available after Windows and ordinary background tasks. Allocate at most 1536 MiB of observed aggregate resident working set to the browser, Discord and Steam, leaving at least 512 MiB available. This does not describe a PC with only 2 GiB installed. Games, recording and other apps need their own budget.

These are proposed acceptance criteria, not measured results or manufacturer specifications:

| Criterion | Proposed acceptance |
|---|---|
| Three-app RAM | Peak aggregate working set <=1536 MiB in each scenario, with all required groups present and readable. |
| System spare RAM | >=512 MiB throughout the final session; record the minimum using a system monitor. The included collector records system availability at start/end only. |
| Idle CPU | Combined mean <=5% and p95 <=10% of the target machine's total CPU. |
| Chat, browsing and voice CPU | Combined mean <=25% and p95 <=40%; measure the game separately. |
| Interaction | p95 <=200 ms from input to visible response; no stall >1 s during repeated tasks. |
| Scrolling | On 60 Hz, at least 95% of frames <=33.3 ms in a fixed chat/library route; record a trace, not only average FPS. |
| Voice | Thirty minutes with no client-attributable disconnects or audible dropouts in a controlled test; record reported loss and jitter. |
| Gaming | Fixed scene, average FPS and 1% low no worse by >5% versus original clients, without new crashes or audio problems. |

Record the exact CPU model, physical cores and logical processors. CpuPercentActualMachine describes the tested machine. CpuPercentFourCoreEquivalent divides observed CPU time by four as arithmetic; it does not predict another processor's performance.

## Repeatable comparison

1. Record Windows, CPU, installed RAM, GPU, storage, driver, resolution, scaling, client versions, extensions and warm/cold cache conditions.
2. Keep account, pages, channel, game, audio volume and test route identical. Measure original clients first. Do not empty working sets or disable services to manufacture lower values.
3. Open all three apps, wait five minutes and record five minutes idle with all apps present. Do not substitute web Discord for the desktop patch in a desktop comparison.
4. Record ten minutes of fixed browsing/library tasks, twenty channel changes and typing. Test a thirty-minute voice call with windows visible and then in the background.
5. Record ten minutes of the same game scene with the call and other apps open. Record game settings, frame-time tool and connectivity.
6. Repeat after patching under the same conditions. Run three repetitions per variant/scenario, alternating order. Keep CSV, JSON, traces and failure observations; publish unfavorable results too.

Use dedicated test hardware or an appropriate VM. A VM can help study memory pressure, but GPU, audio and FPS conclusions require corresponding real hardware. Do not close personal apps or artificially constrain the development PC to manufacture acceptance.

## Included collector

With apps already open, run from the repository folder:

~~~powershell
powershell.exe -NoProfile -File .\scripts\Measure-AppBudget.ps1 -Scenario original-idle -DurationSeconds 300 -IntervalSeconds 1 -OutputDirectory .\results\original-idle-01
powershell.exe -NoProfile -File .\scripts\Measure-AppBudget.ps1 -Scenario modified-active -DurationSeconds 600 -IntervalSeconds 1 -OutputDirectory .\results\modified-active-01
~~~

Each output folder must be new. samples.csv contains per-app rows and a Total row per sample; summary.json contains hardware, peaks, means, coverage and verdict. The collector reads counters and writes local results. It does not change Windows/client settings or read window titles, URLs, history or command lines. Review hardware details and paths before sharing.

Parameters include DurationSeconds (default30), IntervalSeconds (2), BudgetMiB (1536), ReserveMiB (512), TargetAvailableMiB (2048), Scenario, OutputDirectory and DiscordMode (Desktop). Web declares an operator-selected scenario; it does not verify a signed-in web session, and a running desktop Discord is still counted.

Processes are grouped by name. Chrome includes chrome; Discord includes stable/PTB/Canary/Development; Steam includes steam, steamwebhelper, steamservice, GameOverlayUI and crash reporters. Names are not signature or path verification. Renamed browsers require collector changes and tests.

## Interpret results

KnownWorkingSetMiB is resident memory including shared pages; summing processes can count shared pages more than once. Failed reads produce partial values. KnownPrivateCommitMiB is privately committed memory, not private resident RAM. **Do not add private commit to working set.** GPU memory, the game and other programs are outside the three-app sum. [Microsoft process memory counters](https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex).

CPU uses per-process CPU-time differences and actual elapsed intervals. The first baseline has no delta. New, ended or unreadable processes produce partial_lower_bound; processes created and ended between samples may be missed. Snapshots are not atomic. Time-weighted means can include partial intervals and do not certify a CPU bound with incomplete coverage.

GPU utilization and memory need separate measurements. Record the same visible route, window size, scaling, animations and foreground/background state before and after. Task Manager reports a process's busiest GPU engine rather than a sum of engine percentages. Process-memory GPU counters can over-report on affected Windows systems; use the Performance pane or WPR/WPA to investigate. Integer zero readings in a brief sample do not prove zero activity. No GPU saving is established by this release. [Microsoft GPU display](https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/) and [counter issue](https://learn.microsoft.com/en-us/troubleshoot/windows-client/performance/gpu-process-memory-counters-report-wrong-value).

within_observed_working_set_budget means only that observed resident readings fit the threshold with required groups present and no failed memory reads. over_observed_working_set_budget means observed values already exceeded it. incomplete_observation prevents missing apps or unreadable values from passing. None certifies voice, latency, paging, GPU use or the complete project target.

## Readability and keyboard

Require at least4.5:1 contrast for ordinary text and3:1 for large text. Evaluate unrounded ratios; rounded displays are only presentation. [WCAG contrast](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html).

The declared Discord main text #e4f5eb over #111713 has16.058:1 contrast; Steam text over #101e17 has15.211:1. Secondary #c0d1c7 over #111713 has11.409:1. Focus #6affab over #101e17 has13.527:1. Node tests recalculate defined CSS combinations; the minimum declared Discord combination is6.39:1. These values do not inspect every inherited or remotely supplied client color.

Check 100/150/200% scaling, small windows, high contrast, visible focus, tab order and Enter/Space activation. Never hide controls, connection state, messages, names or voice indicators for lower counters. Offscreen screenshots help find clipping but do not replace keyboard and screen-reader checks in real clients.

See [Validation](VALIDACION.md) for tested fixtures and outstanding product checks. EXE size, smaller CSS and changed logos are not measurements of RAM savings. Transparency also does not establish fewer compositor layers. [Layer memory costs](https://web.dev/articles/stick-to-compositor-only-properties-and-manage-layer-count).
