# Informe de prueba de usuario · Psicóloga de selección (PS1)

Fecha: 30/10/2026 · Ambiente: local (API `http://localhost:5000`) · Empresa: Talento Andino EST S.A.S. · Usuario: `valentina.rios@talentoandino.co` (Gerente RRHH)

## 1. Quién soy y qué hice

Soy **Catalina Ruiz**, psicóloga organizacional con 6 años en selección masiva en una empresa de servicios temporales. Mi día es depurar cientos
de hojas de vida, preseleccionar por teléfono o WhatsApp, aplicar y leer pruebas, armar ternas para los clientes y, cuando aprueban, sacar la
oferta, el contrato y perseguir los documentos de ingreso. Trabajé solo en **Mercaderista** (Retail Express, Medellín), **Cajero de supermercado**
(Retail Express, Bogotá) y **Operario de producción · selección masiva** (1.000 postulaciones, 40 cupos). Usé la API con curl (lo mismo que hacen
los botones) y leí las pantallas de `frontend/src/features/vacantes`, `pruebas`, `preseleccion`, `vinculacion` y `portal-persona`.

### Semana 1 · Depurar volumen (vacante masiva `VAC-261010-94B2`)

- Abrí la pestaña **Proceso** (`GET /reclutamiento/vacantes/{id}/proceso`). Embudo: 550 recibidas (550 por decidir), 150 en preselección esperando
  al candidato, 100 en pruebas, 50 en terna, 150 descartadas. Agente en modo «Sugerir», umbral de afinidad 70, tolerancia salarial +10 %.
  «316 pasos listos para aceptar».
- Repasé las 550 recibidas en páginas de 100 (el máximo): 216 con «Invitar a la preselección» y **334 con «Afinidad por debajo del umbral:
  revísela antes de decidir»**, que no tienen ningún botón propio, solo «Mover a…».
- La vacante **no tenía filtros ATS configurados** («La vacante no tiene filtros»). Fui a **Filtros ATS**: sugería «Bachiller» y la pregunta
  «¿Tienes disponibilidad para turnos rotativos?» como excluyentes y la ciudad Bogotá. Guardé Bachiller + la pregunta + ciudades Bogotá y Soacha +
  6 meses de experiencia (`PUT .../filtros`). En 0,2 s: 755 cumplen, 95 no cumplen (todos por haber respondido «No» a turnos rotativos).
- Propuse los descartes (`POST .../filtros/descartes` → 89) y los aprobé (`POST .../filtros/descartes/aprobar` → 89). Ojo: entre los 89 había
  16 personas que ya estaban en el chat de preselección y 11 en pruebas; 6 que estaban en terna no se propusieron (bien).
- Acepté en bloque la primera página de 100 sugerencias «Invitar a la preselección» (`POST .../proceso/sugerencias` → 100 aplicadas, 0,5 s).
- Intenté aceptar el paso «Enviarle las pruebas» que la IA sugiere a 89 personas en pruebas: la vacante **no tiene batería**; el bloque responde
  `{"aplicadas":0}` sin decir por qué y el paso individual devuelve «Esta vacante no tiene pruebas: elija la batería en Configuración o pase a la
  persona directo a la terna».
- Revisé el **reporte de equidad** (`/sesgo`) y el **banco de talento** de la vacante (vacío).

**¿Puedo depurar 1.000 en una mañana?** La parte de reglas sí (filtros + descartes aprobados en bloque + aceptar de a 100). Pero me quedaron
334 «por revisar» bajo el umbral que solo se pueden descartar **uno por uno, escribiendo el motivo**, y 237 personas en preselección esperando un
enlace que nadie les envió. En una mañana real no termino.

### Semana 2 · Preselección y pruebas (Mercaderista y Cajero)

- Leí la conversación de preselección de Jorge Lozano (Mercaderista) y su resumen. Le envié las pruebas a mano (`POST /postulaciones/{id}/avanzar`
  destino Pruebas) y abrí su enlace público `/pruebas/:token` para ver lo que ve el candidato (consentimiento y las dos pruebas).
