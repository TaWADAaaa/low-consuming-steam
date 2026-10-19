# Low Consuming Steam

Herramienta abierta para aplicar modificaciones locales a Steam en Windows. Incluye código fuente propio, guías, pruebas y el ejecutable **`SteamTawop.exe`**.

**Estado experimental.** Es un parcheador: conserva el motor original del cliente. En el caso de Chrome, modifica fuentes de Chromium y todavía requiere compilar y probar el navegador. El objetivo conjunto de Discord, Steam y navegador con **2048 MiB disponibles después de Windows**, reservando 1536 MiB para esas apps y 512 MiB de margen, **no está demostrado**.

[English](README.en.md) · [Código](https://github.com/TaWADAaaa/low-consuming-steam) · [Descargas](https://github.com/TaWADAaaa/low-consuming-steam/releases) · [Guía en inglés](docs/USER_GUIDE.en.md) · [Donaciones / Donations](DONATIONS.md) · [Privacidad / Privacy](PRIVACY.md)

## Alcance de esta publicación

Modifica hojas CSS admitidas del cliente con superficies planas, foco visible y menos sombras/blur. Oculta copias decorativas difuminadas reconocidas, conservando indicadores de carga funcionales. Biblioteca, descargas, juegos y motor Steam permanecen; deben comprobarse en el cliente real.

Los repositorios públicos del proyecto son [Discord](https://github.com/TaWADAaaa/low-consuming-discord), [Steam](https://github.com/TaWADAaaa/low-consuming-steam) y [Chrome / Chromium](https://github.com/TaWADAaaa/low-consuming-chrome). Este repositorio publica **un solo parcheador: `SteamTawop.exe`**, con sus fuentes y descargas. Las herramientas comparten un motor propio de respaldo y restauración; compartir fuentes no convierte los clientes de terceros en código abierto.

## Descargar y usar

[Descargar SteamTawop.exe](https://github.com/TaWADAaaa/low-consuming-steam/releases/download/v0.3.0-experimental/SteamTawop.exe) · [ZIP con fuentes y guías](https://github.com/TaWADAaaa/low-consuming-steam/releases/download/v0.3.0-experimental/low-consuming-steam-v0.3.0-experimental.zip)

Podés abrir el EXE individual o extraer el ZIP completo para tener fuentes, documentación y recursos editables. El ejecutable incorpora los tres recursos propios del kit. Sin una carpeta externa `assets/`, los prepara en `%LOCALAPPDATA%\TawopModKit\assets\0.3.0-<hash>` y verifica sus SHA256 antes de utilizarlos.

1. Elegí la carpeta de la instalación admitida. Para Chrome elegí `src` de un checkout Chromium, no Google Chrome instalado.
2. Usá **Analizar archivos** y revisá las rutas, tamaños y hashes.
3. Cerrá el cliente desde su bandeja y esperá que termine cualquier actualización.
4. Usá **Aplicar parches**; el kit conserva originales y un journal en `.tawop/` dentro de la carpeta elegida.
5. Abrí el cliente normalmente y comprobá sus funciones. Para volver, cerralo y elegí **Restaurar originales**.

No borres `.tawop/` antes de restaurar. Si una actualización ajena modificó un archivo, el restaurador conserva ese cambio y deja pendiente su respaldo. El reemplazo es atómico por archivo; toda la instalación no es una única transacción.

Los EXE no están firmados digitalmente. Los hashes y el código permiten revisar la entrega; no constituyen una certificación de seguridad o compatibilidad. Si Windows deniega escritura, se necesitan los permisos de la carpeta elegida.

## Recursos simples, textos legibles

Se priorizan superficies planas, contraste, foco de teclado y eliminación de decoración reconocida. Los controles, mensajes, nombres e indicadores funcionales deben conservarse. Los selectores cambian con las actualizaciones y necesitan comprobación en clientes reales.

Ocultar o hacer transparente un asset no garantiza que deje de descargarse, decodificarse o consumir memoria. El kit no elimina los recursos originales del disco; además, los respaldos ocupan espacio. La paleta declarada se comprueba con tests, pero la accesibilidad completa de todas las pantallas sigue pendiente.

Una carpeta `assets/` externa permite personalizar sin recompilar, pero debe contener `discord/tawop.css`, `discord/preload.js` y `steam/tawop.css`. Se rechazan carpetas incompletas para evitar mezclar versiones. Una caché generada alterada produce un error sin sobrescribirla; el mensaje identifica la carpeta que se puede mover para regenerar.

## Compilar y comprobar

Windows 10/11, PowerShell 5.1 y compilador C# de .NET Framework 4.8. La suite completa de desarrollo usa Node en PATH; el ejecutable de uso final no necesita Node ni Electron.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1 -Product Steam
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Measure.Tests.ps1
```

`Build.ps1 -Product Steam -SkipTests` compila sólo este parcheador sin validar la suite. Los fixtures comprueban el motor compartido y formatos; no certifican voz, actualizaciones ni consumo de los clientes reales. Chromium necesita otra cadena de compilación C++ y sus avisos completos. [Guía Chromium](README_CHROMIUM.md).

## Guías y evidencia

| Documento | Contenido |
|---|---|
| [Arquitectura](docs/ARQUITECTURA.md) | Motor de parches, ASAR, recursos embebidos, respaldo y límites. |
| [Rendimiento](docs/RENDIMIENTO.md) | Presupuesto RAM/CPU, contraste y protocolo de comparación. |
| [Validación](docs/VALIDACION.md) | Pruebas repetibles y pruebas reales pendientes. |
| [Contribuir](CONTRIBUTING.md) | Fuentes ordenadas, tests y reportes reproducibles. |
| [Avisos](THIRD_PARTY_NOTICES.md) | Licencias y componentes de terceros. |

Las explicaciones se mantienen en documentación; el código propio usa nombres explícitos y formato consistente, sin comentarios internos de documentación. Se conservan directivas funcionales de compilación.

## Licencia y apoyo

[MIT](LICENSE) cubre el código, estilos, herramientas, pruebas y marca Tawop propios. Los clientes y motores de Discord, Steam, Google Chrome y Chromium conservan sus licencias. Este repositorio no redistribuye clientes propietarios completos.

Si querés apoyar el desarrollo, las direcciones y redes están en [Donaciones](DONATIONS.md). Descargar y usar las herramientas no requiere donar.
