# Mejoras de Selvana · prueba con clientes simulados (30/10/2026)

Cuatro agentes con perfil de cliente usaron Selvana como si fuera su trabajo durante 30 días, en el ambiente local:

| Agente | Personaje | Informe | Hallazgos | Veredicto |
| --- | --- | --- | --- | --- |
| `jefa-rrhh-cliente` | Patricia Gaitán, jefa de RRHH de una EST (40 a 80 vacantes al mes) | `informe-jefa-rrhh.md` | 32 (JR-) | Hoy no lo compraría; lo probaría con un cliente pequeño |
| `psicologa-seleccion-cliente` | Catalina Ruiz, psicóloga de selección masiva | `informe-psicologa-1.md` | 27 (PS1-) | Hoy no lo compraría para una EST |
| `psicologo-seleccion-cliente` | Julián Ortega, psicólogo que crea ofertas y deja los contratos | `informe-psicologo-2.md` | 26 (PS2-) | Hoy no lo compraría para una EST |
| `empleado-cliente` | Leonardo Montoya, operario que usa el celular | `informe-empleado.md` | 31 (EMP-) | Para buscar empleo sí; para su empleo, todavía no |

**116 hallazgos.** Los cuatro coinciden en que el enfoque es bueno: la IA explica, descartar exige motivo, las alertas legales de la contratación
bloquean de verdad, los datos sensibles están bien cuidados y la lectura de la hoja de vida es excelente. Lo que frena la compra es (1) el riesgo legal
del contrato de misión, (2) el trabajo manual repetido para manejar volumen y (3) la IA que corre sin que nadie la pida. Costo total de IA de la prueba:
menos de US$ 0,02 en 9 acciones; un agente se pasó de su límite porque dos botones llaman a la IA sin avisar.

## Orden propuesto

| Prioridad | Tema | Por qué primero |
| --- | --- | --- |
| **P0 · ya** | Empleador y responsable de datos en una EST · contrato de misión legal · salario mínimo desde la vacante · nadie se sube permisos solo · textos internos expuestos al candidato | Riesgo legal y de seguridad: con esto no se puede vender |
| **P1** | Camino manual y control de la IA en la contratación y la inducción | Rompe la regla del producto («todo lo de la IA tiene camino manual») y gasta sin decisión humana |
| **P1** | Trabajo en volumen: acciones en bloque, ordenar, filtrar, exportar, duplicar y editar vacantes, enlaces por enviar | Es la promesa para EST y selección masiva |
| **P1** | Rigor de la selección: no saltarse etapas, filtros justos, pruebas rotuladas, rol de psicólogo | Selección estructurada y Ley 1090 |
| **P2** | Lo propio de una EST: el cliente pide y aprueba, ANS, misiones de 6 + 6 meses, tablero por empresa usuaria | Diferenciador para el sector |
| **P2** | Portal de la persona: postularse sin archivo, pendientes visibles, cuenta y contraseña, autorización clara | Experiencia del candidato y del empleado |
| **P3** | Detalles de texto, móvil y datos de ejemplo | Pulido |

---

## P0 · Legal y seguridad

### 1. En una EST el empleador es la EST, no la empresa usuaria (Bloquea)
- **Qué pasa:** en las vacantes de misión, la oferta, el contrato («Son partes… Agroindustrial del Tolima, como empleador»), la página del candidato
  («Oferta laboral · Retail Express Colombia»), el chat de preselección, el consentimiento de las pruebas, la autorización de la Ley 1581 y el portal
  («solo la ve Agroindustrial del Tolima») nombran a la empresa usuaria. La propia IA lo advirtió al redactar.
- **Hallazgos:** JR-01, JR-02, PS1-01, PS1-02, PS1-03, PS2-01, PS2-09, EMP-25.
- **Mejora:** en todo lo que ve el candidato y en todo documento legal, «Te contrata **Talento Andino EST** (NIT …) · En misión en Retail Express».
  El responsable del tratamiento de datos es la EST; la empresa usuaria solo aparece como lugar de la misión (y se oculta si la vacante es confidencial).
  El pulso y el clima deben decir de qué empresa preguntan.