- Leí los resultados de la batería de Mercaderista (`/pruebas/resultados`): Atención y precisión con corte 60 y Personalidad (sensible).
  Abrí el detalle de personalidad de un candidato (`POST /pruebas/aplicaciones/{id}/sensible/PERSONALIDAD`).
- Revisé el comparativo de finalistas y el reporte de equidad de la masiva.

### Semana 3 · Terna y contratación (Mercaderista)

- Pasé a Daniel Duque a terna a mano y **aprobé la terna** Camila Toro, Fabián Benítez y Daniel Duque (`POST .../terna?postulaciones=...`), sin
  pedirle la terna a la IA.
- Abrí **Aprobar contratación** para Camila Toro (`GET /vinculacion/contratacion/sugerida`): condiciones prellenadas por reglas (misión temporal,
  $1.800.000, ingreso 09/11/2026, fin 08/05/2027, 36 días de prueba, auxilio de transporte). Aprobé (`POST /vinculacion/procesos`).
- **Al aprobar, la IA redactó sola la oferta y el contrato** (claude-haiku, US$0,0023, ~25 s). El contrato quedó con **Retail Express Colombia
  como empleador**; la misma IA lo advirtió en sus alertas. Lo corregí **a mano** («Ajustar el texto»: carta y cláusula PRIMERA), aprobé la
  oferta y abrí el enlace del candidato `/vinculacion/:token`. Acepté como la candidata con nombre y cédula.

### Semana 4 · Ingreso

- **Camila (Mercaderista):** cargué antecedentes (sin anotaciones) y examen médico (apto con restricciones) desde la consola. Los documentos de
  la candidata no los subí porque cada uno lo lee la IA (4 lecturas = 4 acciones de IA).
- **Santiago Quintero (Cajero, proceso sembrado `VIN-261027-A5FB`):** aprobé sus 4 documentos «sin observaciones» en bloque, cargué antecedentes
  y examen médico, registré EPS, pensión, ARL y caja, completé el alta (centro de costo, área, cuenta) y **creé el empleado**. **Al crearlo, la IA
  generó sola el plan de inducción** (gemini flash-lite, US$0). Lo aprobé, marqué las 11 tareas y cerré: el proceso quedó **«Vinculado»**.
- Probé la alerta de ARL con fecha posterior al ingreso: la bloqueó con la norma citada. Bien.

## 2. Lo que me funcionó

1. **Filtros ATS por reglas con evidencia** y descarte en bloque que aprueba una persona: 89 descartes en dos clics, cada uno con su motivo
   («Respondió No a "¿Tienes disponibilidad para turnos rotativos?"»), sin gastar IA, y no propone descartar a quien ya está en terna.
2. **Criterios visibles en cada sugerencia** (✓/✕ con evidencia: «Puntaje 95/100», «Aspira a $2.025.000 · máximo $2.100.000»). Es selección
   estructurada de verdad: mismos criterios para todos y ajustables en «Reglas de avance».
3. **Cuidado del dato sensible:** la personalidad no sale en la tabla ni en el comparativo; para verla hay que tener permiso y queda en bitácora,
   con la nota «Prueba en validación: … no cuentan para decidir. No hay rasgos buenos o malos». El examen médico nunca pasa por la IA.
4. **Condiciones del contrato prellenadas con su norma** (quinta parte de la misión para el periodo de prueba, auxilio de transporte hasta 2
   SMMLV) y alertas que **bloquean** de verdad (ARL que no cubre desde el día anterior al ingreso, caja sin confirmar, falta centro de costo).
5. **El redactor de la oferta se autocorrige:** sus alertas detectaron que el empleador no era la EST y que la prórroga no tenía fecha. Y la
   edición manual de carta y cláusulas funciona.

## 3. Hallazgos

