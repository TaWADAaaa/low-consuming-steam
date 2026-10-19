# Beginner guide: Low Consuming Steam

## Download and open

Open the [release page](https://github.com/TaWADAaaa/low-consuming-steam/releases/tag/v0.5.1-experimental), expand **Assets** and download **low-consuming-steam-v0.5.1-experimental.zip**. Right-click the ZIP, choose **Extract All**, and open the new folder. Double-click **SteamTawop.exe**. The individual EXE download also works by itself.

You do not need to understand source code or run a command. Developer files are included so the tool can be reviewed and rebuilt. BUILD.cmd is for developers; it is not the app.

The executable is unsigned. Windows may display a publisher or reputation message, and antivirus software can flag unknown files. A ZIP does not remove these checks. Keep Windows protection enabled. If you do not trust a download, stop and review the source or ask someone you trust to help. SHA256 checks establish file identity, not safety certification.

## Use the main button

Save your work and finish any call, game, download or update first. For Discord or Steam, click **Optimize Discord** or **Optimize Steam**. The tool locates the installed client, checks its files, requests a normal shutdown, backs up originals and applies the changes. If it stays in the tray, choose Quit Discord or Exit Steam there and retry; the tool does not force-terminate it. It reopens the client if it was running. Keep the window open until it shows the result.

For Chrome, the button is **Optimize Chrome**. It detects official installed Google Chrome 126 or later and saves a profile of four current-user performance policies: Memory Saver enabled at Maximum, background mode disabled, and predictive page loading disabled. It does not close Chrome. Open chrome://policy and click **Reload policies**, or restart Chrome normally when convenient. These supported policies refresh dynamically, but existing work may not change immediately.

Chrome can discard eligible inactive tabs sooner; revisiting them reloads their pages and can increase network traffic. Background apps after the last browser window closes and predictive page preloading are disabled. The tool does not force-close active tabs, calls or audio. Chrome may display **Managed by your organization** because the settings are local policies; it does not enroll you in an organization. While applied, the corresponding settings are controlled by policy. **Undo changes** removes only this tool's verified values. Detected existing local managed performance policies are preserved instead of overridden.

Developer CLI commands still provide the separate Chromium source patch. For example, ChromeTawop.exe --inspect C:\src\chromium\src examines a source checkout; --apply and --restore prepare or undo that source patch. These commands do not configure the installed browser. Native Chromium compilation is a separate task in [the Chromium guide](../README_CHROMIUM.md).

If automatic detection needs help, click **Choose a different folder**. For Discord you may select the Discord parent folder or a supported app-VERSION folder. The parent can contain several versions; detection must select an unambiguous usable installation. For Steam choose the folder containing steam.exe. For Chrome choose the Google Chrome Application folder or its parent. A Chromium source checkout is only for the advanced CLI.

**Details** shows diagnostic information. You do not need to inspect a plan or use a second apply button for normal operation.

## Stop an operation

While an operation runs, the main button becomes **Stop**. Click it once to request cancellation. Closing the window during an operation also requests a safe stop; the window stays open until the operation reaches a safe result.

Stopping is cooperative. Reading, checking and preparing stop at checkpoints. If replacement has started, the tool attempts to roll back its own changes before finishing. That recovery cannot itself be canceled. A locked file or external update can prevent complete recovery; keep the .tawop backup and use **Undo changes** when the client is closed. Do not end the tool in Task Manager while it is recovering.

During **Undo changes**, Stop finishes the current file and saves recovery status before stopping; the remaining files stay available for another undo. Upgrading an earlier project patch restores its verified originals first. If you stop between that restoration and the new patch, the app is left in its original state rather than partly upgraded.

Stop controls this tool's current operation. It does not suspend Discord, Steam, Chrome, a game, a call or a Windows service. An operating-system disk request can still take time to return. The status text explains whether the tool is stopping or recovering.

## What aggressive mode changes

Discord uses supported main-process paths to block recognized decorative CDN assets before fetching them, and requests small static forms of recognized avatar, icon and emoji images. Requests for authentication, APIs, voice, video, streams and attachments are not rewritten. The image animation policy makes GIF, APNG and animated WebP images still, including images in messages. Built-in spellcheck is disabled. These are deliberate feature tradeoffs. Eligible muted autoplay-loop decorative videos are paused even while the window is visible; controlled video, audio and streams are excluded.

Steam uses the supported library settings getter to enable its native low bandwidth, low performance, disabled community content and reduced motion paths. This can remove community screenshots/feed content and reduce motion and decorative library media. The tool also applies simpler styling. Game executables, downloads, DRM and account profiles are outside its patch targets.

These policies need recognized source anchors. An unknown or changed client build is rejected instead of applying a guessed edit. Browser engines and ordinary app state remain in memory. No fixed memory, CPU or GPU reduction is guaranteed.

## Go back to the original version

Use **Undo changes**. Discord and Steam verify their backups and current files before restoring. Keep the hidden .tawop folder inside the application folder until undo completes; it contains the originals.

Chrome's GUI profile uses %LOCALAPPDATA%\LowConsuming\Chrome\policy-backup.json instead. This backup contains only the four allowed policy names, original absence, desired DWORD values, hashes and recovery state. Undo compares current values before deleting its own changes; external edits are preserved. It never deletes the Chrome policy registry tree or another setting. The advanced Chromium source CLI continues to use a .tawop source backup.

If another tool or an updater changed a file, undo keeps that external change and leaves the backup available. It does not force an old file over a newer one. If a failure interrupts patching, automatic rollback is attempted; any unfinished recovery information is kept. Replacement is atomic for each file, not for the entire installation at once.

## Common messages

| Message or situation | What to do |
|---|---|
| Application not found | Install the official client first, or use Choose a different folder. Discord's usual parent is %LOCALAPPDATA%\Discord; other channels may have their own folders. |
| Unsupported client or integrity format | This version cannot be patched safely by the current tool. Keep the original client and report the version with the error text. An unknown fuse format is not fixed by administrator permissions. |
| Client cannot close | Save work, end calls/games and close the client through its tray menu. Try again when its updater has finished. |
| Access denied | The selected folder needs Windows write permissions. Check that it is your intended installation. Administrator rights do not make an unsupported format compatible. |
| An active backup already exists | Use Undo changes first. Do not delete the backup manually to bypass the check. |
| A file changed | Finish or pause the update, keep the backup and try undo. Do not overwrite another tool's change. |
| Stopping or recovering | Wait for the final result. Recovery finishes before the window closes. Keep backups if an external change or lock prevents it. |
| Assets folder incomplete | Extract the complete ZIP again, or place the standalone EXE alone in a new folder. |
| Cached resource changed | The error identifies the resource version folder. Move that folder aside so the tool can recreate its own assets. Do not move client profiles. |
| Existing Chrome performance policy | The tool will preserve settings owned by an administrator or another tool. Undo this tool's own profile if a backup exists; do not delete company policies. |
| Chrome looks managed | The four local policies can trigger Chrome's managed indicator. Undo changes removes this tool's policies when they still match its journal. |

## What to expect afterward

Text and controls should remain readable with less recognized decoration. Check messages, dialogs, keyboard navigation and the functions you use. For Discord check voice, microphone, screen sharing and media; for Steam check library, downloads and game launch.

CSS selectors change when clients update. Finish updates before applying, undo before updating when possible, and check a new version again. Functional testing with real clients remains necessary; automated fixtures cannot certify a voice call or game session.

Memory savings are not guaranteed. The target is 2048 MiB available after Windows, assigning at most 1536 MiB to all three apps with 512 MiB spare. That target is unverified. A game needs its own memory budget. The [earlier desktop observation](BASELINE.md) showed about 1 GiB of Discord resident memory with the previous patch installed; it is not a controlled before-and-after result. Read the [performance guide](RENDIMIENTO.md) before treating a memory counter as proof.

## Privacy and help

The tool has no account sign-in, token reader, telemetry or report uploader. Detection examines known installation locations and process executable paths. The original clients keep their normal network behavior. Logs and errors can contain local paths: remove usernames and other personal information before sharing. Do not upload profiles, cookies, credentials, complete client archives or .tawop backups.

Reports should include the tool version, client version, action, exact error and expected result. See [Privacy](../PRIVACY.md). Optional public donation addresses are listed separately in [Donations](../DONATIONS.md).
