# Current-desktop diagnostic observation

On October 19, 2026, a read-only 30-second observation found that the earlier Discord patch was installed while Discord still used about 1 GiB of aggregate resident working set. This explains why a simpler appearance did not establish the requested memory reduction. The new revision changes additional application behavior, but its savings still need a controlled comparison.

This was an uncontrolled current workload, not an idle benchmark, original-versus-patched experiment or test on a four-core PC. No apps were closed, suspended, patched or reconfigured to collect it. The development machine had 12 physical cores and 24 logical processors. These observations do not predict another PC's performance.

| Process-name group | Peak processes | Mean working set MiB | Peak working set MiB | Mean private commit MiB |
|---|---:|---:|---:|---:|
| Chrome | 15 | 2500.928 | 2634.859 | 2012.552 |
| Discord | 6 | 1030.771 | 1030.867 | 856.995 |
| Steam | 9 | 1132.243 | 1132.484 | 1227.860 |
| Total | 30 | 4663.942 | 4797.992 | 4097.406 |

The collection covered 16 snapshots over 30.007 seconds, with all three groups present throughout. One Steam-group process was unreadable in each sample; Steam and total values are lower bounds. Working set includes shared resident pages and can double-count them between processes. Private commit is separate and must not be added to working set. The observed total already exceeded the proposed 1536 MiB application budget.

Mean observed CPU use was 0.581% of the development machine's total logical-processor capacity. Every total CPU interval had partial coverage because of the unreadable Steam process. The short observation did not reproduce the reported CPU spike or a hang. It establishes neither an idle CPU guarantee nor a four-core result.

A separate executable-path classification found 14 readable chrome.exe processes under the normal installed Google Chrome application executable pattern. No bundled automation/Chromium path or unreadable executable identity was found in that separate snapshot. This helps identify the group; it is not a signature verification or an atomic match to the earlier samples.

## What was actually installed

Discord had an active applied journal with two records. Both recorded patched SHA256 values matched the current files, and the installed core preload had the v3 marker. Steam had no active patch journal, and none of its 13 inspected frontend stylesheets had that marker. Installed Google Chrome was outside the existing Chromium-source-only tool's scope.

These are different installation states. Do not describe the three groups as a benchmark of three patched clients, or use this snapshot to calculate a saving percentage.

## GPU limits

A separate ten-snapshot GPU collection returned only integer zero utilization readings. It did not establish zero GPU activity or a reduction. GPU-process-memory counters were available, but those can report misleading values on affected Windows systems. No GPU saving is claimed. Cross-check Task Manager's Performance pane or WPR/WPA before interpreting GPU-memory results. [Microsoft's counter issue](https://learn.microsoft.com/en-us/troubleshoot/windows-client/performance/gpu-process-memory-counters-report-wrong-value).

Raw reports remain local and are excluded from release packages. This public summary omits local paths, usernames, process identifiers, accounts, window titles, URLs and credentials. Use the [performance protocol](RENDIMIENTO.md) for a matched original-versus-modified comparison.
