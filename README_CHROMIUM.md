# Chromium source tool

ChromeTawop.exe prepares supported Chromium source code. It is not a browser and does not patch installed Google Chrome. Select the src folder of a complete source checkout and use **Prepare Chromium sources**.

| Source file | Change |
|---|---|
| chrome/app/theme/chromium/BRANDING | Four product/installer names become ChromeTawop; unrelated fields remain. |
| components/performance_manager/user_tuning/prefs.cc | Memory Saver enabled and aggressive for the two default registrations. |

Unique keys and registrations are required. Unknown or ambiguous structure stops the operation. UTF-8 BOM and existing line endings are preserved. The shared engine verifies files and keeps originals for undo. Existing user choices and managed policies still prevail in a future browser build. Aggressive memory saving may reload inactive tabs more often; it does not guarantee a smaller active tab.

## Native build for developers

scripts/Build-Chromium.ps1 requires an already synchronized checkout and dependencies with the patch applied. It does not fetch Chromium, install Visual Studio or download prerequisites. Follow the [official Windows instructions](https://chromium.googlesource.com/chromium/src/+/main/docs/windows_build_instructions.md) for the selected revision.

The inspected baseline required Windows x64, Visual Studio 2026 >=18.0 with C++ x64 and ATL/MFC, Windows SDK 10.0.28000.2270, Debugging Tools >=10.0.26100.3323, Git and depot_tools. Requirements can change with the source revision. Chromium development needs substantial RAM and disk space; a checkout path must have no spaces. Build-machine requirements do not establish runtime memory performance.

~~~powershell
powershell.exe -NoProfile -File .\scripts\Build-Chromium.ps1 -SourcePath C:\src\chromium\src -CheckOnly
powershell.exe -NoProfile -File .\scripts\Build-Chromium.ps1 -SourcePath C:\src\chromium\src -PackagePath C:\releases\ChromeTawop-native
~~~

CheckOnly diagnoses prerequisites without starting compilation. The full command builds the real chrome target under out\TawopBrowser, using optimized release settings without debug symbols. The internal browser executable remains chrome.exe; the patch tool ChromeTawop.exe is a separate program.

Packaging queries GN runtime_deps and copies existing dependencies while retaining layout. External paths and links are rejected. It includes Chromium's primary license and a manifest with source revision, patched hashes and validation status. An external dependency stops packaging while preserving the build for review.

## Current state

Synthetic tests cover the source edits and rejection cases. **A complete native Chromium build and browser startup test have not been completed.** No compiled browser is distributed in this release.

Before distributing a browser, verify startup, navigation, video, WebRTC, keyboard access, readability, tab restoration and real resource use. Keep all third-party notices required by the selected revision; the primary license alone is not a substitute. Retain revision, source patches and the manifest. [Performance protocol](docs/RENDIMIENTO.md).
