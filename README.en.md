# Low Consuming Steam

A small Windows tool with one main button. It finds a supported installation, checks compatibility, saves a backup and applies the project's simpler interface changes.

**Experimental release.** This is a local patch tool, not a replacement client. The combined 2 GiB memory target has not been demonstrated. Chrome is a Chromium source tool and does not include a working browser.

[Download](https://github.com/TaWADAaaa/low-consuming-steam/releases/tag/v0.4.0-experimental) · [Beginner guide](docs/USER_GUIDE.en.md) · [Source code](https://github.com/TaWADAaaa/low-consuming-steam) · [Donations](DONATIONS.md) · [Privacy](PRIVACY.md)

## Start here

1. Open the [v0.4.0 download page](https://github.com/TaWADAaaa/low-consuming-steam/releases/tag/v0.4.0-experimental).
2. Download **low-consuming-steam-v0.4.0-experimental.zip**. Right-click it and choose **Extract All**.
3. Open the extracted folder and double-click **SteamTawop.exe**. You do not need the CMD files, Node or a development tool.
4. Save your work and finish calls, downloads or updates. Click the large button. After checks pass, it asks Discord or Steam to close, applies changes and reopens a client that was running. If it stays in the tray, use its Quit/Exit menu and retry.
5. Wait for the result. **Undo changes** restores verified original files when you want to go back.

Most users do not need to select a folder. If detection cannot find an unambiguous supported installation, use **Choose a different folder**. Discord's parent folder, such as %LOCALAPPDATA%\Discord, is accepted for detection. Chrome requires an existing Chromium source checkout; installed Google Chrome is not a valid target.

## What it changes

Patches supported client CSS with flat surfaces, visible focus and fewer shadows/blur effects. Hides recognized decorative blurred copies while retaining functional loading indicators. The library, downloads, games and Steam engine remain and need real client checks.

Supported decoration is reduced while text, controls and functional indicators remain. Flat backgrounds, fewer shadows and visible keyboard focus favor a readable interface. Hiding an image does not prove it stops downloading or using memory. Original assets stay on disk, and backups need extra space.

Unsupported files, incomplete installations, conflicting backups and unknown integrity requirements are rejected before patching. An explanatory message is a compatibility result; installing the tool does not make every client version supported. See the [beginner guide](docs/USER_GUIDE.en.md) for common messages.

## Windows download messages

The ZIP is a standard archive containing ordinary, unobfuscated Windows executables. The EXE is **unsigned**. Windows SmartScreen or antivirus software may still warn, including for a newly signed application. No packaging format can guarantee zero warnings. This project does not disable or bypass Windows protection.

Use this repository's release downloads. SHA256 files let you check that a download matches a release; they do not certify safety. The [source](src/) and [build instructions](#build-from-source) are available for review. [Microsoft explains application reputation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).

## Build from source

Use Windows 10/11, PowerShell 5.1 and .NET Framework 4.8. Development tests also need Node on PATH. Run BUILD.cmd, or:

~~~powershell
powershell.exe -NoProfile -File .\scripts\Build.ps1 -Product Steam
~~~

Each repository produces one executable. Sources for the shared backup engine and format modules are included. The standalone executable embeds the original CSS and JavaScript it needs. A complete external assets folder allows customization without recompiling. Developer explanations are in the guides, with clear names and consistent formatting in source instead of inline documentation comments.

| Guide | Contents |
|---|---|
| [Beginner guide](docs/USER_GUIDE.en.md) | Download, automatic operation, undo and common messages. |
| [Architecture](docs/ARQUITECTURA.md) | Detection, integrity checks, backups and limitations. |
| [Performance](docs/RENDIMIENTO.md) | Memory/CPU targets and a repeatable measurement procedure. |
| [Validation](docs/VALIDACION.md) | Automated checks and outstanding real client tests. |
| [Chromium build](README_CHROMIUM.md) | Separate native browser compilation for developers. |
| [Contributing](CONTRIBUTING.md) | Code layout, checks and private-data-free issue reports. |

## License and support

[MIT](LICENSE) covers original project code, tools, tests and artwork. The original Discord, Steam, Google Chrome and Chromium components retain their licenses. Complete proprietary clients are not distributed here. [Third-party notices](THIRD_PARTY_NOTICES.md).

Optional [donations](DONATIONS.md) support development. Downloading and using the tools is free.
