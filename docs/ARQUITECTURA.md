# Arquitectura de Low Consuming Steam

Low Consuming Steam v0.3.0 publica un parcheador: `SteamTawop.exe`. El código Tawop compartido contiene el motor local y módulos para Discord, Steam y Chromium; esta entrega selecciona sólo Steam al compilar. La interfaz usa Windows Forms de .NET Framework: no inicia un navegador adicional para mostrar el parcheador. Las secciones de otros módulos explican las fuentes comunes, no representan ejecutables adicionales incluidos.

Las explicaciones de diseño viven en esta guía; las fuentes usan nombres explícitos y formato consistente, sin comentarios de documentación insertados en el código. Las directivas de compilación, como `#if` de C# y `#requires` de PowerShell, sí son necesarias para ejecutar correctamente el proyecto.

## Mapa del proyecto

| Archivo o carpeta | Responsabilidad |
|---|---|
| [src/Program.cs](../src/Program.cs) | Ventana, selección de carpeta, CLI y presentación del plan. |
| [src/ModCore.cs](../src/ModCore.cs) | Validación de rutas, plan, hashes, journal, reemplazo y restauración. |
| [src/AssetStore.cs](../src/AssetStore.cs) | Recursos embebidos, preferencia de recursos externos y caché verificada. |
| [src/AsarArchive.cs](../src/AsarArchive.cs) | Lectura, verificación y reconstrucción de archivos ASAR. |
| [src/DiscordMod.cs](../src/DiscordMod.cs) | Reconocer el cliente admitido y preparar su modificación local. |
| [src/SteamMod.cs](../src/SteamMod.cs) | Encontrar hojas de estilo admitidas y preparar el tema local. |
| [src/ChromeMod.cs](../src/ChromeMod.cs) | Cambiar marca y valores iniciales de rendimiento en fuentes Chromium. |
| [assets/](../assets/) | CSS, preload y marca gráfica propios. |
| [scripts/](../scripts/) | Build del kit, empaquetado, build opcional Chromium y recolección de métricas. |
| [tests/](../tests/) | Fixtures y pruebas del motor, formatos, restauración e interfaz. |

## Ejecutable individual y recursos

Cada EXE incorpora los tres recursos propios `discord/tawop.css`, `discord/preload.js` y `steam/tawop.css`. `AssetStore` da prioridad a una carpeta `assets/` externa junto al ejecutable; si existe, exige los tres archivos para permitir personalización explícita sin mezclar versiones.

Si no hay carpeta externa, extrae los recursos embebidos a `%LOCALAPPDATA%\TawopModKit\assets\0.3.0-<hash16>`. El nombre incluye un hash del conjunto y se comprueba SHA256 de cada archivo al extraer y al reutilizarlo. Un mutex coordina instancias concurrentes; las escrituras preparan un archivo temporal antes de moverlo. Se rechazan rutas enlazadas y recursos ausentes o fuera del límite admitido.

La caché contiene sólo CSS/JavaScript propio del kit, no archivos de Discord, Steam, Chromium ni datos personales. Un archivo de caché alterado produce un error sin sobrescribirlo. El mensaje indica la carpeta de esa versión para moverla y permitir regeneración; editar `assets/` externa completa es la vía para personalizar los estilos.

## Desde un análisis hasta un parche

**Analizar archivos** lee la carpeta elegida y prepara las ediciones en memoria. El informe muestra archivos, tamaños y hashes anteriores/posteriores. Que se pueda preparar una edición no demuestra que el cliente vaya a arrancar después.

**Aplicar parches** exige revisar primero el análisis desde la ventana. El motor comprueba que los originales no hayan cambiado, conserva las copias exactas y guarda un journal en `.tawop/` dentro de esa instalación. Prepara cada reemplazo en el mismo directorio del archivo destino y lo sustituye por archivo. Una instalación completa no se modifica en una única transacción atómica.

Si falla una parte, intenta restaurar los cambios ya escritos. Un bloqueo o cambio externo puede impedirlo; el journal conserva información para reintentar. **Restaurar originales** comprueba tanto el respaldo como los archivos actuales. Si una actualización ajena cambió un archivo, lo conserva y deja el respaldo pendiente. No borres `.tawop/` antes de resolverlo.

Los parches requieren clientes cerrados y sin actualización en curso. El kit no finaliza procesos, no inicia sesión, no lee tokens y no instala servicios. La CLI comparte el mismo motor; sus opciones son `--inspect`, `--apply` y `--restore`.

