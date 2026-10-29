# Planeación · Vinculación, Empleados, Desempeño y Clima

Planeación ejecutable de las Etapas 2 a 5 del [plan de trabajo](../Selvana-Plan-de-trabajo.md), pedida el 23 de octubre de 2026 para construir ya los módulos que el menú muestra en «Próximas etapas». No cambia el alcance del plan: lo ordena en bloques que se pueden probar, uno detrás de otro.

## Reglas para estos módulos

- **La IA trabaja, el humano decide.** Contratar, calificar el desempeño, sancionar y retirar siempre los aprueba una persona; la IA prepara el borrador y lo explica.
- **Integraciones al final** (decisión vigente): firma electrónica certificada, WhatsApp, antecedentes en línea (Procuraduría, Contraloría, Policía) y el alta directa en el ERP quedan para la fase de integraciones. Mientras tanto cada bloque deja el **paso manual asistido** (enlace, carga de soporte, archivo de exportación) para que el flujo funcione completo.
- **Un módulo por contexto** (ADR 0001 y 0005): `Vinculacion`, `Expediente` (Empleados), `Desempeno` y `Clima`, cada uno con su esquema de PostgreSQL y sus proyectos Domain, Application, Infrastructure, Api y Contracts. Se comunican por eventos con outbox.
- **Datos sensibles** (examen médico, resultados de clima): cifrados, solo el concepto necesario y acceso por rol. Clima nunca muestra resultados de grupos de menos de 5 personas.
- **Marca Dosel, paginación de 10, esqueletos y botones con carga** en todas las pantallas nuevas.

## Orden de construcción

| Orden | Módulo | Por qué en este orden |
| --- | --- | --- |
| 1 | **Vinculación** | Cierra el ciclo de selección: el candidato de la terna pasa a empleado |
| 2 | **Empleados** (Expediente) | Recibe a los vinculados; desempeño y clima necesitan empleados |
| 3 | **Desempeño** | Evalúa a los empleados del expediente |
| 4 | **Clima** | Encuestas anónimas sobre los empleados y su estructura |

## Estado (28 de octubre de 2026)

| Bloque | Estado |
| --- | --- |
| A1 a A4 · Filtros ATS | Hecho |
| V1 · Contratación y oferta | Hecho, con maestro de tipos de contrato y plantillas (28/10) |
| V2 · Documentos de ingreso · V3 · Alta y afiliaciones | Hechos |
| V4 · Onboarding | Hecho (plan de inducción con IA o plan base, aprobación y seguimiento); encuestas, después con Clima |
| P1 a P3b · Portal de la persona | Hecho y rediseñado; P4 y P5 después de E1 |
| E1 · Expediente digital | Hecho (28/10): módulo `Expediente` y carga de la planta por CSV con previsualización |
| E2 · Actualización inteligente | Hecho (28/10): la IA lee el documento, las reglas proponen y RRHH aplica o descarta |
| **E4 · «Mi equipo» del líder** | **Sigue** |
| Desempeño · Clima | Después |

## 0. Reclutamiento · Filtros ATS (pedido el 23 de octubre de 2026)

Se construye justo después de Vinculación V1 y antes de V2. Un ATS clásico descarta en automático; en Selvana el filtro **propone** y una persona decide.

| Bloque | Qué se construye |
| --- | --- |
| **A1 · Filtros de la vacante** | Requisitos marcados como excluyentes (licencia, curso vigente, estudios, años en el cargo), preguntas filtro con la respuesta que descarta, rango salarial, ciudad, jornada y disponibilidad. La IA propone cuáles son excluyentes desde el perfil del cargo. |
| **A2 · Evaluación de cada postulación** | Reglas y evidencia de la hoja de vida (con sinónimos, no solo palabras exactas): cumple todo, le falta uno o no cumple, con el motivo y su cita. Nunca usa edad, género, foto ni barrio. |
| **A3 · Descarte sugerido** | Los que no pasan un excluyente quedan en «No cumple · descarte sugerido» y van a la bandeja; RRHH aprueba los descartes en bloque o rescata a alguien. Queda en la bitácora. |
| **A4 · Filtros en la consola y en el sesgo** | Filtros por resultado en Postulantes y en el tablero (paginados en el servidor) y, en el reporte de sesgo, cuántos descarta cada filtro. |