### 2. Contrato de misión con lo que exige la ley (Bloquea / Alta)
- **Qué pasa:** no hay campo para la causal de la misión (art. 77 de la Ley 50: labor ocasional, reemplazo o incremento de la producción); faltan
  razón social y NIT de las partes, salario en letras, forma de pago, lugar de firma y dotación; el tipo de contrato no es un campo de la vacante;
  las plantillas no tienen revisión jurídica y el tipo «Aprendizaje» puede estar desactualizado frente a la Ley 2466 de 2025.
- **Hallazgos:** JR-03, JR-15, PS1-18, PS2-12, PS2-13, PS2-26.
- **Mejora:** campos obligatorios «Causal de la misión» y «Tipo de contrato» desde la vacante; datos de las partes desde la empresa (no los escribe
  la IA); minutas base revisadas por un abogado laboral (misión, obra o labor, término fijo, aprendizaje).

### 3. Salario mínimo, jornada y auxilios desde la vacante (Alta)
- **Qué pasa:** vacantes de $800.000 a $1.500.000 de tiempo completo se crean, se aprueban y salen en el portal público; el bloqueo solo aparece al
  contratar. La jornada es texto libre (turno noche sin recargo). La IA metió el rodamiento dentro del salario.
- **Hallazgos:** JR-04, PS2-06, EMP-04, PS2-14, JR-24.
- **Mejora:** la misma regla de salario mínimo (proporcional a la jornada) al guardar, aprobar y publicar; jornada estructurada (horas semanales,
  turnos, nocturno) con aviso de recargos; campo aparte «Auxilios no salariales».

### 4. Nadie se sube permisos solo (Alta · seguridad)
- **Qué pasa:** un usuario con permiso de gestionar usuarios cambió su propio rol y la empresa quedó sin Administrador.
- **Hallazgos:** JR-28.
- **Mejora:** prohibir cambiar el propio rol y exigir siempre al menos un Administrador activo.

### 5. Textos internos para la IA expuestos al candidato (Alta)
- **Qué pasa:** el enlace público de la oferta devuelve `tipoContratoDescripcion`, que trae las instrucciones internas para la IA («Al redactar: deja
  claro quién es el empleador…»); en la consola salen mezcladas con la explicación legal.
- **Hallazgos:** PS1-21, PS2-24.
- **Mejora:** separar «explicación para personas» de «instrucciones para la IA» y no enviar estas últimas fuera de la consola.

## P1 · La IA trabaja solo cuando se la pide, y siempre hay camino manual

- **Qué pasa:** aprobar la contratación lanza el redactor de la oferta y el contrato; si falla, solo hay «Pedir de nuevo a la IA». Cambiar una
  condición borra lo corregido a mano y vuelve a llamar a la IA. El texto editado puede contradecir las condiciones aprobadas y la alerta vieja de la
  IA sigue visible. Crear el empleado lanza el plan de inducción con IA; guardar un cargo sin misión pide el perfil a la IA; cada documento de ingreso
  pasa por la IA. Tres inducciones llevan 5 días «Generando» sin error. «Automático con límites» dice «La IA lo hace sola» cuando son reglas.
- **Hallazgos:** PS2-02, PS1-19, JR-13, PS2-03, PS2-04, PS2-05, PS1-23, JR-12, PS1-22, JR-17, JR-30.
- **Mejora:**
  - Al aprobar la contratación, elegir «Redactar con IA» · «Usar la plantilla del tipo de contrato» · «Escribirla a mano».
  - Cambiar condiciones actualiza solo los datos (salario, fechas) en el texto ya corregido; validar que el texto coincida con las condiciones.
  - Inducción y documentos: la IA solo con su botón; plan base por reglas por defecto; «subir sin leer».
  - Si la IA no responde en unos minutos, pasar a «No se pudo» y ofrecer el camino manual.
  - Violeta y «la IA lo hace» solo para lo que hace el modelo; las reglas se llaman reglas.

## P1 · Trabajo en volumen (selección masiva y muchas ofertas)