## Discord

El módulo reconstruye los ASAR compatibles después de verificar estructura, rutas, offsets y hashes SHA256, incluidos bloques de integridad. Mantiene el contenido ajeno a los cambios. Inyecta CSS propio y añade un preload al flujo principal reconocido.

El EXE del cliente no se parchea. Si el fuse de validación de integridad ASAR embebida está activado o no se puede reconocer con certeza, el módulo rechaza la instalación. El formato puede cambiar entre versiones del cliente y requiere una nueva prueba antes de admitirse.

El preload se limita a la presentación y al control de multimedia decorativa identificado. Pausa únicamente videos conectados con autoplay, loop, mute, duración finita positiva, sin controles ni `srcObject` cuando la ventana está oculta. Al mostrarla intenta reanudar el mismo recurso si sigue cumpliendo esos filtros. Usa eventos y `WeakMap`, sin polling ni observador global de mutaciones. No sustituye autenticación, voz, protocolo de mensajes ni motor Electron. Las pruebas sintéticas no certifican esas funciones; se evalúan con el [protocolo funcional](RENDIMIENTO.md).

Se ocultan `.avatarDecoration__44b0c`, `.avatarDecoration_c19a55`, `.profileEffects__0dd08` y clases concretas de destellos. No se oculta el avatar base ni se desactivan globalmente animaciones de carga/voz. Los selectores se contrastaron con hojas CSS públicas de Discord; son nombres de implementación, no una API estable.

## Steam

Se añaden reglas CSS propias a hojas del frontend admitidas. Se conservan los bytes anteriores en el respaldo y el motor del cliente sigue siendo el original. El parche no incorpora archivos de Steam al repositorio ni cambia el backend, DRM, descargas o ejecutables de juegos.

Se ocultan `.OpenGraphImgBackground`, copias `.productImage.productImageBlur` dentro de invitaciones concretas y `.Throbber_Blur`; permanece el indicador de carga funcional. Se verificaron esas estructuras en CSS del frontend changelist 11123049. Los selectores de clases pueden dejar de coincidir cuando Steam cambia su interfaz. Una regla visual que ya no coincide puede dejar de ahorrar dibujo sin que fallen los tests sintéticos. La biblioteca, los menús, las descargas y los diálogos deben comprobarse tras una actualización.

## Chromium

`ChromeTawop.exe` recibe un checkout de Chromium, no una instalación de Google Chrome. Cambia dos archivos: `BRANDING` y el registro de dos preferencias de ahorro de memoria. No genera por sí solo un navegador. El build nativo y su validación están descritos en [README_CHROMIUM.md](../README_CHROMIUM.md).

## Qué implica simplificar assets

Ocultar decoración reduce su participación en el dibujo, pero no equivale a eliminar las funciones que la originan ni a borrar su coste del motor. `display: none` quita un elemento del árbol de layout; `content-visibility` permite omitir trabajo de renderizado bajo sus condiciones. [Chrome: pipeline de renderizado](https://developer.chrome.com/blog/inside-browser-part3), [Google: content-visibility](https://web.dev/articles/content-visibility).

Una imagen ya solicitada o un recurso cargado por JavaScript puede seguir ocupando memoria aunque se oculte. Para impedir una descarga hay que controlar cómo se crea o carga el recurso; la política `loading="lazy"` es distinta de ocultar con CSS. Tawop no promete ahorro de red sólo por aplicar un estilo. [Google: carga de imágenes](https://web.dev/articles/browser-level-image-lazy-loading).

Transparencia tampoco equivale a menos trabajo. El compositor une las capas visibles y las capas adicionales consumen memoria. Por eso Tawop prioriza superficies simples, pocos efectos y ausencia de blur decorativo; cualquier transparencia se reserva a decoración donde no afecte lectura. El resultado de rendimiento debe medirse en el cliente real. [Google: composición y memoria de capas](https://web.dev/articles/stick-to-compositor-only-properties-and-manage-layer-count).

## Alcance del código abierto

MIT cubre el código, estilos, pruebas, herramientas y marca Tawop publicados aquí. Los clientes Discord y Steam siguen siendo software de terceros y sus motores no forman parte de este repositorio. Chromium tiene su propia licencia y avisos. Consultá [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md) antes de distribuir una build derivada.
