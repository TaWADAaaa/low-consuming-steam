# Contribuir a Tawop

La explicación del diseño está en [ARQUITECTURA.md](docs/ARQUITECTURA.md); los criterios para publicar resultados de RAM/CPU están en [RENDIMIENTO.md](docs/RENDIMIENTO.md).

## Preparar un cambio

1. Compilá con `scripts/Build.ps1` en Windows y ejecutá las pruebas del recolector con `tests/Measure.Tests.ps1`.
2. Hacé un cambio con una responsabilidad clara. Usá nombres explícitos, sangría consistente y funciones separadas por tarea. Las explicaciones extensas van en documentación; no agregues comentarios dentro del código. Conservá directivas funcionales como `#requires` y `#if`.
3. Probá entradas válidas y rechazos relevantes. Los cambios ASAR deben mantener hashes, bloques, offsets y archivos no modificados. Todo parche debe comparar el original, conservar el respaldo y permitir restaurarlo sin pisar cambios ajenos.
4. Documentá el efecto observable y la evidencia. Un cambio de color o menos líneas CSS no demuestra ahorro de memoria.

Las pruebas del motor usan archivos temporales; no deben ejecutar ni cambiar clientes personales. Para añadir una variante de cliente, reproducí su estructura con fixtures propios mínimos y rechazá formatos ambiguos. La prueba de los clientes reales es una fase separada y debe declarar versiones y tareas comprobadas.

## Interfaz y assets

Conservá textos, nombres, controles, foco de teclado e indicadores funcionales. Limitá la eliminación a decoración reconocida; no uses reglas globales que escondan imágenes, videos, canvas o elementos con `aria-hidden`. Mantener una interfaz comprensible tiene prioridad sobre bajar un contador.

Verificá contraste de paleta, zoom/escala, alto contraste y ventanas pequeñas. Los selectores de clientes remotos pueden cambiar: documentá qué estructura se verificó y qué ocurre si el selector ya no coincide. No afirmes que CSS evita una descarga sin medir solicitudes y decodificación.

## Reportar un problema

Indicá versión de Tawop, Windows, cliente, archivo que falló, mensaje del parcheador, acción realizada y resultado esperado. Podés incluir una captura sin datos personales y un informe con rutas revisadas. No adjuntes ASAR completos, bundles, tokens, perfiles, conversaciones ni respaldos.

Un reporte de rendimiento debe incluir hardware, versiones, escenario, duración, cobertura, repeticiones y CSV/JSON revisados. Se aceptan resultados que no mejoran o que exceden el presupuesto: son necesarios para decidir el siguiente cambio.

## Qué se puede aportar

El repositorio contiene código y assets propios bajo MIT. No agregues clientes completos, ejecutables o imágenes de terceros, fuentes propietarias, credenciales ni archivos de cuenta. Conservá la licencia y los avisos aplicables de cualquier dependencia nueva. Si publicás un Chromium derivado, conservá revisión, parches y los avisos completos de esa build.
