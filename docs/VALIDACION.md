# Validación de Low Consuming Steam v0.3.0

Preparado el 18 de octubre de 2026. Entrega experimental. Las pruebas de parches y los resultados de los clientes son evidencias distintas.

Esta publicación compila un único ejecutable: `SteamTawop.exe`. El motor compartido se prueba con recursos embebidos, ASAR, Steam, Chromium, restauración, interfaz, 16 pruebas del preload y pruebas del recolector. `BUILD-MANIFEST.json` debe identificar sólo ese ejecutable y su hash. Un build y sus fixtures aprobados no acreditan el presupuesto de las apps modificadas.

## Pruebas que se pueden repetir

| Comprobación | Evidencia y alcance |
|---|---|
| Compilación de `SteamTawop.exe` | `scripts/Build.ps1 -Product Steam`, compilador .NET Framework; su resultado y hashes deben corresponder al manifiesto de esta versión. |
| Recursos embebidos | `tests/AssetStore.Tests.cs`: extracción sin assets externos, reutilización, concurrencia, hashes, recurso faltante, caché alterada sin sobrescritura y precedencia/carpeta externa incompleta. Se ejecuta con la suite del motor. |
| Archivo ASAR | Fixtures propios: Unicode, bloques SHA256, corrupción, rutas, offsets y conservación de payload ajeno. |
| Parche Chromium | Fixtures de marca y preferencias, repetición, BOM/CRLF, campos ajenos y rechazo de fuentes ambiguas. No es un build del navegador. |
| Steam | Fixtures de hojas de estilo y preservación de bytes originales. No ejecuta el cliente. |
| Respaldo y restauración | Fixtures temporales: conflictos, archivos bloqueados y respaldo alterado. No modifica instalaciones personales. |
| Ventanas del kit | `tests/GuiSmoke.cs` renderiza fuera de pantalla a 900×730, 740×640 y 580×450. La revisión visual sigue siendo necesaria. |
| Preload de Discord | `tests/Preload.Tests.js`, con Node de desarrollo; DOM/multimedia simulados. No usa una cuenta ni certifica una llamada real. |
| Recolector | `tests/Measure.Tests.ps1`: agrupación, umbral, lecturas inválidas, CPU, sumas, apps ausentes y escenario web sintético. Suite completa aprobada localmente en esta revisión. |
| Contraste de paleta | Cálculo sRGB/WCAG con los colores propios documentados en [RENDIMIENTO.md](RENDIMIENTO.md). No es una certificación de todas las pantallas de los clientes. |

La suite completa del kit necesita Node en PATH para las pruebas del preload. Los EXE de uso final no necesitan Node. `Build.ps1 -Product Steam -SkipTests` compila sin esa suite, pero no debe anunciarse como una compilación validada. El modo `tests/Measure.Tests.ps1 -SkipIntegration` conserva pruebas unitarias y evita la muestra real; la ejecución completa añade una muestra de lectura de un segundo y casos sintéticos con archivos en TEMP.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1 -Product Steam
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Measure.Tests.ps1
node .\tests\Preload.Tests.js
```

Las pruebas de medición aprobadas no necesitan que las tres apps estén abiertas: una app ausente debe producir observación incompleta, nunca una aceptación falsa. El caso web usa datos sintéticos conocidos. Los resultados de TEMP y sus rutas personales se excluyen de la distribución.

## Inspecciones históricas y límites

En v0.2.0 se prepararon parches en memoria sobre Discord 1.0.9262: dos ASAR, 5384473 bytes originales totales. Steam, frontend changelist 11123049, preparó cinco CSS con 9960028 bytes originales. Se inspeccionó sin escribir clientes reales ni crear un respaldo activo. Esas inspecciones describen compatibilidad estructural de esa revisión; no validan automáticamente los cambios visuales de v0.3.0.

Preparar bytes en memoria no demuestra que un cliente arranque, se vea correctamente, reproduzca voz ni consuma menos. Las clases CSS, preload y formato de archivos pueden cambiar con actualizaciones.

## Verificaciones pendientes del producto

| Verificación | Estado |
|---|---|
| Ejecutar Discord y Steam reales con los parches nuevos | Pendiente. |
| Voz 30 minutos, multimedia, transmisión, biblioteca y diálogos | Pendiente. |
| Build completo y arranque de Chromium nativo | Pendiente; no se distribuye un navegador compilado. |
| Pruebas de actualización y restauración en clientes reales | Pendiente. |
| RAM ≤1536 MiB conjunta, margen ≥512 MiB y CPU/respuesta propuestas | Sin demostrar. |
| Juego con voz en hardware de cuatro núcleos bajo presión de memoria | Pendiente. |
| Teclado, lector de pantalla y escalas 100/150/200% en clientes reales | Pendiente. |
| Workflow remoto y publicación GitHub | Comprobar en la página de Actions y la release de esta entrega. |

La aceptación necesita tres repeticiones por escenario con los mismos datos y hardware, cobertura completa y prueba funcional. No se publican porcentajes de ahorro estimados como resultados. Consultá el [protocolo de rendimiento](RENDIMIENTO.md) antes de interpretar un veredicto del recolector.