| Mejora | Hallazgos |
| --- | --- |
| Descartar en bloque con un motivo común (incluida la sugerencia «Revisar» bajo el umbral) | PS1-04 |
| Ordenar por puntaje, días en la etapa y fecha; filtrar por ciudad, canal, rango de afinidad y resultado de filtros; más de 100 por página | PS1-05 |
| Exportar la vista filtrada, la terna y el informe para el cliente (Excel y PDF) | PS1-06 |
| Al invitar o enviar en bloque, una lista «enlaces por enviar» con botón de WhatsApp (mientras llegan las integraciones) | PS1-07, JR-20, JR-19 |
| «Duplicar vacante» y plantillas por cliente y cargo; elegir un cargo del catálogo copia su perfil aprobado | JR-11, PS2-07 |
| Editar una vacante publicada (cupos, aviso) dejando rastro, sin cerrarla | PS2-10 |
| En masivos, «Seleccionados» por lotes en vez de «terna», comparativo paginado y envío del lote al cliente | JR-22, PS1-17 |
| Bandeja con filtros por tipo, empresa usuaria y vacante, y asignación a una persona | JR-21 |
| Responsable de la selección en cada vacante y filtro «mis vacantes»; fecha requerida de ingreso y fecha de inicio | PS2-21, PS2-22, JR-25 |
| No sugerir pasos imposibles (enviar pruebas en una vacante sin batería) | PS1-08 |

## P1 · Rigor de la selección

| Mejora | Hallazgos |
| --- | --- |
| Para pasar a terna saltándose el chat o las pruebas, pedir motivo; nunca sugerir «Lista para contratar» a quien no cumple un excluyente | JR-07, PS1-09, PS2-08 |
| Filtros: «sin evidencia» no es «no cumple»; usar las respuestas del chat; ciudades del área metropolitana y radio de municipios cercanos | PS1-10, PS1-11, PS2-20 |
| Razón de afinidad que explique por qué alguien saca bajo (lo que más resta) | PS1-12 |
| El resumen del chat solo afirma lo que está en la conversación (revisar también los datos de ejemplo) | PS1-13 |
| Rótulo visible «Prueba en validación: no usar como único criterio»; baremos y percentiles; pruebas por competencias; batería visible al crear la vacante; registrar pruebas externas y el concepto del psicólogo | JR-05, PS1-14, PS1-15, PS2-17, PS2-18 |
| Rol «Psicólogo de selección» con acceso a resultados sensibles; preguntar en el registro si quien compra es de RRHH | JR-06, JR-29 |
| Pedir el motivo antes de abrir un resultado sensible (como ya se hace con la hoja de vida) | PS1-16 |
| Equidad: impacto adverso por grupo (regla de los 4/5) usando las variables protegidas solo para auditar; explicar «promedio 0» | PS1-26 |
| Cerrar una vacante pide confirmación y motivo, bloquea acciones posteriores y avisa a quienes siguen en proceso | PS2-11, PS1-27 |

## P1 · Vinculación

| Mejora | Hallazgos |
| --- | --- |
| PDF de la oferta y del contrato aceptado para RRHH y el trabajador | PS1-20 |
| Aceptación con código de un solo uso al celular o al correo (Decreto 2364 de 2012) | JR-16 |
| Textos con lenguaje neutro o con el género de la persona, numeración de cláusulas fija, sin «el enlace que te enviaremos» dentro del enlace | JR-14, PS1-21, EMP-24 |
| Examen médico «No apto» detiene la contratación hasta que decida una persona; «Apto con restricciones» se compara con el cargo | PS2-15 |
| Caja de compensación radicada = «Revisar», no «Bloquea» (la ARL sí bloquea) | PS1-25 |
| Separar «Vinculado» (ingresó) de «Inducción terminada»; no marcar hechas tareas con fecha futura sin aviso | PS1-24, PS2-25 |
| Reglas en el alta: centro de costo de la sede, clase de ARL coherente, áreas y centros de costo del catálogo | JR-18, PS2-19 |
| Estado «Por ingresar» hasta la fecha de ingreso | PS2-23 |
| Inducción de misión con lo que hace la EST y lo que hace la empresa usuaria (SST en el puesto) | PS1-23, PS2-25 |

## P2 · Lo propio de una EST

