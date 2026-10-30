# Guía para los agentes que prueban Selvana como usuarios

Esta guía la leen los agentes de `.claude/agents/` que simulan clientes reales (jefa de RRHH, psicólogos de selección y empleado).
Su trabajo es **usar Selvana como lo haría una persona durante 30 días** y dejar retroalimentación honesta, concreta y accionable.

## Reglas

1. **Solo el ambiente local.** API `http://localhost:5000`, web `http://localhost:5173`. Nunca el ambiente de Azure.
2. **Presupuesto de IA mínimo.** Cada llamada a la IA cuesta dinero. Cada agente puede hacer como máximo **2 acciones que llamen a la IA**
   (redactar una vacante con IA, «Calificar con IA», pedir al agente redactor la oferta o el contrato, leer una hoja de vida o un documento con IA,
   proponer terna, copiloto del evaluador, análisis de comentarios). Para todo lo demás use los caminos manuales, que existen por regla del producto.
   Anote en su informe cuáles hizo y qué resultó. Si una acción de IA tarda, espere hasta 60 segundos consultando el estado y siga.
3. **No borre ni dañe datos de otros.** Trabaje en su empresa o con los registros que le corresponden. No cambie contraseñas ajenas.
4. **Pruebe como usuario, juzgue como usuario.** Use la API (curl) para hacer lo que haría en pantalla, y lea los componentes del frontend
   (`frontend/src/features/**`) para saber qué ve la persona: textos, botones, pasos, mensajes de error. No proponga cambios de código:
   proponga cambios de producto, de pantalla y de texto.
5. **No invente.** Si algo no lo pudo probar, dígalo. Cite la pantalla, el endpoint o el texto exacto cuando señale un problema.

## Cómo usar la API

```bash
# Entrar a la consola (guarda la cookie)
curl -s -c /tmp/<agente>.txt -H "Content-Type: application/json" -d '{"correo":"...","contrasena":"..."}' http://localhost:5000/api/v1/sesion/ingresar
# Llamar con la sesión
curl -s -b /tmp/<agente>.txt http://localhost:5000/api/v1/...
```

Use un archivo de cookies propio en su carpeta temporal (no comparta cookies con otros agentes). Para textos con tildes envíe el JSON desde un archivo
(`--data @archivo.json`) escrito en UTF-8.

Las rutas están en los archivos de endpoints de cada módulo:

| Módulo | Endpoints | Pantallas |
| --- | --- | --- |
| Sesión, registro, empresa, estructura | `backend/src/Modules/Core/Selvana.Core.Api/CoreEndpoints.cs`, `UsuariosEndpoints.cs` | `frontend/src/features/inicio`, `estructura`, `usuarios` |
| Bandeja | `backend/src/Modules/Core/Selvana.Core.Api/` (bandeja) | `frontend/src/features/bandeja` |
| Vacantes, postulantes, proceso, pruebas, preselección | `backend/src/Modules/Reclutamiento/Selvana.Reclutamiento.Api/ReclutamientoEndpoints.cs` | `frontend/src/features/vacantes`, `pruebas`, `preseleccion`, `candidatos` |
| Portal de empleo y portal de la persona | `.../Reclutamiento.Api/PortalPersonaEndpoints.cs` y los públicos de `ReclutamientoEndpoints.cs` | `frontend/src/features/empleos`, `portal-persona` |
| Vinculación (oferta, contrato, documentos, afiliaciones, inducción) | `backend/src/Modules/Vinculacion/Selvana.Vinculacion.Api/` | `frontend/src/features/vinculacion` |
| Empleados y «Mi equipo» | `backend/src/Modules/Expediente/Selvana.Expediente.Api/ExpedienteEndpoints.cs` | `frontend/src/features/empleados`, `equipo` |
| Desempeño y clima | `backend/src/Modules/Desempeno/...Api`, `backend/src/Modules/Clima/...Api` | `frontend/src/features/desempeno`, `clima` |

Todas las rutas de la API llevan el prefijo `/api/v1`. El contexto de producto está en `CLAUDE.md` y en `docs/Selvana-Plan-de-trabajo.md`.

## Usuarios del ambiente local (contraseña `Selvana-Local-2026`)

| Usuario | Empresa y rol |
| --- | --- |
| `valentina.rios@talentoandino.co` | Talento Andino EST · Gerente RRHH (empresa de ejemplo con datos de todo el proceso y una vacante masiva de 1.000 postulaciones) |
| `andres.gomez@talentoandino.co` | Talento Andino EST · Líder (Comercial y Tiendas) |
| `dueno@selvana.local` | Talento Andino EST · Administrador |
| Portal: `leonardo.montoya506@ejemplo.co`, `fabian.henao500@ejemplo.co` | Empleados con «Mi empleo» en el portal de la persona |
| Portal: `fernanda.patino104@ejemplo.co`, `andres.cardenas504@ejemplo.co` | Candidatos con postulaciones |

## Qué entregar

Un informe en `docs/pruebas-usuarios/informe-<su-agente>.md`, en español de Colombia, con:

1. **Quién soy y qué hice:** su personaje y el diario de los 30 días simulados (semana a semana, qué tareas hizo y con qué pantallas).
2. **Lo que me funcionó:** máximo cinco puntos, concretos.
3. **Hallazgos:** una tabla con cada problema: ID (por ejemplo `JR-01`), etapa del proceso, qué intenté, qué pasó, por qué me estorba en mi trabajo,
   severidad (Bloquea · Alta · Media · Baja), la evidencia (pantalla, texto o endpoint) y la mejora que propongo. **Mínimo 20 hallazgos.**
4. **Lo que no entendí:** palabras, estados o pasos que no son claros para alguien de RRHH (o para un empleado).
5. **Lo que me falta para usarlo en mi trabajo real.**
6. **Uso de IA:** las acciones de IA que hizo y qué tan útiles fueron.
7. **Mi veredicto:** ¿lo compraría o lo recomendaría hoy? ¿Qué tendría que cambiar primero?
