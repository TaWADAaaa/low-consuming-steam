# Beginner guide: Low Consuming Steam

## Download and open

Open the [release page](https://github.com/TaWADAaaa/low-consuming-steam/releases/tag/v0.4.0-experimental), expand **Assets** and download **low-consuming-steam-v0.4.0-experimental.zip**. Right-click the ZIP, choose **Extract All**, and open the new folder. Double-click **SteamTawop.exe**. The individual EXE download also works by itself.

You do not need to understand source code or run a command. Developer files are included so the tool can be reviewed and rebuilt. BUILD.cmd is for developers; it is not the app.

The executable is unsigned. Windows may display a publisher or reputation message, and antivirus software can flag unknown files. A ZIP does not remove these checks. Keep Windows protection enabled. If you do not trust a download, stop and review the source or ask someone you trust to help. SHA256 checks establish file identity, not safety certification.

## Use the main button

Save your work and finish any call, game, download or update first. For Discord or Steam, click **Optimize Discord** or **Optimize Steam**. The tool locates the installed client, checks its files, requests a normal shutdown, backs up originals and applies the changes. If it stays in the tray, choose Quit Discord or Exit Steam there and retry; the tool does not force-terminate it. It reopens the client if it was running. Keep the window open until it shows the result.

For Chrome, the button is **Prepare Chromium sources**. It accepts a developer's existing Chromium source folder, checks supported source files and applies the source patch with a backup. It does not optimize installed Google Chrome, download Chromium or compile a browser. Native browser compilation is a separate developer task in [the Chromium guide](../README_CHROMIUM.md).

If automatic detection needs help, click **Choose a different folder**. For Discord you may select the Discord parent folder or a supported app-VERSION folder. The parent can contain several versions; detection must select an unambiguous usable installation. For Steam choose the folder containing steam.exe. For Chromium choose its src folder.

**Details** shows diagnostic information. You do not need to inspect a plan or use a second apply button for normal operation.

## Go back to the original version

Use **Undo changes**. The tool verifies its backups and the current files before restoring. Keep the hidden .tawop folder inside the application folder until undo completes; it contains the originals.

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
| Assets folder incomplete | Extract the complete ZIP again, or place the standalone EXE alone in a new folder. |
| Cached resource changed | The error identifies the resource version folder. Move that folder aside so the tool can recreate its own assets. Do not move client profiles. |

## What to expect afterward

Text and controls should remain readable with less recognized decoration. Check messages, dialogs, keyboard navigation and the functions you use. For Discord check voice, microphone, screen sharing and media; for Steam check library, downloads and game launch.

CSS selectors change when clients update. Finish updates before applying, undo before updating when possible, and check a new version again. Functional testing with real clients remains necessary; automated fixtures cannot certify a voice call or game session.

Memory savings are not guaranteed. The target is 2048 MiB available after Windows, assigning at most 1536 MiB to all three apps with 512 MiB spare. That target is unverified. A game needs its own memory budget. Read the [performance guide](RENDIMIENTO.md) before treating a memory counter as proof.

## Privacy and help

The tool has no account sign-in, token reader, telemetry or report uploader. Detection examines known installation locations and process executable paths. The original clients keep their normal network behavior. Logs and errors can contain local paths: remove usernames and other personal information before sharing. Do not upload profiles, cookies, credentials, complete client archives or .tawop backups.

Reports should include the tool version, client version, action, exact error and expected result. See [Privacy](../PRIVACY.md). Optional public donation addresses are listed separately in [Donations](../DONATIONS.md).
