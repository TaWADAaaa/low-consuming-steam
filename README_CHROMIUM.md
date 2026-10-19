# ChromeTawop: parche de fuentes Chromium

`ChromeTawop.exe` es el **parcheador de fuentes**, compilado desde `src/ChromeMod.cs` y el núcleo Tawop. No incluye un navegador compilado. Su entrada es la carpeta `src` de un checkout completo de Chromium; una instalación de Google Chrome no sirve como entrada.

## Cambios exactos

| Archivo | Edición |
|---|---|
| `chrome/app/theme/chromium/BRANDING` | Producto e instalador `ChromeTawop`; conserva autores, copyright y campos ajenos. |
| `components/performance_manager/user_tuning/prefs.cc` | Ahorro de memoria activado y agresividad alta como valores iniciales de esas dos preferencias. |

El parche exige claves y registros únicos. Si cambia la estructura o hay entradas ambiguas, se detiene. Repetirlo sobre fuentes ya modificadas no produce cambios nuevos. Se preservan UTF-8 con/sin BOM y los saltos de línea existentes. El núcleo Tawop prepara las ediciones, compara los archivos y conserva sus originales para restauración.

Estos valores se usan cuando el perfil no tiene una elección guardada o política administrada. La agresividad alta puede recargar más pestañas al volver; no reduce por sí misma la memoria de la pestaña activa. No se cambia la congelación de pestañas ya presente en la fuente inspeccionada. [Marca Chromium](https://chromium.googlesource.com/chromium/src/+/main/chrome/app/theme/chromium/BRANDING), [preferencias de rendimiento](https://chromium.googlesource.com/chromium/src/+/main/components/performance_manager/user_tuning/prefs.cc).

## Preparar la compilación

`scripts/Build-Chromium.ps1` requiere checkout y dependencias sincronizadas, con el parche ya aplicado. No ejecuta `fetch`, no instala Visual Studio ni descarga Chromium. Prepará la cadena de herramientas según la [guía oficial para Windows](https://chromium.googlesource.com/chromium/src/+/main/docs/windows_build_instructions.md).

Requisitos consultados el 18 de octubre de 2026: Windows x64, Visual Studio 2026 ≥18.0 con desarrollo C++ y ATL/MFC, Windows SDK 10.0.28000.2270 y Debugging Tools ≥10.0.26100.3323, además de Git y depot_tools. Chromium pide al menos 8 GB de RAM, recomienda más de 16 y necesita al menos 100 GB libres en NTFS. La ruta del checkout no debe tener espacios. Estos son requisitos del equipo que compila; no validan el objetivo de consumo al ejecutar el navegador.

Con `git`, `gn` y `autoninja` preparados en PATH:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Chromium.ps1 -SourcePath C:\src\chromium\src -CheckOnly
```

El diagnóstico no inicia una compilación ni crea sus archivos. Para compilar y preparar un candidato en una carpeta nueva:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Chromium.ps1 -SourcePath C:\src\chromium\src -PackagePath C:\releases\ChromeTawop-native
```

## Qué genera el script

Se compila el target real `chrome` en `out\TawopBrowser`, optimizado como release, sin componentes compartidos de desarrollo ni símbolos de depuración. Se mantiene el nombre interno `chrome.exe`, que Chromium utiliza en distintas partes del producto. La marca visible es ChromeTawop. El parcheador `ChromeTawop.exe` y ese navegador nativo son productos distintos.

Para preparar el candidato se consulta `gn desc ... runtime_deps` y se copia cada dependencia existente conservando su estructura. Se rechazan rutas externas y enlaces del sistema. Se incorpora la licencia principal de Chromium y un manifiesto con revisión, hashes de las dos fuentes modificadas y estado de validación. Si GN devuelve una dependencia externa, se conserva el build y se detiene el empaquetado para revisión. [Cálculo de dependencias de GN](https://gn.googlesource.com/gn/+/main/docs/reference.md#runtime_deps-how-runtime-dependency-computation-works).

## Estado y publicación del navegador

Los tests sintéticos comprueban los cambios, repetición, campos ajenos, BOM/CRLF y rechazo de fuentes ambiguas. **No se ejecutó un build completo de Chromium ni una prueba del navegador nativo.** La auditoría de desarrollo no encontró la cadena C++ completa preparada.

Antes de distribuir un navegador hay que compilarlo, abrirlo en una PC de prueba y verificar navegación, video, WebRTC, teclado, lectura de textos y restauración de pestañas. También faltan las mediciones conjuntas con Discord y Steam en cuatro núcleos y con 2 GiB disponibles. [Protocolo de rendimiento](docs/RENDIMIENTO.md).

Una build derivada necesita los avisos de terceros aplicables a su revisión: la licencia principal por sí sola no sustituye todos los avisos de Chromium. Conservá revisión, fuentes/parches y manifiesto al publicarla. El ZIP del kit sólo entrega nuestro parcheador y fuentes propias; no se presenta como un navegador portable validado.
