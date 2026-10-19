# Privacy

The tool detects known installation folders and process executable paths, then inspects only recognized patch targets. Its automatic action can close and reopen the selected installed client. It keeps originals and a .tawop journal inside that installation for undo.

Original code does not implement sign-in, account token reading, telemetry or report uploads. Original clients keep their own connections and network behavior. Build tools and hosting services have their own policies.

When no external assets folder is supplied, original embedded CSS and JavaScript are extracted under %LOCALAPPDATA%\TawopModKit\assets\0.4.0-<hash>. This contains project assets, not client profiles or account data.

The optional performance collector reads process names, CPU time, memory counters and hardware/Windows information and writes local CSV/JSON. It does not read window titles, URLs, browsing history or command lines. Inspection reports, errors and screenshots may include installation paths and system details.

Review paths, usernames, screenshots and hardware details before sharing reports. Do not publish profiles, messages, credentials, cookies, complete ASAR archives or .tawop folders. Donation addresses in [DONATIONS.md](DONATIONS.md) are intentionally public at the owner's request.

A release audit checks the allowlisted file inventory, packaging exclusions, hash consistency and distributed bytes for recognizable credentials or personal paths. Its result describes the inspected scope; it cannot promise an absolute absence of all personal information or vulnerabilities. Public source and tests support further review.