## 1. Vinculación (Etapa 2)

**Objetivo:** del candidato aprobado a empleado creado, sin redigitar datos.

| Bloque | Qué se construye ahora | Qué queda para las integraciones |
| --- | --- | --- |
| **V1 · Contratación y oferta** | Maestro de tipos de contrato por empresa (nombre, modalidad legal, cómo se trabaja y plantilla opcional en PDF o Word); se siembran indefinido, término fijo, obra o labor, aprendizaje y misión en temporales. Desde la terna, «Aprobar contratación» (decisión humana) abre el proceso de vinculación con los datos del candidato y la vacante. La IA redacta la carta oferta y el contrato desde plantillas por tipo de contrato (término fijo, indefinido, obra o labor, aprendizaje, misión en temporal) y explica cada decisión; RRHH ajusta y aprueba. El candidato acepta la oferta por su enlace (aceptación electrónica con evidencia: fecha, IP, navegador y huella SHA-256). | Firma electrónica certificada; revisión jurídica de plantillas |
| **V2 · Documentos de ingreso** | Lista de documentos por tipo de contrato. El candidato los sube por su enlace; la IA lee cada uno (cédula, certificado bancario, EPS, AFP, diplomas, certificados) y propone si es válido y si coincide con sus datos; RRHH aprueba en la bandeja. Antecedentes y examen médico con su soporte cargado y el concepto (apto, apto con restricciones, no apto) cifrado. | Pedido y seguimiento por WhatsApp; consulta automática de antecedentes |
| **V3 · Alta y afiliaciones** | Lista de afiliaciones (EPS, AFP, ARL, caja de compensación) con alertas. Al completar todo, «Crear empleado» publica el evento de vinculación: el empleado nace en Empleados y se genera el archivo de alta para el ERP (formato canónico). | Alta directa en el ERP por conector o API |
| **V4 · Onboarding** | Plan de inducción generado por la IA según el cargo (tareas, responsables y fechas), con seguimiento. | Encuestas de 30, 60 y 90 días (van con Clima) |

**Pantallas:** «Vinculación» en el menú (procesos con su avance), detalle del proceso (oferta, documentos, afiliaciones, alta), enlace público del candidato `/vinculacion/:token`.

## 2. Empleados · Expediente vivo (Etapa 3)

**Objetivo:** la hoja de vida del empleado se mantiene actualizada sola.

| Bloque | Qué se construye ahora | Qué queda para después |
| --- | --- | --- |
| **E1 · Expediente digital** *(hecho el 28/10/2026)* | Empleado con datos, contrato, cargo, salario, sede, área, centro de costo y empresa usuaria; historial de cargos, salarios y traslados; carpeta documental con versiones y vencimientos. Lista paginada y ficha del empleado. Entra desde Vinculación y desde la carga del archivo de planta. | Migración asistida de carpetas escaneadas |
| **E2 · Actualización inteligente** *(hecho el 28/10/2026)* | RRHH o el empleado sube un documento; la IA identifica el tipo, extrae los datos y propone el cambio; RRHH aprueba en la bandeja. Alertas de documentos y certificados por vencer. | Campaña anual desde el portal del empleado (P5) |
| **E4 · «Mi equipo» del líder** | Vista dentro de la consola para el rol Líder (no es un portal aparte): su equipo, pendientes y alertas; preguntas en lenguaje natural («¿quién tiene el curso de alturas vencido?»). | — |

## 3. Desempeño (Etapa 4)

**Objetivo:** evaluaciones que se preparan solas a partir de la evidencia; el líder califica.