| ID | Etapa | Qué intenté | Qué pasó | Por qué me estorba | Severidad | Evidencia | Mejora que propongo |
| --- | --- | --- | --- | --- | --- | --- | --- |
| PS1-01 | Contratación (EST) | Aprobar la contratación de Camila en misión | El proceso, la oferta y el contrato ponen a **Retail Express Colombia** (empresa usuaria) como empleador y como «empresa». La candidata ve «Oferta laboral · Retail Express Colombia». | En una EST el empleador es la EST (art. 71 y 77 Ley 50/1990). Un contrato así es nulo o expone a la usuaria a solidaridad; un candidato firma con la empresa equivocada. | Bloquea | `GET /vinculacion/procesos/{id}` → `"empresa":"Retail Express Colombia"`; cláusula PRIMERA de la IA: «Retail Express Colombia, en adelante el empleador»; `GET /portal/vinculacion/{token}` → `"empresa":"Retail Express Colombia"`; pantalla `OfertaCandidato` «Oferta laboral · {o.empresa}» | Si el tipo es misión, empleador = la EST (razón social y NIT) y empresa usuaria aparte, en la carta, el contrato, la pantalla del candidato y el archivo de alta. |
| PS1-02 | Preselección | Leer el chat de Jorge (Mercaderista) | El agente se presenta como «el asistente de selección de **Retail Express Colombia**». La EST no aparece. | El candidato no sabe que su empleador será la temporal; luego le llega un contrato de otra empresa y desconfía. En vacantes confidenciales es peor. | Alta | `GET /postulaciones/{id}/preseleccion`, primer mensaje del agente | Presentarse como «Talento Andino EST, para una vacante en Retail Express» (o «empresa confidencial»). Igual en la vacante pública. |
| PS1-03 | Pruebas · Ley 1581 | Abrir el enlace de pruebas de Jorge | El consentimiento dice «Autorizo a **Retail Express Colombia y a Selvana** a tratar los resultados…». No nombra a la EST, que es quien aplica e interpreta. | El responsable del dato sensible queda mal identificado; la usuaria recibe autorización sobre datos que no debería ver. | Alta | `GET /portal/pruebas/{token}` → `consentimientoTexto` | Nombrar como responsable a la EST, a la usuaria solo si recibirá el informe, y decir quién verá qué (psicóloga sí, cliente solo el concepto). |
| PS1-04 | Depurar volumen | Descartar a los 334 bajo el umbral en la masiva | La sugerencia «Afinidad por debajo del umbral: revísela antes de decidir» no tiene botón ni entra en «Aceptar en bloque». Descartar es uno por uno, con motivo escrito. | Con 334 personas es una semana de clics. Es exactamente lo que la herramienta debía quitarme. | Alta | `ProcesoSeleccion.tsx`: `aceptable()` excluye Descartar y Revisar; comentario «descartar va uno por uno, con su motivo» | Selección múltiple + «Descartar con el mismo motivo» (motivo de una lista, editable), o una regla «bajo X de afinidad → descarte sugerido» que pase por la bandeja como los filtros ATS. |
| PS1-05 | Depurar volumen | Ordenar y filtrar la lista del proceso | Solo hay etapa, «Por decidir» y buscar por nombre o número. No puedo ordenar por puntaje, días en la etapa o fecha, ni filtrar por ciudad, canal, rango de afinidad o resultado de filtros. Máximo 100 por página. | En masivos trabajo por cortes («primero Soacha con más de 80», «los de 10 días sin respuesta»). | Alta | `GET /vacantes/{id}/proceso` acepta `etapa, porDecidir, buscar, pagina, tamano`; `TAMANOS_PAGINA = [10, 25, 50, 100]` | Orden y filtros por columna; «seleccionar todos los que cumplen el filtro» (no solo la página). |
| PS1-06 | Depurar volumen | Exportar la lista o la terna para el cliente | No hay exportar (CSV, Excel ni PDF) en proceso, filtros, finalistas ni banco. | El cliente me pide la terna o el listado de enviados en Excel; hoy tendría que copiar a mano (doble digitación). | Alta | Sin «exportar», «csv» ni «excel» en `frontend/src/features/vacantes`, `candidatos`, `pruebas` | Exportar la vista filtrada y un «informe de terna para el cliente» en PDF sin datos sensibles. |
| PS1-07 | Preselección | Aceptar 100 «Invitar a la preselección» en bloque | Se aplican, pero no recibo ningún enlace. El aviso con el enlace para copiar sale solo al mover a una persona. Los candidatos sin cuenta en el portal nunca se enteran. La masiva ya tiene **237 esperando** en preselección. | Invitar sin avisar no es invitar. El embudo se llena de «esperando» que no saben nada. | Alta | Pantalla: «Aplicados 100 pasos. Quedan en la bitácora con su nombre.»; `AvisoAvance` solo en el paso individual | Mientras llegan WhatsApp y correo: descargar los enlaces del bloque (nombre, celular, enlace) para un envío masivo, o una vista «pendientes de avisar». |
| PS1-08 | Pruebas | Aceptar «Enviarle las pruebas» en la masiva | La IA sugiere enviar pruebas a 89 personas en una vacante **sin batería**. En bloque responde «aplicadas: 0» sin explicar nada; uno por uno da el error «Esta vacante no tiene pruebas…». | Una sugerencia imposible me hace perder tiempo y desconfiar de las demás. | Media | `GET .../proceso?etapa=Pruebas` → `"accion":"EnviarPruebas"` con `tieneBateria:false`; `POST .../proceso/sugerencias` → `{"aplicadas":0}` | Sin batería, sugerir «Pasar a la terna» o «Configure la batería». En bloque, decir cuántos no se aplicaron y por qué. |
| PS1-09 | Selección estructurada | Mirar a quién llevó la masiva a terna | Hay 50 en terna con el chat solo «Invitada», sin aspiración salarial y sin pruebas; 6 de ellos respondieron «No» a turnos rotativos. «Mover a…» deja saltarse cualquier etapa sin pedir motivo. | La selección estructurada se rompe: alguien llega a terna sin la misma evidencia que los demás y nadie sabe por qué. | Alta | `GET .../finalistas` → `preseleccion.estado:"Invitada"`, `pruebas:null`; `nc.json` de filtros: 6 en Terna con NoCumple | Al saltar etapas o pasar a terna con criterios sin cumplir, pedir motivo y dejar la marca «avanzó por excepción» en el comparativo. |
| PS1-10 | Filtros ATS | Ver por qué se descarta a Mariana, Nicolás y Olga en Mercaderista | «No cumple: Moto propia con papeles al día: La hoja de vida no lo demuestra.» Que la hoja de vida no lo diga se toma como «no cumple». Jorge respondió «Sí» a «¿Tienes moto propia?» en el chat, pero los filtros no usan las respuestas del chat. | Descartar por ausencia de evidencia es injusto y hace perder buenos candidatos. Lo correcto es «sin evidencia → preguntar». | Alta | `GET .../proceso` de Mercaderista, sugerencia `Descartar`; `EvaluadorFiltros.Requisito`: si el ranking lo puso en «faltantes» → `NoCumple` | «La HV no lo menciona» → «Sin evidencia: pregúntelo en el chat», y que las respuestas del chat cuenten en los filtros. |
| PS1-11 | Filtros ATS | Usar la ciudad sugerida para la masiva | La sugerencia es solo «Bogotá»; la regla compara el texto exacto. Si la guardo así, las 250 personas de Soacha quedan como «No cumple». | Soacha es área metropolitana de Bogotá: perdería el 25 % del volumen por una regla de texto. | Media | `GET .../filtros` → `"ciudades":["Bogotá"]`; `EvaluadorFiltros`: `filtros.Ciudades.Any(c => ... == ciudad)` | Sugerir el área metropolitana o un radio. Antes de guardar, mostrar cuántos quedarían fuera por ciudad. |
| PS1-12 | Ranking | Leer la razón de afinidad en la masiva | Las 550 recibidas tienen la misma razón principal «Experiencia en producción o bodega», saquen 95 o 30. | Una explicación igual para el mejor y el peor es relleno: no me dice por qué uno tiene 30. | Media | `GET .../proceso?etapa=Recibida` → `razonPrincipal` idéntica en todas | Mostrar la razón que más resta cuando el puntaje es bajo (falta X, experiencia sin fechas…), con la cita de la HV. |
| PS1-13 | Preselección | Comparar el resumen con el chat de Jorge | El resumen dice «¿Tienes moto propia? Sí» y «Respondió sí a las preguntas técnicas», pero en la conversación el agente nunca hizo esas preguntas (preguntó aspiración y fecha de inicio). | Si el resumen afirma algo que no está en la conversación, no puedo confiar en él para filtrar. (Puede ser un dato de ejemplo, modelo «ejemplo (sin IA)», pero el producto debe impedirlo.) | Alta | `GET /postulaciones/{id}/preseleccion` → `mensajes` sin la pregunta; `resumen.respuestas` con ella | Cada respuesta del resumen debe citar el mensaje donde se dijo; si no hay cita, «sin respuesta». |
| PS1-14 | Pruebas | Leer resultados de Mercaderista | La batería es Atención (10 preguntas, 4 min) y Personalidad Mini-IPIP, «en validación», sin baremos ni confiabilidad. La personalidad sale en cuartos (25/50/75) sin percentil ni interpretación. El «total» (provisional) es en la práctica el puntaje de atención. | Con 10 ítems no discrimino a nadie (todos sacan 70 a 90). No puedo presentarle esto a un cliente como evaluación psicotécnica. Lo sé porque está «en validación», pero hoy no me sirve para decidir. | Alta | `GET /pruebas/suite`; `GET .../pruebas/resultados` → `total:70`, `provisional:true`; sensible → `"puntaje":75,50,25` | Prioridad a la validación (fases 2 y 3): baremos por cargo y nivel, confiabilidad publicada y un informe interpretativo para la psicóloga. Permitir cargar resultados de pruebas externas (16PF, Wonderlic…) a mano. |
| PS1-15 | Pruebas · competencias | Relacionar pruebas con las competencias de la vacante | Mercaderista pide «Orden, Orientación a resultados, Autonomía», pero ninguna prueba ni pregunta las mide. No hay instrumento por competencias. | La selección por competencias es el corazón de mi trabajo; aquí las competencias son solo texto de la vacante. | Media | `GET /vacantes/{id}` → `competencias`; `pruebas: ATENCION, PERSONALIDAD` | Un mapa competencia → evidencia (prueba, pregunta del chat o calificación manual con escala) que salga en el comparativo de finalistas. |
| PS1-16 | Datos sensibles | Abrir la personalidad de un candidato | Se abre sin pedir motivo (la hoja de vida sí lo pide). | Para un dato sensible la bitácora debería decir para qué se abrió, igual que la HV. | Media | `POST /pruebas/aplicaciones/{id}/sensible/{codigo}` sin cuerpo; `POST /postulaciones/{id}/hoja-de-vida` exige `Motivo` | Pedir el motivo (lista corta) antes de mostrar el resultado sensible. |
| PS1-17 | Terna | Aprobar la terna que armé a mano | La bitácora registra «Propuesta por el agente de ranking» aunque la IA no la propuso. La terna no es un objeto: no queda «enviada al cliente», no tiene fecha de envío ni respuesta del cliente. En la masiva (40 cupos) el concepto de terna no aplica. El comparativo admite solo 4 personas. | La auditoría queda falsa y no puedo medir «terna enviada → respuesta del cliente», que es mi indicador. | Media | `AprobarTernaHandler`: texto fijo «Propuesta por el agente de ranking»; `ComparativoFinalistas.tsx` `MAXIMO = 4` | Registrar el origen real (IA o persona). Terna con estados (armada, enviada al cliente, respondida) y, para masivos, «lista de enviados» por cupos. |
| PS1-18 | Contratación | Revisar el contrato de misión | No hay campo para la **causal de la misión** (art. 77: ocasional, reemplazo o incremento de la producción); la labor sugerida es «según la necesidad de la empresa usuaria». Faltan NIT y dirección de las partes, salario en letras, forma de pago (en la cláusula de la IA), lugar de firma y dotación. Dice «identificado» para Camila. | Un contrato de misión sin causal es la primera glosa del Ministerio del Trabajo. El resto me obliga a corregirlo cada vez. | Alta | `GET /vinculacion/contratacion/sugerida` → `labor`; cláusulas de la IA; `CondicionesFormulario.tsx` sin campo de causal | Campo obligatorio de causal en misión (con su límite de tiempo); datos de las partes desde la empresa; concordancia de género; validar el contrato con un abogado (la alerta ya lo dice). |
| PS1-19 | Contratación · IA | Aprobar la contratación | Aprobar dispara sola la redacción con IA; el botón no lo dice. Si la IA falla, la pantalla solo ofrece «Pedir de nuevo a la IA»: no hay oferta base por plantilla ni «escribirla a mano». Tampoco puedo agregar ni quitar cláusulas, solo editar las que hay. | Rompe la regla de que todo lo de la IA tiene camino manual, y gasta IA sin que yo lo pida. | Alta | `ProcesoVinculacion.tsx` estado `FallaIa`: único botón «Pedir de nuevo a la IA»; `PUT /oferta` solo edita la lista recibida | Al aprobar, elegir «Redactar con IA» o «Usar la plantilla del tipo de contrato». Oferta base por reglas si la IA falla. Agregar y quitar cláusulas. |
| PS1-20 | Contratación | Descargar el contrato aceptado | No hay PDF de la oferta ni del contrato, ni para RRHH ni para la candidata. Solo una «huella» del texto. | Debo archivar el contrato firmado y entregarle copia al trabajador; el ERP de nómina también lo pide. | Alta | `ProcesoVinculacion.tsx` y `OfertaCandidato.tsx` sin descarga; `GET /portal/vinculacion/{token}` → `huella` | PDF del contrato aceptado con la evidencia (fecha, IP, huella) para ambos, y copia en el expediente. |
| PS1-21 | Oferta · candidato | Leer la carta como candidata | La carta dice «Hola Camila Toro Rincón:» y «acepta la oferta a través del enlace que te enviaremos», cuando ya está en el enlace. La API pública devuelve `tipoContratoDescripcion`, que incluye las instrucciones internas para la IA («Al redactar: deja claro quién es el empleador…»). | Texto que confunde y se siente robótico. Exponer instrucciones internas al candidato no es profesional. | Media | `oferta.carta`; `GET /portal/vinculacion/{token}` → `condiciones.tipoContratoDescripcion` | Saludo con el primer nombre; «acepta aquí abajo»; no enviar la descripción interna al portal público. |
| PS1-22 | Documentos de ingreso · IA | Subir los documentos de Camila desde la consola | Cada documento que sube el candidato o RRHH en su nombre pasa por la IA, sin opción de «subir sin leer» (en Empleados sí existe «Leer con IA»). | En masivos son 4 documentos × 40 personas = 160 lecturas de IA. Si la IA cae, el documento queda «leyendo» y no puedo aprobarlo. | Media | `SubirDocumentoConsolaHandler` publica siempre el evento de lectura; `DocumentosIngreso.tsx`: no deja aprobar mientras está en «Leyendo» | Casilla «Leer con IA» como en el expediente y «Aprobar sin lectura» con mi nombre. |
| PS1-23 | Inducción · IA | Crear el empleado de Santiago | Crear el empleado dispara sola la generación del plan con IA. El plan pone «Evaluar el periodo de prueba… antes de que se cumplan los 30 días» el **día 30** (no antes), y toda la inducción es de la usuaria («políticas de Retail Express»), sin la inducción de la EST. | Gasto de IA no pedido; el plan es de la usuaria, pero la responsable del trabajador en misión es la EST. | Media | `POST /crear-empleado` → `induccion.estado:"Generando"`, modelo gemini-3.5-flash-lite; tarea con `dia: 30` | Elegir «plan con IA» o «plan base» al crear. Evaluar el periodo de prueba unos 5 días antes del vencimiento. En misión, separar la inducción de la EST y la de la usuaria. |
| PS1-24 | Cierre | Llevar el proceso a «Vinculado» | No cierra mientras haya tareas del día 30 sin marcar; me dejó marcar como hechas tareas con fecha futura sin advertencia. | «Vinculado» queda amarrado al fin del periodo de prueba, o se marca en falso para cerrar. | Baja | `POST /onboarding/cerrar` → «Aún hay tareas sin terminar…» | Separar «Vinculado» (ingresó) de «Inducción terminada»; avisar si se marca hecha una tarea futura. |
| PS1-25 | Afiliaciones | Crear el empleado con la caja radicada | «Caja de compensación: radicada, falta la confirmación» **bloquea** crear el empleado. | La caja suele confirmarse días después del ingreso; en masivos esto frena altas legítimas. La ARL sí debe bloquear (y lo hace bien). | Baja | Alertas del alta con nivel `Bloquea` para la caja | Caja radicada = «Revisar», no «Bloquea». |
| PS1-26 | Equidad | Leer el reporte de equidad de la masiva | Solo compara por ciudad y por canal. No hay impacto adverso por género ni por edad (regla de los 4/5) usando esos datos solo para auditar. Y el reporte «descartesPorFiltro» muestra «promedio 0», que no se entiende. | Excluir las variables del puntaje no prueba que no haya sesgo; para eso hay que medir el resultado por grupo. | Media | `GET /vacantes/{id}/sesgo` → `porCiudad`, `porCanal`; `descartesPorFiltro: promedio 0` | Tasas de paso por etapa y grupo (género, rango de edad) con la regla de 4/5, solo para auditoría y con permiso. |
| PS1-27 | Experiencia del candidato | Seguir a los descartados de la masiva | 27 personas que estaban en chat o pruebas quedaron descartadas por el filtro; el portal les muestra «No seleccionada», sin mensaje de cierre ni explicación. | Un descarte en mitad del proceso sin cierre es mala experiencia y mala marca empleadora para la EST. | Media | `MiPortal.tsx` estado «No seleccionada»; 16 en Preselección y 11 en Pruebas dentro de los 89 | Mensaje de cierre respetuoso (plantilla editable) y la opción de seguir en el banco para otras vacantes. |

