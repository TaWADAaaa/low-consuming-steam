# Contributing

Read [Architecture](docs/ARQUITECTURA.md) for design and [Performance](docs/RENDIMIENTO.md) for measurement criteria.

## Prepare a change

1. Build with scripts/Build.ps1 on Windows. Run the collector tests with tests/Measure.Tests.ps1.
2. Keep each change focused. Use explicit names, consistent indentation and functions with clear responsibilities. Put design explanations in documentation; do not add inline documentation comments. Keep functional directives such as #requires and #if.
3. Test valid inputs and meaningful rejection cases. ASAR changes must preserve hashes, blocks, offsets and unrelated payload. Every patch must compare originals, keep backups and restore without overwriting external changes.
4. Document the observable behavior and evidence. A smaller CSS file or different color does not prove memory savings.

Engine tests use temporary files and must not modify or run personal clients. New client layouts need minimal original fixtures and conservative rejection of ambiguous formats. Real-client checks are a separate phase with exact versions and tasks recorded.

## Interface and assets

Keep the main window in plain English with one primary action, readable text, visible focus and useful progress. Advanced details must not be required for ordinary operation. Keep names, controls and functional indicators. Limit hiding to recognized decoration, without global rules that remove images, video, canvas or aria-hidden content indiscriminately.

Check small windows, 100/150/200% scaling, high contrast, tab order and keyboard activation. Document selectors' inspected client structure and what happens after updates. Do not claim a download is prevented by CSS without measuring requests and decoding.

## Report an issue

Include tool version, Windows version, client version, action, exact error and expected result. Redact screenshots and reports. Do not attach complete archives, bundles, profiles, messages, tokens, cookies or backup folders.

Performance reports need hardware, versions, scenario, duration, coverage, repetitions and reviewed CSV/JSON. Unfavorable and inconclusive results are useful. Do not publish estimated savings as measured results.

Original code and assets are MIT-licensed. Do not add third-party clients, proprietary sources, credentials or account data. Keep dependency notices. A future Chromium distribution must retain its revision, patches and complete applicable notices.
