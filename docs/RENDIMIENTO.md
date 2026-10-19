# Presupuesto de rendimiento

**La meta de 2 GiB disponibles y cuatro núcleos todavía no está demostrada.** Esta guía fija cómo aceptarla o rechazarla. El kit no impone un límite duro de RAM ni hace que CSS reduzca automáticamente la memoria del motor.

## Objetivo que se va a comprobar

Se parte de **2048 MiB libres para aplicaciones después de Windows y las tareas habituales**. Se asigna un máximo observado de **1536 MiB a Chrome/Chromium, Discord y Steam juntos**, con **512 MiB de margen**. El juego, grabación, overlays adicionales y otras apps deben entrar en un presupuesto propio; 512 MiB no garantizan que cualquier juego quepa. Una PC con 2 GiB instalados en total no es este objetivo.

Los umbrales CPU y respuesta de esta tabla son criterios del proyecto, pendientes de evaluación; no son resultados obtenidos ni especificaciones de los fabricantes.

| Criterio | Condición de aceptación propuesta |
|---|---|
| RAM de las tres apps | Pico de working set agregado ≤1536 MiB en cada escenario y las tres apps observadas sin fallos de lectura. |
| Margen real del sistema | ≥512 MiB disponibles durante la sesión final; registrar mínimo con un monitor del sistema. El recolector actual sólo registra inicio/final. |
| CPU en reposo | Media conjunta ≤5% y percentil 95 ≤10% de la CPU total del equipo objetivo. |
| CPU con chat, navegación y voz | Media conjunta ≤25% y percentil 95 ≤40% de la CPU total; excluir el juego de la suma de apps y medirlo aparte. |
| Respuesta al interactuar | Percentil 95 ≤200 ms desde clic o tecla hasta respuesta visible, sin bloqueos >1 s durante las tareas repetidas. |
| Desplazamiento de interfaz | En pantalla de 60 Hz, al menos 95% de cuadros ≤33,3 ms durante el recorrido fijo de chat/biblioteca. Registrar con traza; FPS promedio solo no alcanza. |
| Voz | 30 minutos, sin desconexiones ni cortes perceptibles atribuibles al cliente en la prueba controlada. Registrar también pérdidas y jitter que muestre Discord. |
| Juego con voz y apps abiertas | Comparar la misma escena: media de FPS y 1% low sin empeorar >5% frente al cliente original y sin cierres ni cortes nuevos. |

Se registra el modelo exacto de CPU y si sus cuatro núcleos tienen cuatro u ocho procesadores lógicos. El valor `CpuPercentActualMachine` sirve para el equipo medido. `CpuPercentFourCoreEquivalent` divide tiempo CPU por cuatro de forma aritmética: no predice cómo rendirá otra CPU ni reemplaza hardware de cuatro núcleos.

## Comparación reproducible

1. Registrar Windows, CPU, RAM instalada, GPU, disco, resolución, escala, driver y versiones exactas de clientes. Anotar extensiones, configuración y si hay caché caliente o fría.
2. Conservar cuenta, páginas, servidor/canal, juego, volumen y ruta de prueba. Medir primero los clientes originales. No vaciar working sets ni desactivar servicios para forzar un número menor.
3. Abrir las tres apps, esperar cinco minutos y medir reposo durante cinco minutos. Mantener todas presentes; no sustituir Discord escritorio por web en una comparación del mod de escritorio.
4. Medir diez minutos de tareas activas: recorrido fijo por páginas y biblioteca, veinte cambios de canal y escritura de mensajes. Medir una llamada de treinta minutos, con las ventanas visibles y luego en segundo plano.
5. Medir diez minutos de la misma escena de juego con llamada activa y las otras apps abiertas. Registrar juego, resolución, ajustes, herramienta de frametime y conectividad.
6. Repetir después de aplicar los parches, con las mismas condiciones. Hacer tres repeticiones por variante y escenario, alternando el orden. Guardar CSV, JSON, trazas y observaciones de fallos; publicar también resultados desfavorables.

No limitar artificialmente la RAM de esta PC ni finalizar apps personales para fabricar un test. La aceptación final necesita una máquina/VM de prueba apropiada; una VM sirve para presión de memoria, pero las conclusiones de GPU, audio y FPS necesitan el hardware real correspondiente.

## Recolector incluido

