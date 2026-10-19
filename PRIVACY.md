# Privacidad / Privacy

## Español

El kit inspecciona y modifica únicamente los archivos reconocidos dentro de la carpeta que se elige. Guarda originales y un journal `.tawop/` allí para restaurar. Los recursos CSS/JavaScript propios embebidos se extraen, cuando no hay assets externos, a `%LOCALAPPDATA%\TawopModKit\assets\0.3.0-<hash>`; esa caché no es una copia de los clientes ni de una cuenta.

El código propio no implementa inicio de sesión, lectura de tokens, telemetría ni envío de informes. Los clientes originales conservan sus conexiones y comportamiento de red. Los scripts de build pueden usar las herramientas y servicios necesarios para compilar; las políticas de GitHub y de otros servicios siguen aplicándose al usar sus sitios.

El recolector de rendimiento lee nombres de proceso, tiempos CPU, contadores de memoria y datos del hardware/Windows. Escribe CSV/JSON locales. No lee títulos de ventanas, URLs, historial ni líneas de comando. Los informes de inspección pueden incluir rutas de instalación; los errores y capturas pueden revelar información del equipo.

Antes de publicar un informe, revisá rutas, nombres de usuario, capturas, versiones y datos de hardware. No publiques perfiles, mensajes, credenciales, cookies, ASAR completos ni la carpeta `.tawop/`. Las direcciones de donación de [DONATIONS.md](DONATIONS.md) se publican por decisión expresa del propietario.

La revisión de una entrega debe comprobar la lista de archivos, exclusiones de empaquetado y coincidencia de hashes, y buscar credenciales o rutas personales en el contenido distribuido. Esa revisión describe el alcance que se inspeccionó: **no promete ausencia absoluta de toda información personal o vulnerabilidad**. El código y las pruebas permiten auditar y reportar problemas.

## English

The kit inspects and modifies recognized files inside the folder selected by the user. It keeps originals and a `.tawop/` journal there for restoration. When no external assets are supplied, embedded original CSS/JavaScript assets are extracted under `%LOCALAPPDATA%\TawopModKit\assets\0.3.0-<hash>`; this cache does not copy clients or account data.

Original kit code does not implement sign-in, token access, telemetry or report uploads. The original clients retain their own connections and network behavior. Build scripts may use the tools and services required for compilation; GitHub and other services retain their own policies when their sites are used.

The performance collector reads process names, CPU time, memory counters and hardware/Windows information. It writes local CSV/JSON files. It does not read window titles, URLs, browsing history or command lines. Inspection reports may contain installation paths; errors and screenshots may reveal system information.

Before sharing a report, review paths, usernames, screenshots, versions and hardware data. Do not publish profiles, messages, credentials, cookies, complete ASAR archives or `.tawop/` folders. The donation addresses in [DONATIONS.md](DONATIONS.md) are public at the owner's explicit request.

A release audit should check the file list, packaging exclusions and hash consistency, and scan the distributed content for credentials or personal paths. This documents what was inspected: **it does not promise the absolute absence of all personal information or vulnerabilities**. Source code and tests support further auditing and issue reports.
