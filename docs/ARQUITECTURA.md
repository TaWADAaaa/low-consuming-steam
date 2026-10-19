# Architecture: Low Consuming Steam

This repository builds one native Windows Forms patch tool, SteamTawop.exe. It includes shared source modules for Discord, Steam and Chromium, with Steam selected at compile time. The window does not launch another browser engine. Source uses explicit names and consistent formatting; design explanations live here instead of inline documentation comments.

| File | Responsibility |
|---|---|
| src/Program.cs | Simple English window, progress, result and command-line entry point. |
| src/AutoSetup.cs | Installation detection and automatic close, patch and reopen workflow. |
| src/OperationControl.cs | Cooperative cancellation shared by preparation and transactional edits. |
| src/AggressivePolicy.cs | Original Discord request policy and Steam native settings policy. |
| src/ModCore.cs | Allowed paths, plans, hashes, journals, atomic file replacement and restoration. |
| src/AssetStore.cs | Embedded original resources, complete external assets and verified cache. |
| src/AsarArchive.cs | ASAR structure, integrity validation and reconstruction. |
| src/DiscordMod.cs | Supported Discord recognition and CSS/preload patch preparation. |
| src/SteamMod.cs | Supported frontend stylesheets and native library settings-getter patch. |
| src/ChromeMod.cs | Chromium branding and two memory-saving source defaults. |
| src/ChromeRuntimeProfile.cs | Installed Google Chrome performance policies and isolated policy recovery. |
| scripts/ | Native patcher build, packaging, optional Chromium build and measurements. |
| tests/ | Temporary format, recovery, workflow, resource and GUI fixtures. |

## Automatic operation

The main action detects a known installation or resolves a manually selected parent folder. It prepares changes before closing the selected client. A compatibility failure stops before patching. Normal shutdown is requested only for processes inside the selected installation. A client that hides to its tray may require its Quit/Exit menu; processes are never force-terminated. Once closure is verified, the existing engine verifies originals, creates backups and writes changes. A client that was running is reopened. Undo uses the same root resolution and backup checks.

Discord/Steam closure can end calls or pause downloads, so the main screen asks the user to save work first. Chrome's GUI action keeps the browser open and configures only its four allowed current-user performance policies. The workflow does not change client integrity fuses, read account tokens, disable Windows protection or install a service. The advanced Chromium source commands do not become an automatic browser build.

Work runs away from the GUI event loop. The main button becomes Stop while busy; closing the window requests the same cancellation and waits for a safe result. Preparation uses cancellation checkpoints and bounded reads. A prepared plan is reused after client closure rather than preparing twice. Once a transaction has changed files, cancellation triggers attempted rollback. Rollback ignores cancellation so a repeated Stop cannot interrupt recovery. A blocked operating-system request, lock or external update can still delay or prevent completion; backups remain available.

## Assets

Every EXE embeds discord/tawop.css, discord/preload.js and steam/tawop.css. A complete external assets folder takes precedence for explicit customization. An incomplete folder is rejected rather than mixing versions.

Without external assets, resources are extracted under %LOCALAPPDATA%\TawopModKit\assets\0.5.0-<hash16>. SHA256 hashes are checked when creating and reusing them. A mutex coordinates concurrent preparation; temporary files are moved into place. Links, absent resources and files beyond the size limit are rejected. Modified cache files are preserved and reported, not overwritten.

## Backups and recovery

Only allowlisted client paths can be edited. Original hashes are checked during planning and immediately before replacement. Exact originals and a schema-versioned journal are saved under .tawop inside the installation. Each replacement is atomic, but the whole client is not a single atomic transaction.

A failure triggers attempted rollback. A lock or external change can prevent completion; the journal remains for recovery. Undo verifies both backup bytes and current files. If an updater changed a target, that change is kept and the corresponding original remains pending. Historical schema-1 status strings remain stable for compatibility with v0.3 backups; they are internal journal values, not UI labels.

## Client modules

Discord reconstructs supported ASAR files after verifying paths, offsets, payload and SHA256 blocks. Recognized CSS and main-process structure receive original styles and a preload. The client EXE and Electron integrity fuses are never patched. Unknown formats or enforced embedded ASAR integrity stop the operation.

