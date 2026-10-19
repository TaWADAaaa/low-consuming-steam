# Validation: Low Consuming Steam v0.5.1

Prepared on October 19, 2026. This is an experimental release. SteamTawop.exe is a local patch tool, not a replacement client. BUILD-MANIFEST.json records its version, hash and automated-test result. A passing build does not prove the resource budget or full real-client operation.

## Repeatable checks

| Check | Scope |
|---|---|
| Native compilation | scripts/Build.ps1 -Product Steam, using the .NET Framework compiler. |
| Automatic workflow | Temporary discovery, selection and close/patch/reopen fixtures where included; real clients are not modified by the suite. |
| Cancellation | Cooperative cancellation checkpoints and temporary recovery fixtures. Recovery is not interrupted by a second stop request. |
| Aggressive policies | Supported JavaScript transforms and synthetic request/settings behavior. No real network saving or app-memory benchmark. |
| Embedded assets | Extraction, reuse, concurrency, hashes, missing resource, changed cache, complete/incomplete external folder. |
| ASAR | Original fixtures for Unicode, SHA256 blocks, corruption, paths, offsets and unrelated payload preservation. |
| Discord integrity | Supported/unknown fuse formats and embedded ASAR integrity rejection, without modifying the client EXE. |
| Chromium | Branding/default edits, repetition, BOM/CRLF, unrelated fields and ambiguous source rejection. No browser compilation. |
| Installed Chrome policies | In-memory registry fixtures for the four allowed DWORDs, pre-write durable backup, existing management rejection, failed writes, Stop/rollback, external edits and interrupted undo. No real registry writes in the tests. |
| Steam | Stylesheet and supported native settings-getter fixtures; original byte preservation. No real client execution. |
| Recovery | Temporary backups, external changes, locked files, altered backups and journal handling. |
| Window | Offscreen layout checks at multiple sizes. Visual review, keyboard and screen-reader testing remain necessary. |
| Preload | Node-driven simulated DOM/media behavior and declared-palette contrast. No account or real voice call. |
| Collector | Grouping, budget boundaries, invalid readings, CPU deltas, sums, missing apps and a synthetic web scenario. |

~~~powershell
powershell.exe -NoProfile -File .\scripts\Build.ps1 -Product Steam
powershell.exe -NoProfile -File .\tests\Measure.Tests.ps1
node .\tests\Preload.Tests.js
node .\tests\AggressivePolicy.Tests.js
~~~

Full development validation needs Node on PATH; release users do not need Node. -SkipTests compiles without validating and must not be advertised as a passing test run. The collector's -SkipIntegration mode keeps unit cases while skipping its short read-only local sample. Temporary reports and developer paths are excluded from publication.

## Outstanding real-client acceptance

Discord and Steam must be tested after applying on a dedicated installation: startup, text, dialogs, keyboard, microphone, voice, sharing, media, library, downloads and game launch as applicable. Updates and undo must be exercised against real client versions. A supported structure alone does not prove these behaviors.

Discord image-animation suppression and disabled spellcheck are feature tradeoffs, not transparent optimizations. Test message images as well as profile decoration. Test that API, attachment, voice and streaming request paths are preserved. Steam's native library settings reduce community content, motion and media; verify ordinary library controls and game launch remain usable.

The [current-desktop observation](BASELINE.md) records the previous Discord patch present and Steam unpatched in an uncontrolled workload. It exceeds the proposed memory budget and does not establish a before-and-after saving. No GPU-performance improvement is certified.

A complete native Chromium build, startup, navigation, video, WebRTC and accessibility checks remain outstanding; this release does not ship that browser.

The combined RAM target of at most 1536 MiB for all three apps, at least 512 MiB spare, CPU/latency criteria, a 30-minute voice call and gaming on four physical cores under memory pressure remain unverified. Use the [performance protocol](RENDIMIENTO.md), with three repetitions per scenario and failures reported alongside improvements.

Verify the actual remote build status in GitHub Actions and the actual downloadable files on this release page. Neither a workflow artifact nor a prerelease label proves the clients' resource use.
