# Low Consuming Steam

An open tool for applying local modifications to Steam on Windows. Includes original source code, guides, tests and the **`SteamTawop.exe`** executable.

**Experimental.** This executable is a patching tool; the original client engine remains. For Chrome, it patches Chromium source code and still requires building and testing the browser. The combined target of Discord, Steam and a browser with **2048 MiB available after Windows**, allocating 1536 MiB to those apps and leaving 512 MiB spare, **has not been demonstrated**.

[Español](README.es.md) · [Source](https://github.com/TaWADAaaa/low-consuming-steam) · [Downloads](https://github.com/TaWADAaaa/low-consuming-steam/releases) · [English user guide](docs/USER_GUIDE.en.md) · [Donations / Donaciones](DONATIONS.md) · [Privacy / Privacidad](PRIVACY.md)

## What this release changes

Patches supported client CSS with flat surfaces, visible focus and fewer shadows/blur effects. Hides recognized decorative blurred copies while retaining functional loading indicators. The library, downloads, games and Steam engine remain and need real client checks.

The public projects are [Discord](https://github.com/TaWADAaaa/low-consuming-discord), [Steam](https://github.com/TaWADAaaa/low-consuming-steam) and [Chrome / Chromium](https://github.com/TaWADAaaa/low-consuming-chrome). This repository distributes **one patching tool: `SteamTawop.exe`**, with its sources and downloads. The tools share an original patch, backup and restore engine; sharing these sources does not make the third-party clients open source.

## Download and use

[Download SteamTawop.exe](https://github.com/TaWADAaaa/low-consuming-steam/releases/download/v0.3.0-experimental/SteamTawop.exe) · [Sources and guides ZIP](https://github.com/TaWADAaaa/low-consuming-steam/releases/download/v0.3.0-experimental/low-consuming-steam-v0.3.0-experimental.zip)

Run the individual EXE, or extract the full ZIP for sources, guides and editable assets. The executable embeds the kit's three original assets. Without an external `assets/` folder, it prepares them under `%LOCALAPPDATA%\TawopModKit\assets\0.3.0-<hash>` and checks their SHA256 hashes before use.

1. Select a supported installation folder. For Chrome, select the `src` folder of a Chromium checkout, not an installed Google Chrome folder.
2. Choose **Analizar archivos** to inspect paths, sizes and hashes.
3. Close the client from its tray and wait for any update to finish.
4. Choose **Aplicar parches**. The kit keeps exact originals and a journal under `.tawop/` in the selected installation.
5. Start the client normally and check its features. To undo the changes, close it and choose **Restaurar originales**.

Keep `.tawop/` until restoration is complete. If an updater or another tool changed a file, restore preserves that change and leaves its backup pending. Replacements are atomic per file; the whole installation is not a single atomic transaction.

The executables are unsigned. Source code and hashes help review a release; they do not certify safety or compatibility. Writing to a protected folder requires the appropriate Windows permissions.

## Simple assets and readable controls

The visual changes prioritize flat surfaces, contrast, keyboard focus and removal of recognized decoration. Controls, messages, names and functional indicators must remain usable. Client selectors change with updates and need checking in the real applications.

Hiding an asset or making it transparent does not guarantee that it stops downloading, decoding or occupying memory. The kit does not remove original client assets from disk, and backups use additional space. Tests check the declared palette, but full accessibility across every client screen remains unverified.

A complete external `assets/` folder allows customization without recompiling. It must contain `discord/tawop.css`, `discord/preload.js` and `steam/tawop.css`. Incomplete folders are rejected to avoid mixing versions. A modified generated cache causes an error without overwriting it; the message identifies the version folder that can be moved aside to regenerate it.

## Build and check

Use Windows 10/11, PowerShell 5.1 and the .NET Framework 4.8 C# compiler. The full development test suite also needs Node on PATH; the distributed executable does not need Node or Electron.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1 -Product Steam
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Measure.Tests.ps1
```

`Build.ps1 -Product Steam -SkipTests` builds only this patching tool without validating the suite. Fixtures check the shared patch engine and formats; they do not certify real client voice calls, updates or resource use. Chromium requires a separate C++ toolchain and all applicable notices. [Chromium build guide in Spanish](README_CHROMIUM.md).

## Guides and evidence

| Document | Contents |
|---|---|
| [English user guide](docs/USER_GUIDE.en.md) | Applying, restoring, assets, privacy and development. |
| [Architecture, Spanish](docs/ARQUITECTURA.md) | Patch engine, ASAR, embedded assets, backups and limits. |
| [Performance, Spanish](docs/RENDIMIENTO.md) | RAM/CPU budget, contrast and comparison protocol. |
| [Validation, Spanish](docs/VALIDACION.md) | Repeatable tests and outstanding real client checks. |
| [Contributing, Spanish](CONTRIBUTING.md) | Source organization, tests and reproducible reports. |
| [Third-party notices, Spanish](THIRD_PARTY_NOTICES.md) | Licenses and third-party components. |

Design explanations live in the guides. Original code uses explicit names and consistent formatting without inline documentation comments. Functional compiler directives remain.

## License and support

[MIT](LICENSE) covers original Tawop code, styles, tools, tests and artwork. Discord, Steam, Google Chrome and Chromium retain their respective licenses. This repository does not redistribute complete proprietary clients.

Optional support addresses and their exact networks are listed in [Donations](DONATIONS.md). Donations are not required to download or use the tools.