| Bloque | Qué se construye ahora |
| --- | --- |
| **D1 · Ciclos y formularios** | Ciclos (anual, semestral, trimestral, periodo de prueba, fin de misión); tipos 90°, 180° y 360°; formulario por cargo generado por la IA desde las competencias del perfil, que RRHH aprueba. |
| **D2 · Objetivos** | Objetivos con pesos sugeridos por la IA desde el cargo; avance y evidencias durante el periodo. |
| **D3 · Copiloto del evaluador** | Resumen de evidencias, feedback y avance; borrador de comentarios que el líder ajusta (la calificación la pone el líder); detección de sesgos de calificación (todo alto, todo medio, diferencias entre grupos). |
| **D4 · Calibración y desarrollo** | Matriz 9-box, comité de calibración y plan de desarrollo individual propuesto por la IA desde las brechas. |

## 4. Clima (Etapa 5)

**Objetivo:** medir cómo está la gente y alertar a tiempo.

| Bloque | Qué se construye ahora | Qué queda para después |
| --- | --- | --- |
| **C1 · Encuestas de clima y eNPS** | Encuestas anónimas por enlace; resultados por área, sede o cliente con mínimo de 5 respuestas por grupo. | Envío por WhatsApp |
| **C2 · Análisis de comentarios** | La IA agrupa los comentarios abiertos por temas y sentimiento, sin exponer a nadie. | — |
| **C3 · Pulsos de ingreso** | Encuestas de 30, 60 y 90 días a los vinculados, con alerta temprana de retiro. | — |
| **C4 · Riesgo de rotación** | Señales explicadas (clima, desempeño, ausentismo) con acciones sugeridas. | Modelo predictivo entrenado con retiros reales |

Formación (LMS, plan de capacitación) y Carrera y sucesión siguen en la Etapa 5 del plan, después de estos bloques.

## 5. Portal del candidato y del empleado (pedido el 23 de octubre de 2026; P1 a P3b hechos y rediseñados el 28)

Hoy cada cosa llega por un enlace distinto (preselección, pruebas, oferta y documentos). El portal las reúne en un solo lugar con la cuenta de la persona.

| Bloque | Qué se construye |
| --- | --- |
| **P1 · Cuenta y acceso** | Cualquier persona se registra con correo y contraseña y sube su hoja de vida (la IA la lee como en la postulación); confirma sus datos y acepta la autorización Ley 1581. Sesión propia del portal, distinta de la consola (ADR 0013). Puede actualizar su hoja de vida y sus datos. |
| **P2 · Mis postulaciones** | Cada postulación con su estado en palabras claras (recibida, en revisión, en preselección, finalista, contratada, no seleccionada), sin puntajes internos ni motivos que la expongan. Se unen a la cuenta solo si el correo y la cédula coinciden. |
| **P3 · Mis pendientes** | Una bandeja de lo que la persona debe hacer: chat de preselección, pruebas por presentar, oferta por aceptar y firmar, documentos por subir o corregir. Los enlaces que se envían llevan al portal. |
| **P3b · Ofertas para ti** | Vacantes publicadas que encajan con su hoja de vida (reglas sobre cargos, habilidades y ciudad), para postularse con un clic con la hoja de vida que ya subió. |
| **Hecho además** | Rediseño con la marca (bienvenida, cifras, navegación animada, hoja de vida por partes), hoja de vida en PDF, «Completa tu perfil», «Mi cuenta y privacidad» y sesión visible en el portal de empleo (menú con nombre y tarjeta «Hola, …»). |
| **P4 · Mis documentos** | Subir y reemplazar documentos (cédula, certificados) una sola vez y reutilizarlos en otros procesos de la misma empresa, con su vigencia. |
| **P5 · Empleado** | Al vincularse, el mismo portal pasa a ser el del empleado: su expediente y la actualización de datos (conecta con Empleados E2). |

## Definición de terminado de cada bloque

Funciona de punta a punta en la demo local (5000/5173) con datos sintéticos; pruebas de dominio en verde; lo que produce la IA se muestra en violeta con su explicación y se puede corregir; lo que afecta a una persona pasa por aprobación humana y queda en la bitácora; plan, wiki y Azure actualizados.
