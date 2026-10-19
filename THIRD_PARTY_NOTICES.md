# Avisos de terceros

El código del kit, los temas, el preload agregado y los iconos Tawop fueron escritos para este proyecto. Los ejecutables del kit usan .NET Framework del sistema operativo; no se redistribuye ese runtime. El lector ASAR es una implementación propia del formato, no una copia de `@electron/asar`.

Las pruebas JavaScript usan Node sólo como herramienta de desarrollo. Los ejecutables de uso final no incluyen Node, Electron ni los motores de los clientes. Los assets gráficos Tawop se crean con formas propias; el parche de Steam no añade un logo flotante o una imagen decorativa al fondo.

El paquete no incluye ejecutables, DLL, bundles, CSS ni imágenes de Discord, Steam o Google Chrome. Tampoco incluye el checkout completo de Chromium. Los parches se generan localmente usando la instalación/fuentes que seleccione quien ejecuta el kit. Los respaldos y archivos resultantes conservan la licencia del software de origen y no quedan cubiertos por MIT del kit.

- [Formato ASAR de Electron](https://github.com/electron/asar): documentación pública del formato de archivo utilizado.
- [Fuses de Electron](https://www.electronjs.org/docs/latest/tutorial/fuses): comprobación de `EnableEmbeddedAsarIntegrityValidation`, sin alterar fuses.
- [Términos de Discord](https://discord.com/terms): licencia del cliente y componentes; los archivos inspeccionados no constituyen una entrega de código abierto de Discord.
- [Acuerdo de Steam](https://store.steampowered.com/subscriber_agreement/): licencia del cliente. Steam y sus marcas pertenecen a sus titulares.
- [Chromium](https://www.chromium.org/), [licencia principal](https://chromium.googlesource.com/chromium/src/+/main/LICENSE), [marca](https://chromium.googlesource.com/chromium/src/+/main/chrome/app/theme/chromium/BRANDING) y [preferencias](https://chromium.googlesource.com/chromium/src/+/main/components/performance_manager/user_tuning/prefs.cc): base para el parche de fuentes.
- [Compilar Chromium en Windows](https://chromium.googlesource.com/chromium/src/+/main/docs/windows_build_instructions.md): las builds futuras necesitan sus avisos y licencias de terceros completos. La licencia principal no los sustituye.

Tawop es un proyecto independiente. Los nombres Discord, Steam y Chrome se usan para identificar los objetivos de los parches; no indican afiliación ni respaldo de sus titulares.
