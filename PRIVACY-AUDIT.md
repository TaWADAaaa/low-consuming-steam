# Privacy audit / Auditoría de privacidad

Review date / Fecha de revisión: 2026-10-18.

The staged release was reviewed before packaging: source code, tests, documentation, workflow configuration, resources, screenshot, build manifest and the single `SteamTawop.exe` download. Searches covered personal profile paths, contact details, common credential patterns and browser/session artifacts. No such information was found in this snapshot. The executable was scanned as ASCII and UTF-16 and contains no debug directory or PDB path record. The screenshot uses a fictional profile path and contains no textual or EXIF metadata.

Se revisó la entrega preparada antes de empaquetarla: fuentes, pruebas, documentación, workflow, recursos, captura, manifiesto y el único ejecutable `SteamTawop.exe`. No se encontraron rutas personales, datos de contacto, patrones comunes de credenciales ni artefactos de sesiones o navegador. Se revisaron cadenas ASCII y UTF-16 del EXE; no contiene directorio de depuración ni registro de ruta PDB. La captura utiliza una ruta ficticia y carece de metadatos textuales o EXIF.

Public repository identity and BTC, SOL and USDT donation addresses are explicitly authorized public information. Address syntax and applicable BTC/EIP-55 checksums were validated locally. Ownership, balances and deposit acceptance were not verified; no transaction was initiated.

La identidad pública del repositorio y las direcciones de donación BTC, SOL y USDT están autorizadas para publicación. Se validaron localmente sus formatos y los checksums BTC/EIP-55 correspondientes. No se verificaron titularidad, saldos ni aceptación de depósitos, y no se iniciaron transacciones.

Private profiles, backups, caches, local measurement output, test artifacts and Git metadata are outside this package. See [the build manifest](BUILD-MANIFEST.json) for executable hashes. This point-in-time review reduces disclosure risk; it is not an absolute guarantee or a complete security audit. Recheck changed files and commit author metadata before publication.

El paquete excluye perfiles, respaldos, cachés, mediciones locales, artefactos de pruebas y metadatos Git. Los hashes están en el [manifiesto](BUILD-MANIFEST.json). Esta revisión puntual reduce riesgos; no constituye una garantía absoluta ni una auditoría de seguridad completa. Revisar cambios posteriores y los datos del autor del commit antes de publicar.
