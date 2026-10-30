# Prueba de carga · selección masiva (sprint 10)

Objetivo: que una vacante con cientos de postulaciones (selección masiva de una EST) responda rápido en la consola.

## Cómo se corre

1. Arranque la API con los datos de ejemplo y la vacante masiva (la crea una sola vez, sin IA, en la primera empresa de ejemplo):

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT='Development'; $env:Ejemplo__Sembrar='true'; $env:Carga__Postulaciones='1000'
   dotnet run --project backend/src/Host/Selvana.Api --no-launch-profile --urls http://localhost:5000
   ```

   La vacante «Operario de producción · selección masiva» queda con 55 % recibidas, 15 % en preselección (con chat), 10 % en pruebas,
   5 % en terna y 15 % descartadas, de cuatro fuentes.

2. Corra la herramienta (entra con un usuario de la consola y golpea en paralelo las lecturas de la selección):

   ```powershell
   dotnet run --project backend/tools/Selvana.Carga -- --contrasena <clave> --concurrencia 8 --segundos 20 --umbral-ms 1500
   ```

   Reporta p50, p95 y máximo por endpoint y sale con código 1 si hay errores o si algún p95 pasa el umbral.
   No escribe nada ni llama a la IA: solo lecturas (proceso, tablero, postulantes, filtros, detalle, tablero de selección y lista de vacantes).

## Resultado (30/10/2026, equipo de desarrollo, PostgreSQL en Docker)

1.000 postulaciones en la vacante.

| Paralelo | Peticiones/s | Errores | p95 más alto | Endpoint más lento |
| --- | --- | --- | --- | --- |
| 8 | 121 | 0 | 156 ms | Proceso (filtro «por decidir») |
| 32 | 172 | 0 | 397 ms | Proceso (página 5, con búsqueda) |

Todo bajo el umbral de 1,5 s. El cuello de botella es la vista del proceso: evalúa en memoria las señales de todas las postulaciones de la
vacante (filtros, chat y pruebas) para calcular el embudo y la sugerencia de cada persona. Con varios miles por vacante convendrá guardar la
sugerencia ya calculada y paginar en la base.
