# Privacy audit: Low Consuming Tools v0.5.1

Prepared on October 19, 2026. The inspected scope is the three staged release folders for Discord, Steam and Chrome: 62 files per project, 186 files total, before adding this audit record.

## Results

- An independent byte scan checked source and binary content as UTF-8 and UTF-16LE. No unexpected local profile path, Windows username, machine identifier, email address, recognizable access token, private key or real credential assignment was found. Public repository URLs and synthetic security-test URL values were reviewed as intentional test data.
- Each file inventory matches the explicit packaging allowlist. There is one project executable per folder, with original project source, guides, tests and assets. No private runtime folders, upstream client archives, account profiles, backups, unexpected files or reparse links were found.
- Each executable matches its SHA256 in the version 0.5.1 manifest. Each manifest records passing automated tests and exactly one selected product. The PE debug directories are empty, so no PDB path is embedded there.
- Staged source, test and asset bytes match the canonical source used for this revision. Build output, benchmark harnesses, raw measurements, screenshots and local antivirus logs remain outside the public package.
- Public repository owner links are intentional. Each supplied BTC, SOL and USDT receiving address appears once in its project's DONATIONS.md with its requested network. These are authorized public receiving addresses, not private keys.
- The source comment-line check found no explanatory inline comments. Functional directives and format-marker strings remain where needed.
- Microsoft Defender custom scans of all three staged folders returned exit code 0 and reported no threats on October 19, 2026.

## Packaging and limits

The package script uses an explicit public-file allowlist and verifies the executable hash before creating a standard ZIP. Account data, proprietary clients, .git history, .tawop backups, reports, local validation output and build intermediates are excluded. Final ZIP inventories and downloadable asset hashes are checked separately before publication.

Discord and Steam inspect recognized application files and can request normal client closure. Chrome's installed-browser action only writes four allowed current-user performance policies and its own recovery journal; it does not read browser profiles, passwords or sessions. The tools do not implement telemetry, report uploads or account sign-in. Original clients retain their own network behavior.

The audit covers these staged release files. Pattern scans and a point-in-time antivirus result cannot certify the absence of every secret, vulnerability or future detection. Executables are unsigned; Windows reputation checks or antivirus software may still warn. SHA256 establishes file identity, not safety certification. Windows protection is not disabled or bypassed.

Real-client RAM, CPU and GPU savings, all client versions, a complete native Chromium browser build, and the combined 2 GiB / four-core target remain unverified. Public source and automated tests support further review.
