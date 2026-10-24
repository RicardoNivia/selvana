# ADR 0010: Bitácora de auditoría encadenada en Core, alimentada por el outbox de cada módulo

- Estado: Aceptado
- Fecha: 2026-10-22

## Contexto

La Ley 1581 de 2012 y la Superintendencia de Industria y Comercio piden demostrar quién autorizó, cuándo y qué se hizo con los datos de cada titular. En Selvana además hay que distinguir lo que hizo la IA de lo que decidió una persona. La bitácora debe ser confiable (que nadie la altere sin que se note) y completa (que una acción no quede sin registrar ni se registre una que no pasó).

## Decisión

- La bitácora vive en Core (tabla `core.bitacora`). Cada línea guarda quién (persona, IA, sistema o titular), qué, detalle, nota (quién aprobó lo de la IA, motivo de un acceso), módulo, entidad y si fue un acceso a datos personales.
- **Cadena de huellas:** cada línea guarda la huella SHA-256 de la anterior de la misma empresa y la suya propia, calculada sobre todo su contenido. Índices únicos sobre (empresa, huella anterior) y (empresa, huella) impiden que la cadena se bifurque: si dos escrituras compiten, una falla y se reintenta. La consola tiene «Verificar integridad», que recorre la cadena y señala la primera línea alterada o el hueco de una borrada. La fecha se trunca a microsegundos para que la huella sea la misma al releer de PostgreSQL.
- **Solo se agrega:** no hay comandos para editar ni borrar líneas.
- **Misma transacción:** Core escribe sus líneas con su propio `SaveChanges`. Los demás módulos publican `AccionAuditada` (Core.Contracts) en su outbox, junto con el cambio; llega por HTTP al inbox de Core (ADR 0008) y entra a la cadena. Si el cambio no se guarda, la línea tampoco; si Core está caído, la línea llega después.
- **Enrutamiento:** `Selvana.Core.Contracts.Auditoria.*` va al inbox de Core y `Selvana.Core.Contracts.Privacidad.*` al de Reclutamiento; el worker de IA recibe solo `Selvana.Core.Contracts.Estructura.*` y `Selvana.Reclutamiento.Contracts.*`.
- **Accesos a datos personales:** abrir una hoja de vida es un POST con motivo; la API la entrega y deja la línea marcada como acceso a datos personales.
- **Solicitudes de titulares** (consulta, actualización, supresión, revocatoria) en Core, con el plazo legal en días hábiles colombianos (festivos de la Ley 51 de 1983 incluidos). Aprobar una supresión publica `SupresionDeTitularAprobada`; cada módulo borra los datos del titular y deja la constancia de la revocatoria.

## Consecuencias

- La verificación hoy carga toda la cadena de la empresa en memoria; cuando crezca se hará por tramos con puntos de control firmados.
- La huella prueba que nadie alteró la bitácora desde la base de datos de la aplicación; para una prueba ante terceros falta anclar la última huella del día fuera de Selvana (por ejemplo, en un almacenamiento inmutable). Queda para producción.
- Enviar la respuesta al titular por correo o WhatsApp va con las integraciones; mientras tanto la persona la copia.