## 4. Lo que no entendí

- **«Afinidad»**: ¿qué es exactamente? ¿Hoja de vida contra requisitos? ¿Pesa igual la experiencia que la formación? No hay una ficha con la
  fórmula ni los pesos.
- **«Por decidir» y «Esperando»** en el embudo: entendí después que «esperando» es al candidato. Mejor «Esperan al candidato» y «Esperan su
  decisión».
- **«Revisar»** sin botón: no queda claro qué espera la IA que haga.
- **«Rescatada»**: ¿la rescaté yo o el sistema? ¿Vuelve a la etapa anterior?
- **«Promedio»** en la tarjeta de pruebas, cuando la personalidad no suma: ¿promedio de qué?
- **«Total provisional»** de las pruebas.
- **«Misión en empresa usuaria»** está bien para mí, pero a un candidato hay que decirle «contrato con la temporal para trabajar en…».
- **«Huella»** de la aceptación: el candidato no la entiende.
- **«Onboarding»** junto a «Inducción» (dos palabras para lo mismo) y **«Vinculado»** cuando la persona todavía está en periodo de prueba.
- La ciudad del contrato de Camila no se edita en «Aprobar contratación» (viene de la vacante).

## 5. Lo que me falta para usarlo en mi trabajo real

1. Que la **EST sea el empleador** en todo el flujo (chat, consentimiento, oferta, contrato, alta) y que la usuaria aparezca como tal.
2. **Acciones masivas reales:** seleccionar todos los que cumplen un filtro (no solo la página), descartar en bloque con motivo, reenviar enlaces
   en bloque y exportar.