AsarArchive.Read retains the supplied byte array and aliases unchanged members to avoid allocating another full payload copy. A caller must not mutate that input for the archive object's lifetime. GetFile returns a defensive copy, and SetFile clones the replacement bytes. The patch engine keeps its input buffer private. See the [patcher benchmark](PATCHER-BENCHMARK.md) for allocation measurements; those are separate from running-client memory.

Discord's initial BrowserWindow preferences disable image animation and built-in spellcheck. Image animation suppression applies to GIF, APNG and animated WebP images, including message images. The existing request dispatcher receives an original policy for exact recognized decorative CDN routes and static small avatar/icon/emoji forms. It does not replace Electron's request listener, authentication, APIs, attachments, voice or streaming protocols. Supported anchors are required before a change is prepared. [Electron preferences](https://www.electronjs.org/docs/latest/api/structures/web-preferences) and [request API](https://www.electronjs.org/docs/latest/api/web-request).

The preload pauses connected, muted, autoplay-loop decorative video with finite positive duration, no controls and no srcObject even in the foreground. Events and WeakMap avoid polling and retaining detached elements. User-controlled video, audio and streams are excluded.

Steam appends original CSS to supported frontend sheets and keeps exact original bytes. It also patches a recognized native clientSettings getter to return a cached WeakMap Proxy forcing library_low_bandwidth_mode, library_low_perf_mode, library_disable_community_content and accessibility_reduce_motion true. Existing client rendering paths perform the reductions; community library content and motion are feature tradeoffs. Unknown JavaScript anchors stop preparation. Backend, DRM, downloads, game executables and user profiles are outside the patch targets. Class selectors and native anchors may change after updates.

Chromium accepts a source checkout, changes four branding values and two memory-saving defaults, and preserves unrelated fields and encoding. Existing profile preferences and managed policies still prevail in a future build. Installed Google Chrome is not accepted. See [Chromium build](../README_CHROMIUM.md).

The separate Chrome GUI profile accepts official Google Chrome 126 or later. It writes HighEfficiencyModeEnabled=1, MemorySaverModeSavings=2, BackgroundModeEnabled=0 and NetworkPredictionOptions=2 under HKCU\Software\Policies\Google\Chrome. These enable native Maximum Memory Saver, disable background mode after the last window closes, and disable predictive page loading. Native Chrome heuristics decide when eligible inactive tabs are discarded; this is not a memory cap. Frequent reloads and extra network traffic are tradeoffs. The settings may display Chrome's managed indicator. [Memory Saver policy definition](https://chromium.googlesource.com/chromium/src/+/HEAD/components/policy/resources/templates/policy_definitions/Miscellaneous/MemorySaverModeSavings.yaml).

The policy engine rejects existing user target values and detected machine/recommended target policies. Before its first write it durably saves an allowlisted, hashed journal under %LOCALAPPDATA%\LowConsuming\Chrome. Only original absence is accepted as the baseline; arbitrary prior registry values cannot be reinstated through a crafted journal. A mutex serializes tool instances. Undo uses compare-and-restore for each value and preserves external edits. Stop during apply attempts uncancelable rollback; Stop during undo saves the current value's recovery state before leaving the rest pending. It never deletes registry trees, reads browser profiles, closes the browser or weakens security settings. Reload chrome://policy or restart Chrome normally for settings that have not yet refreshed. The internal registry interface supports tests with an in-memory fake; tests do not write real browser policies.

## Visual and performance limits

Flat surfaces, fewer shadows, visible focus and recognized decorative-layer removal simplify drawing. Hiding an asset does not guarantee avoided network requests, decoding or resident memory. Transparency can still require compositing. Original assets stay on disk; backups add disk use. See [Chrome rendering](https://developer.chrome.com/blog/inside-browser-part3) and [layer costs](https://web.dev/articles/stick-to-compositor-only-properties-and-manage-layer-count).

Passing fixtures proves tested invariants, not client compatibility or a RAM saving. The combined 2 GiB / four-core target remains unverified. [Performance protocol](RENDIMIENTO.md).

MIT covers original project code and assets. It does not relicense third-party clients or include their complete engines. [Third-party notices](../THIRD_PARTY_NOTICES.md).