Desde la raíz del kit, con las apps ya abiertas:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Measure-AppBudget.ps1 -Scenario original-idle -DurationSeconds 300 -IntervalSeconds 1 -OutputDirectory .\results\original-idle-01
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Measure-AppBudget.ps1 -Scenario tawop-active -DurationSeconds 600 -IntervalSeconds 1 -OutputDirectory .\results\tawop-active-01
```

Cada salida debe usar una carpeta nueva. `samples.csv` contiene filas por app y una fila `Total` por muestra; `summary.json` incluye hardware, pico, medias, cobertura y veredicto. El script sólo lee contadores de procesos y escribe mediciones. No cambia ajustes de Windows/apps ni lee títulos de ventanas, URLs, historial o líneas de comando. Los resultados pueden contener datos de hardware y versiones: revisalos antes de compartirlos.

Parámetros: `DurationSeconds` (30 por defecto), `IntervalSeconds` (2), `BudgetMiB` (1536), `ReserveMiB` (512), `TargetAvailableMiB` (2048), `Scenario`, `OutputDirectory` y `DiscordMode` (`Desktop` por defecto). `Web` sólo declara un escenario elegido por el operador; el recolector no comprueba la presencia de una sesión de Discord web. Si Discord escritorio sigue abierto, también lo suma.

Los grupos se identifican por nombre de proceso. Chrome incluye `chrome`; Discord incluye variantes Stable, PTB, Canary y Development; Steam incluye `steam`, `steamwebhelper`, `steamservice`, `GameOverlayUI` y sus reportadores de errores. No se valida ruta/firma del proceso. Un navegador renombrado fuera de esos nombres requiere ajustar y probar el recolector.

## Cómo interpretar los números

`KnownWorkingSetMiB` es RAM residente, incluidas páginas compartidas. La suma puede contar algunas páginas más de una vez; si falla una lectura, sólo se conocen valores parciales. `KnownPrivateCommitMiB` es memoria privada comprometida, no RAM privada residente: **no sumes commit y working set**. La memoria GPU, otros programas y el juego no forman parte de esta suma. [Microsoft: contadores de memoria de proceso](https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex).

La CPU usa diferencias de tiempo de CPU por proceso entre capturas y el intervalo real transcurrido. El primer punto es `baseline` y no tiene delta. Un proceso nuevo, terminado o ilegible deja el intervalo como `partial_lower_bound`; procesos que nacen y terminan entre dos capturas pueden pasar inadvertidos. Las capturas no son atómicas. Las medias CPU se ponderan por tiempo e incluyen intervalos parciales, por lo que no certifican un límite si la cobertura es incompleta.

`within_observed_working_set_budget` sólo afirma que las lecturas residentes observadas estuvieron dentro del umbral, con todos los grupos requeridos presentes y sin lecturas de memoria fallidas. `over_observed_working_set_budget` demuestra que lo ya observado excedió el límite. `incomplete_observation` impide aprobar una sesión con apps ausentes o lecturas fallidas. Ninguno certifica voz, latencia, paginación, compatibilidad, rendimiento de GPU o cumplimiento del objetivo completo.

## Legibilidad y uso con teclado

Para texto común se exige contraste ≥4,5:1; para texto grande ≥3:1. W3C considera grande aproximadamente 24 px normal o 18,67 px en negrita. Los colores se evalúan sin redondear contra el umbral; la tabla sólo redondea la presentación. [WCAG 2.2: contraste mínimo](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html).

Colores propios calculados con luminancia relativa sRGB y fórmula `(Lclaro + 0,05) / (Loscuro + 0,05)`:

| Uso | Texto / fondo | Relación calculada |
|---|---|---|
| Texto principal Discord | `#e4f5eb` / `#111713` | 16,058:1 |
| Texto en superficies Steam | `#e4f5eb` / `#101e17` | 15,211:1 |
| Texto secundario | `#c0d1c7` / `#111713` | 11,409:1 |
| Texto atenuado | `#adbbb2` / `#111713` | 9,109:1 |
| Enlace Discord | `#86f2b5` / `#111713` | 13,308:1 |
| Enlace Steam | `#7ce8a6` / `#080d0b` | 13,005:1 |
| Texto blanco en marca Discord | `#ffffff` / `#237647` | 5,594:1 |
| Texto de botones del kit | `#dff7e9` / `#253e31` | 10,275:1 |
| Texto del kit | `#dff7e9` / `#111a17` | 15,726:1 |

El foco `#6affab` sobre `#101e17` alcanza 13,527:1; supera el mínimo 3:1 de indicadores relevantes frente al color adyacente. [WCAG 2.2: contraste no textual](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html).

`node tests/Preload.Tests.js` recalcula el contraste directamente desde los CSS: el mínimo de las combinaciones declaradas de texto/fondo Discord es 6,39:1, y en la superficie Steam es 15,21:1. También prueba texto blanco sobre las tres variantes de marca. Esta prueba detecta regresiones de las variables propias; no inspecciona todos los colores heredados del cliente.

La revisión funcional debe comprobar 100%, 150% y 200% de escala/zoom, ventana de 580×450 y 900×730 para el kit, temas claro/oscuro admitidos, alto contraste, foco visible, orden Tab y activación Enter/Espacio. No se ocultan controles, estado de conexión, mensajes, nombres ni indicadores de voz para bajar números. Los colores de la paleta propia no certifican la accesibilidad completa del cliente ni su contenido remoto.

La interfaz del kit usa Segoe UI de 10 pt y etiquetas descriptivas. Con alto contraste conserva los colores del sistema. Las capturas fuera de pantalla sirven para detectar cortes de texto y distribución, pero no sustituyen una prueba con teclado y lector de pantalla.

## Estado de las evidencias

Consultá [VALIDACION.md](VALIDACION.md) para separar pruebas del motor, inspecciones en memoria y pruebas reales pendientes. El tamaño de los EXE, reducir CSS o cambiar un logo no son pruebas de ahorro de RAM. Una interfaz transparente tampoco prueba que haya menos capas: las capas del compositor tienen coste de memoria. [Google: composición de capas](https://web.dev/articles/stick-to-compositor-only-properties-and-manage-layer-count).