3. **Mandar los enlaces** (chat, pruebas, oferta, documentos) en bloque: aunque sea un archivo para mi WhatsApp Business o un envío masivo por correo.
4. **Pruebas validadas** con baremos, o poder cargar las pruebas que ya uso con su informe; y un informe de terna para el cliente.
5. **Un instrumento de evaluación por competencias** (aunque no haya módulo de entrevistas): una escala de evidencias por competencia que llene
   la psicóloga y que salga en el comparativo.
6. **PDF del contrato** y de la oferta aceptada.
7. Que nada de la IA se dispare sin que yo lo pida (redacción de oferta, plan de inducción, lectura de documentos), con una alternativa por plantilla.

## 6. Uso de IA

Hice **2 acciones de IA**, y ninguna de las dos la pedí con un botón de IA: se dispararon solas.

1. **Redactor de oferta y contrato** al «Aprobar contratación» de Camila Toro (Mercaderista): `claude-haiku-5-5`, US$0,002345, unos 25 s.
   Útil a medias: la carta es cálida y las cláusulas cubren lo básico, y **sus alertas fueron lo mejor**, porque detectó que el empleador no era
   la EST, que la prórroga no tenía fecha y que el auxilio estaba sin cifra. Pero el contrato salió con el empleador equivocado (porque así se lo
   mandó el sistema), sin causal de misión y con errores de género. Lo corregí a mano.
