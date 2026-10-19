# Privacy audit: Low Consuming Tools v0.4.0

Prepared on October 19, 2026. The inspected scope is the three staged release folders for Discord, Steam and Chrome: 52 files per project, 156 files total, before adding this audit record to each project.

## Results

- An independent byte scan checked source and binary content as UTF-8 and UTF-16LE. It found no unexpected local profile path, username, machine identifier, email address, recognizable access token, private key, session value or credential assignment.
- The file inventories contain original project source, guides, tests, assets and one project executable each. No private/runtime directories, account profiles, client backups, unexpected files or reparse links were found.
- Each executable is 86,016 bytes and matches its SHA256 entry in the version 0.4.0 build manifest. The PE debug directory is empty, so no PDB path is embedded there. Each manifest reports passing automated tests and one selected product.
- Public repository owner links are intentional. Each owner-supplied BTC, SOL and USDT receiving address appears exactly once in its project's DONATIONS.md, with the requested network. These are public receiving addresses, not private keys.
- The code comment-line check found no explanatory source comments. Functional directives and format-marker strings remain where needed.
- Microsoft Defender custom scans of all three staged project folders on October 19, 2026 returned exit code 0 and reported no threats. Private scan logs remain outside the publication allowlist.

## Packaging and limits

The package script uses an explicit public-file allowlist and verifies the executable hash before creating a standard ZIP. Account data, original third-party clients, .git history, .tawop backups, reports, local validation output and build intermediates are excluded. Final ZIP inventory and download hashes are checked separately before publication.

The audit covers these staged release files. Pattern scans and a point-in-time antivirus result cannot certify the absence of every secret, vulnerability or future detection. The executable is unsigned; Windows reputation checks or antivirus software may still warn. SHA256 establishes file identity, not safety certification. Windows protection is not disabled or bypassed.

Real-client compatibility checks, a complete native Chromium browser build and the combined 2 GiB memory target remain unverified. Public source and automated tests support further review.
