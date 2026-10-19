# Privacy

The tool detects known installation folders and process executable paths, then inspects only recognized targets. Discord and Steam actions can close and reopen the selected client; originals and a .tawop journal stay inside that installation for undo.

Chrome's GUI action configures four allowed performance-policy DWORD values under HKCU\Software\Policies\Google\Chrome. It does not close Chrome or read its profiles, passwords, cookies, sessions or browsing history. Its own %LOCALAPPDATA%\LowConsuming\Chrome\policy-backup.json contains only policy names, original absence, desired values, hashes and recovery state; it contains no installation path or account data. Existing local managed performance values are preserved. Undo compares current values and leaves external edits intact. These local policies may cause Chrome to display Managed by your organization; they do not enroll the browser in an organization. The advanced Chromium source commands continue to use .tawop backups in the source checkout.

Original code does not implement sign-in, account token reading, telemetry or report uploads. Original clients keep their own connections and network behavior. Build tools and hosting services have their own policies.

When no external assets folder is supplied, original embedded CSS and JavaScript are extracted under %LOCALAPPDATA%\TawopModKit\assets\0.5.0-<hash>. This resource bundle is shared by versions 0.5.0 and 0.5.1. It contains project assets, not client profiles or account data.

The optional performance collector reads process names, CPU time, memory counters and hardware/Windows information and writes local CSV/JSON. It does not read window titles, URLs, browsing history or command lines. Inspection reports, errors and screenshots may include installation paths and system details.

Review paths, usernames, screenshots and hardware details before sharing reports. Do not publish profiles, messages, credentials, cookies, complete ASAR archives or .tawop folders. Donation addresses in [DONATIONS.md](DONATIONS.md) are intentionally public at the owner's request.

A release audit checks the allowlisted file inventory, packaging exclusions, hash consistency and distributed bytes for recognizable credentials or personal paths. Its result describes the inspected scope; it cannot promise an absolute absence of all personal information or vulnerabilities. Public source and tests support further review.