2. **Plan de inducción** al «Crear el empleado» de Santiago Quintero (Cajero): `gemini-3.5-flash-lite`, US$0. Útil: 11 tareas concretas para un
   cajero (datáfono, arqueos, riesgo ergonómico) con responsable y fecha. Falla en la evaluación del periodo de prueba (puesta el día 30) y en
   que ignora la inducción de la EST.

No usé «Proponer terna», «Calificar con IA», el copiloto ni la lectura de documentos, para no pasarme del presupuesto. La terna la armé a mano.

## 7. Mi veredicto

**Hoy no lo compraría para una EST, pero sí lo recomendaría para una prueba en vacantes pequeñas cuando se corrija lo primero.** El enfoque es el
correcto para una psicóloga: criterios iguales y visibles, evidencia en cada sugerencia, la persona decide y el dato sensible se cuida. Los
filtros ATS y las alertas legales de vinculación son mejores que lo que uso hoy.

Lo que tendría que cambiar primero, en orden:

1. **El empleador en misión debe ser la EST** (PS1-01, 02 y 03). Sin eso no puedo firmar ni un contrato.
2. **Volumen de verdad:** descarte en bloque con motivo, filtros y orden, «todos los que cumplen» y exportar (PS1-04, 05, 06), además del envío
   de enlaces en bloque (PS1-07).
3. **Rigor psicométrico:** no tomar la ausencia de evidencia como incumplimiento (PS1-10), resúmenes con cita (PS1-13), pruebas validadas o
   externas (PS1-14) y competencias evaluables (PS1-15).
4. **Contrato completo** con causal de misión, PDF y camino manual cuando la IA no esté (PS1-18, 19, 20).

### Datos que cambié en el ambiente local

- Masiva: filtros ATS guardados (Bachiller, turnos rotativos, Bogotá y Soacha, 6 meses), 89 descartes aprobados, 100 invitados a preselección.
- Mercaderista: pruebas enviadas a Jorge Lozano, Daniel Duque a terna, terna aprobada (Camila, Fabián, Daniel) y proceso de vinculación de
  Camila (`VIN-261030-267F`), con oferta editada y aceptada y antecedentes y examen médico cargados.
- Cajero: proceso de Santiago Quintero (`VIN-261027-A5FB`) llevado hasta «Vinculado», con su empleado creado.
