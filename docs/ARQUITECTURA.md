# Architecture: Low Consuming Steam

This repository builds one native Windows Forms patch tool, SteamTawop.exe. It includes shared source modules for Discord, Steam and Chromium, with Steam selected at compile time. The window does not launch another browser engine. Source uses explicit names and consistent formatting; design explanations live here instead of inline documentation comments.

| File | Responsibility |
|---|---|
| src/Program.cs | Simple English window, progress, result and command-line entry point. |
| src/AutoSetup.cs | Installation detection and automatic close, patch and reopen workflow. |
| src/ModCore.cs | Allowed paths, plans, hashes, journals, atomic file replacement and restoration. |
| src/AssetStore.cs | Embedded original resources, complete external assets and verified cache. |
| src/AsarArchive.cs | ASAR structure, integrity validation and reconstruction. |
| src/DiscordMod.cs | Supported Discord recognition and CSS/preload patch preparation. |
| src/SteamMod.cs | Supported frontend stylesheets and original CSS additions. |
| src/ChromeMod.cs | Chromium branding and two memory-saving source defaults. |
| scripts/ | Native patcher build, packaging, optional Chromium build and measurements. |
| tests/ | Temporary format, recovery, workflow, resource and GUI fixtures. |

## Automatic operation

The main action detects a known installation or resolves a manually selected parent folder. It prepares changes before closing the selected client. A compatibility failure stops before patching. Normal shutdown is requested only for processes inside the selected installation. A client that hides to its tray may require its Quit/Exit menu; processes are never force-terminated. Once closure is verified, the existing engine verifies originals, creates backups and writes changes. A client that was running is reopened. Undo uses the same root resolution and backup checks.

Client closure can end calls or pause downloads, so the main screen asks the user to save work first. The workflow does not change client integrity fuses, read account tokens, disable Windows protection or install a service. Chromium is a source-only action and does not become an automatic browser build.

## Assets

Every EXE embeds discord/tawop.css, discord/preload.js and steam/tawop.css. A complete external assets folder takes precedence for explicit customization. An incomplete folder is rejected rather than mixing versions.

Without external assets, resources are extracted under %LOCALAPPDATA%\TawopModKit\assets\0.4.0-<hash16>. SHA256 hashes are checked when creating and reusing them. A mutex coordinates concurrent preparation; temporary files are moved into place. Links, absent resources and files beyond the size limit are rejected. Modified cache files are preserved and reported, not overwritten.

## Backups and recovery

Only allowlisted client paths can be edited. Original hashes are checked during planning and immediately before replacement. Exact originals and a schema-versioned journal are saved under .tawop inside the installation. Each replacement is atomic, but the whole client is not a single atomic transaction.

A failure triggers attempted rollback. A lock or external change can prevent completion; the journal remains for recovery. Undo verifies both backup bytes and current files. If an updater changed a target, that change is kept and the corresponding original remains pending. Historical schema-1 status strings remain stable for compatibility with v0.3 backups; they are internal journal values, not UI labels.

## Client modules

Discord reconstructs supported ASAR files after verifying paths, offsets, payload and SHA256 blocks. Recognized CSS and main-process structure receive original styles and a preload. The client EXE and Electron integrity fuses are never patched. Unknown formats or enforced embedded ASAR integrity stop the operation.

The preload pauses only connected, muted, autoplay-loop video with finite positive duration, no controls and no srcObject while hidden. It attempts to resume the same still-eligible source on return. Events and WeakMap avoid polling and retaining detached elements. It does not replace authentication, voice, message protocols or Electron.

Steam appends original CSS to supported frontend sheets and keeps exact original bytes. Specific decorative blurred copies and shadow effects are reduced while functional loading indicators remain. Backend, DRM, downloads and game executables are outside the patch targets. Class selectors may change after updates.

Chromium accepts a source checkout, changes four branding values and two memory-saving defaults, and preserves unrelated fields and encoding. Existing profile preferences and managed policies still prevail in a future build. Installed Google Chrome is not accepted. See [Chromium build](../README_CHROMIUM.md).

## Visual and performance limits

Flat surfaces, fewer shadows, visible focus and recognized decorative-layer removal simplify drawing. Hiding an asset does not guarantee avoided network requests, decoding or resident memory. Transparency can still require compositing. Original assets stay on disk; backups add disk use. See [Chrome rendering](https://developer.chrome.com/blog/inside-browser-part3) and [layer costs](https://web.dev/articles/stick-to-compositor-only-properties-and-manage-layer-count).

Passing fixtures proves tested invariants, not client compatibility or a RAM saving. The combined 2 GiB / four-core target remains unverified. [Performance protocol](RENDIMIENTO.md).

MIT covers original project code and assets. It does not relicense third-party clients or include their complete engines. [Third-party notices](../THIRD_PARTY_NOTICES.md).
