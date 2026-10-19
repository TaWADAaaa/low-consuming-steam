# Low Consuming Steam user guide

This release contains the `SteamTawop.exe` patching tool and original Tawop source code. It does not replace the original client engine. For Chrome, the executable patches Chromium source code and a complete native browser build remains outstanding.

## Before applying a patch

Use a supported installation and keep a way to reinstall the original client. Finish updates and close the client through its tray menu. The tool does not terminate applications or calls. An unsupported or ambiguous format is rejected instead of being modified.

Run the individual EXE, or extract the full package to inspect and customize its original assets. The GUI currently uses Spanish action names:

| GUI text | Meaning |
|---|---|
| Elegir carpeta | Select folder. |
| Analizar archivos | Inspect files without modifying the client. |
| Aplicar parches | Apply the reviewed changes with backups. |
| Restaurar originales | Restore verified original files. |

For Discord, select a supported installed `Discord\app-VERSION` folder. For Steam, select the folder containing `steam.exe`. For Chrome, select `src` in a Chromium checkout, not the installed Google Chrome folder.

## Inspect, apply and restore

Inspect first and review every target path, size and SHA256 hash. Applying rechecks the inputs, saves originals and records a journal under `.tawop/` inside the target installation. Files are replaced individually; this is not an atomic transaction covering the whole application.

After applying, start the client normally. Check login, messages, keyboard navigation, text readability, dialogs and the features you use. For Discord, explicitly check microphone, voice, screen sharing and media. For Steam, check library, downloads, game launch and dialogs. These real client checks are not replaced by fixture tests.

To restore, close the client and use **Restaurar originales**. Keep `.tawop/` until restoration completes. If an update or another tool changed a file, restore preserves that external change and leaves the corresponding backup pending. Do not apply during an update. On failure, the engine attempts rollback and retains its journal when recovery cannot finish.

The EXE is unsigned. Source code and the build manifest support review; they do not certify compatibility or safety. Protected installation folders require the appropriate Windows write permissions.

## Embedded assets and customization

Every distributed EXE embeds `discord/tawop.css`, `discord/preload.js` and `steam/tawop.css`. If no external `assets/` folder exists next to it, the tool extracts these original resources to `%LOCALAPPDATA%\TawopModKit\assets\0.3.0-<hash>` and verifies their SHA256 hashes on reuse. This cache contains only kit assets, not client files or account profiles.

A complete external `assets/` folder takes precedence. It must contain all three paths listed above. This supports customization without recompiling. Incomplete external folders are rejected rather than mixed with embedded assets. If generated cache content was changed, the tool stops without overwriting it and identifies the version folder that can be moved aside to regenerate it.

Visual changes favor flat surfaces, readable text and visible focus. Specific decorative layers can be hidden; functional controls and indicators must remain. Hiding a resource or setting transparency does not prove a download, decoding or memory reduction. Original files remain on disk, and backups take additional space.

## Build and tests

The kit uses Windows Forms and the .NET Framework C# compiler. Use Windows 10/11, PowerShell 5.1 and .NET Framework 4.8. Full development tests need Node on PATH, although the distributed EXE does not depend on Node or Electron.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1 -Product Steam
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Measure.Tests.ps1
node .\tests\Preload.Tests.js
```

The C# suite checks synthetic ASAR archives, patch preparation, backup/restore behavior and embedded asset extraction, reuse, concurrency and tampering. GUI previews are rendered offscreen. JavaScript tests simulate DOM/media behavior and check the declared palette. Collector tests check process grouping, budget boundaries, missing apps, sums and CPU calculations; their full run adds a brief read-only sample with local TEMP output.

`Build.ps1 -Product Steam -SkipTests` compiles only this patching tool without running the suite. Passing fixtures does not prove that a real client starts correctly, saves memory or keeps a voice call stable. Source formatting uses explicit names and consistent layout; design commentary belongs in the guides rather than inside the code.

Chromium requires a separately prepared source checkout and C++ toolchain. `Build-Chromium.ps1` does not download the checkout or install its prerequisites. Native browser compilation, startup and accessibility tests remain outstanding. Keep the revision, patches and all required third-party notices with any future derived browser release.

## Measure resource use

The project target is 2048 MiB available after Windows, with at most 1536 MiB observed aggregate resident memory for the three apps and 512 MiB spare. It does not describe a PC with only 2 GiB installed. A game and other applications need their own budget. **This target is not yet demonstrated.**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Measure-AppBudget.ps1 -Scenario original-idle -DurationSeconds 300 -IntervalSeconds 1 -OutputDirectory .\results\original-idle-01
```

Measure original and modified clients using the same hardware, versions, account, pages, channels, game scene and settings. Wait five minutes after opening the apps, then sample five minutes idle, ten minutes active tasks, a thirty-minute voice call and a fixed gaming scene. Repeat each variant/scenario three times and report failures as well as improvements.

Resident working set may count shared pages more than once. Private commit is different and must not be added to working set. CPU uses process CPU-time deltas; partial coverage can undercount. The collector groups processes by name and does not measure GPU use, voice interruptions, input latency or game frame times. Its four-core arithmetic field does not predict performance on another CPU.

A within-budget verdict certifies only the observed working-set criterion with required groups present and readable. Real CPU, responsiveness, voice and gaming acceptance require the additional checks in the Spanish [performance protocol](RENDIMIENTO.md).

## Privacy and reporting

Kit code has no sign-in, token reader, telemetry or report uploader. Original clients retain their own network behavior. Reports can contain installation paths and hardware information. Review paths, usernames and screenshots before sharing. Do not attach account data, credentials, profiles, complete client archives or `.tawop/` backups.

Report the kit/client version, selected action, error, expected result and a minimal reproducible case. A privacy audit documents inspected files and exclusions; it cannot promise an absolute absence of every secret or vulnerability. Read [Privacy](../PRIVACY.md) and [Donations](../DONATIONS.md) for the separate, intentionally public donation addresses.

MIT covers original kit code and assets. It does not relicense Discord, Steam, Google Chrome or Chromium. No complete proprietary client is included in these sources.