| Mejora | Hallazgos |
| --- | --- |
| El cliente (empresa usuaria) pide la vacante por un formulario o enlace y aprueba la terna o el lote | JR-10, PS2-16 |
| Separación de funciones en la requisición: quien la pide no la aprueba (configurable) | JR-09, PS2-16 |
| Tablero de selección por empresa usuaria, con cupos cubiertos contra pedidos y semáforo contra el ANS del cliente | JR-23 |
| Alerta en el expediente cuando una misión pasa de 6 o 12 meses en la misma empresa usuaria | JR-08 |
| Primeros pasos propios de EST (empresas usuarias, tipos de contrato, baterías); «Invite a su equipo» se marca cuando la persona activa la cuenta | JR-27 |
| Advertir cuando el título de la vacante y el cargo del catálogo no se parecen | JR-26 |

## P2 · Portal de la persona (candidato y empleado)

| Mejora | Hallazgos |
| --- | --- |
| Postularse sin archivo: «No tengo hoja de vida» con un formulario corto, y el perfil escrito a mano sirve para postularse con un clic | EMP-02, EMP-03, EMP-16 |
| Todo pendiente cuenta en el inicio (autoevaluación, pulso, pruebas); no dejar a nadie en «Pruebas» sin enlace | EMP-01, EMP-05 |
| Recuperar y cambiar la contraseña; eliminar la cuenta | EMP-10 |
| Autorización con finalidades separadas (este proceso · vacantes similares) y aviso de que una IA lee y ordena; el chat dice que es automático | EMP-09, EMP-20 |
| Validar los datos que propone el empleado (celular, campos obligatorios, ciudad del DANE, listas de EPS, fondos y cajas); corregir o cancelar lo enviado; un solo lugar para el celular | EMP-06, EMP-07, EMP-08, EMP-29, EMP-30 |
| Al subir un soporte: avisar que lo lee la IA, mostrar qué leyó, no marcar «por vencer» un documento recién expedido, avisar si ya está vencido y confirmar antes de quitar | EMP-11, EMP-12, EMP-28 |
| «Ofertas para ti» que de verdad encajen (licencia, vehículo, ciudad primero) y búsqueda sin tildes, con ciudad y sinónimos | EMP-13, EMP-14 |
| Detalle de la vacante con tipo de contrato, si es temporal o en misión, horario, zona y beneficios; sin «· selección masiva» en el título público | EMP-15 |
| Derechos Ley 1581: número de radicado, datos llenos con la sesión, ver mis solicitudes y explicar qué pasa si pido suprimir | EMP-17, EMP-18 |
| Pruebas: confirmar «Prefiero no presentarlas» y guardar el avance si se cae la señal | EMP-19 |
| Celular: 4 o 5 pestañas abajo; PDF de la hoja de vida que se descarga o comparte; «¿Reemplazar o agregar?» al leer una hoja de vida nueva | EMP-21, EMP-26, EMP-27 |
| Autoevaluación con palabras en los extremos de la escala, objetivos con quién y cuándo midió, y evidencias | EMP-22, EMP-23 |
| Trato de tú en todas las preguntas que ve el candidato | EMP-31 |

## P3 · Datos de ejemplo y pulido

- Fechas futuras en historiales de la vacante masiva (JR-32), resumen de chat con respuestas inventadas (PS1-13) y candidatos en «Pruebas» sin aplicación
  (EMP-01): revisar la siembra antes de una demo con clientes.
- Riesgo ARL «III» con error genérico (JR-31).

## Lo que más les gustó (no tocar)

- Los filtros por reglas descartan en bloque con su motivo (89 en dos clics) y descartar a mano exige motivo.
- La IA explica sus sugerencias y advierte errores (detectó el del empleador).
- Las alertas legales de la contratación bloquean de verdad (ARL desde el día anterior al ingreso).
- Datos sensibles cifrados y con acceso por rol; apertura de la hoja de vida con motivo y bitácora.
- La lectura de la hoja de vida con IA (unos 20 segundos, muy buena) y un portal claro para buscar empleo.

## Cómo repetir la prueba

Los agentes están en `.claude/agents/` (`jefa-rrhh-cliente`, `psicologa-seleccion-cliente`, `psicologo-seleccion-cliente`, `empleado-cliente`) y siguen
`docs/pruebas-usuarios/guia-de-pruebas.md`. Después de corregir un bloque, vuelva a lanzarlos para confirmar que el hallazgo se cerró.
