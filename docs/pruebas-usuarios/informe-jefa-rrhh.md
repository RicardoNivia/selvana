# Informe de prueba · Patricia Gaitán, jefa de RRHH (Talento Express EST)

Prueba hecha el 30/10/2026 en el ambiente local (API `http://localhost:5000`), con curl y leyendo las pantallas de `frontend/src/features/**`.
Acciones de IA usadas: **2 de 2**.

## 1. Quién soy y qué hice

Soy Patricia Gaitán, jefa de recursos humanos de Talento Express EST (Bogotá). Publicamos entre 40 y 80 vacantes al mes (operarios, auxiliares de
bodega, mercaderistas, asesores de call center) para clientes de retail, manufactura y logística. Llevo más de 10 años en selección masiva; he usado
Excel, Computrabajo, un ATS extranjero y la nómina de SINCO. Cuento clics y retrabajos, y me preocupa la ley laboral y la Ley 1581.

### Semana 1 · Arranque (mi empresa nueva)

- Me registré en `/registro` (`POST /api/v1/sesion/registro`): «Talento Express EST S.A.S.», NIT 901.777.321-2, marqué que somos EST. Primero probé
  con una contraseña corta: el error salió claro. Con el NIT mal escrito (dígito 5) también: «Escriba el NIT con 9 dígitos y su dígito de verificación».
  Entré de una vez con 30 días de prueba (`pruebaHasta` 29/11/2026). Quedé como **Administrador**.
- Leí los «Primeros pasos» (`GET /core/primeros-pasos`): estructura, planta, equipo, primera vacante, postulantes.
- Estructura a mano (`/consola/empresa/estructura`): 2 sedes, 2 áreas, 1 centro de costo y 3 cargos (Auxiliar de bodega, Asesor de call center,
  Psicólogo de selección) con perfil escrito por mí. Me rechazó el riesgo ARL «III»: solo acepta «Clase III».
- Empresas usuarias (`/consola/empresa/usuarios`, pestaña Empresas): Almacenes La Economía, Logística Sabana y Contacto Total BPO, con NIT validado.
- Usuarios: invité a mis dos psicólogos, Jorge Pinilla (solo dos empresas usuarias) y Marcela Rojas (todas). El rol sugerido por el cargo fue
  «Analista». La invitación no llega por correo: la pantalla dice «Mientras conectamos el correo, cópielo y envíeselo usted».
- Tipos de contrato (`/consola/empresa/tipos-contrato`): ya venían sembrados (aprendizaje, misión en empresa usuaria, obra o labor, etc.) con
  buena descripción legal, pero **sin plantilla**.

### Semana 2 · Vacantes

- Vacante a mano (`POST /reclutamiento/vacantes/manual`): «Auxiliar de bodega - Funza», 15 cupos, Logística Sabana. Luego la mandé a aprobación y la
  aprobé: **un solo clic aprobó RRHH y Gerencia a la vez** y quedó publicada en `/empleos`.
- Probé una vacante con salario de **$800.000 a $900.000** de tiempo completo: se creó, se aprobó y **se publicó en el portal público** sin ninguna alerta.
  (La cerré después.)
- Vacante con IA en una frase (**acción de IA 1**): «Necesito 20 mercaderistas para Almacenes La Economía en Bogotá, con moto propia, salario mínimo
  más rodamiento, para iniciar el 15 de noviembre». Tardó unos 20 s (modelo `gemini-3.5-flash-lite`).
- Revisé los filtros ATS de la vacante de bodega (excluyentes sugeridos por reglas, bien explicados) y la suite psicotécnica: 4 pruebas, **todas
  «validada: false»**.

### Semana 3 · Selección (en Talento Andino, como `valentina.rios@talentoandino.co`)

- Vacante masiva «Operario de producción · selección masiva» (1.000 postulaciones): el proceso por etapas respondió en 0,17 s. Embudo: 388 recibidas,
  234 en preselección, 89 en pruebas, 50 en terna, 239 descartadas.
- Apliqué 3 sugerencias «Invitar a la preselección» en bloque (`POST /proceso/sugerencias`) y descarté una persona: sin motivo no me deja
  («Diga por qué descarta al candidato»), con motivo sí.
- Revisé la bandeja (18 pendientes de todo tipo), el tablero Kanban, el reporte de sesgo (bien: muestra lo que el agente no ve y brechas por ciudad y canal)
  y los filtros ATS de la masiva.
- Comparativo de finalistas en «Asesor comercial de tienda»: claro y con evidencia.

### Semana 4 · Vinculación y gestión (Talento Andino)

- «Aprobar contratación» de Natalia Gómez Ruiz desde la terna. Las condiciones vinieron sugeridas (misión, 6 meses, 36 días de prueba, auxilio de
  transporte). Probé condiciones malas en `POST /vinculacion/contratacion/revisar`: bloquea salario bajo el mínimo y periodo de prueba de 90 días. Bien.
- Al aprobar, el agente redactor escribió **solo y sin preguntarme** la oferta y el contrato (**acción de IA 2**, `claude-haiku-5-5`, USD 0,0027, unos 30 s).
  Corregí a mano el género y el nombre del empleador (`PUT /oferta`), aprobé y simulé la aceptación de la candidata por su enlace.
- Seguí los procesos sembrados: documentos leídos por la IA, afiliaciones, alta, inducción. Tres inducciones llevaban 5 días «Generando»: usé el
  «plan base» en una y salió un plan de 12 tareas muy bueno (incluye inducción en la empresa usuaria).
- Revisé empleados, «Mi equipo», desempeño (ciclos 2025 y 2026), clima (encuesta 2026, riesgo de rotación con señales explicadas), bitácora y el tablero
  de selección como lo vería mi gerente.

## 2. Lo que me funcionó

1. **Las reglas legales al contratar**: el salario bajo el mínimo y el periodo de prueba de más de 2 meses se bloquean con la norma citada (art. 78 CST),
   y la misión de más de un año también (`ReglasContrato`). Eso me da tranquilidad.
2. **Descartar exige motivo** y queda en el historial y la bitácora; la persona ve «No seleccionada», sin el motivo interno.
3. **Rapidez con volumen**: la vacante de 1.000 postulaciones abre el proceso, el Kanban y los filtros en menos de 0,2 s, y las sugerencias se aplican en bloque.
4. **Explicaciones**: cada sugerencia muestra criterios con evidencia («Puntaje 82/100», «Respondió No a…»), el reporte de sesgo dice qué no ve el agente,
   y la oferta trae «alertas» útiles del redactor (comisiones no incluidas, prórroga, fecha de pago).
5. **Hay camino manual de verdad** en casi todo: vacante a mano, cargo con perfil propio, oferta editable, plan de inducción base por reglas (que además
   piensa en la EST: «Inducción en la empresa usuaria»).

## 3. Hallazgos

| ID | Etapa | Qué intenté | Qué pasó | Por qué me estorba | Severidad | Evidencia | Mejora que propongo |
| --- | --- | --- | --- | --- | --- | --- | --- |
| JR-01 | Vinculación · contrato | Leer el contrato que redactó el agente para una misión | La cláusula PRIMERA no nombra al empleador: dice «la empresa de servicios temporales que actúa como empleador», sin razón social ni NIT, y pone a la empresa usuaria como parte del contrato | Un contrato de trabajo sin el empleador identificado no lo firmo; la usuaria no es parte del contrato laboral | Bloquea | `GET /vinculacion/procesos/{id}` → `oferta.clausulas[0]` | Que el contrato tome siempre razón social, NIT y representante de la EST desde la empresa, y deje a la usuaria solo como lugar de la misión |
| JR-02 | Vinculación · oferta | Ver la oferta como la candidata | En el enlace del candidato la «empresa» es la empresa usuaria (Retail Express Colombia), no la EST | La persona cree que la contrata el cliente; eso es justo lo que la Ley 50 obliga a dejar claro | Alta | `GET /portal/vinculacion/{token}` → `"empresa": "Retail Express Colombia"` | En la pantalla del candidato: «Te contrata Talento Andino EST · trabajarás en misión en Retail Express Colombia» |
| JR-03 | Vinculación · contrato | Revisar la misión | El contrato de misión no dice la causa del servicio temporal (labor ocasional, reemplazo, incremento de producción) | Es lo primero que revisa un inspector del Ministerio en una EST; sin causa la misión se cae | Alta | Cláusula TERCERA: «labores de Asesor comercial … según la necesidad de la empresa usuaria» | Campo obligatorio «Causa de la misión» (art. 77 Ley 50) en la vacante o en las condiciones, y que el contrato la cite |
| JR-04 | Vacantes · salario | Crear y publicar una vacante de $800.000–$900.000, tiempo completo | Se creó, se aprobó y se publicó en el portal público sin alerta | Publicar por debajo del mínimo es una falta y una vergüenza ante los candidatos; el bloqueo solo aparece al contratar | Alta | `POST /reclutamiento/vacantes/manual` → 201; `/portal/vacantes` la mostraba | Aplicar la misma regla del salario mínimo de Vinculación al guardar y al aprobar la vacante |
| JR-05 | Pruebas psicotécnicas | Revisar la batería | Las 4 pruebas dicen «validada: false»; aun así salen en el comparativo de finalistas con puntaje y corte | Mis psicólogos no pueden firmar un informe con pruebas sin validar (Ley 1090) | Alta | `GET /reclutamiento/pruebas/suite`; finalistas: `"provisional": true` | Rótulo visible «Prueba en validación: no usar como criterio único» en cada resultado, y que el corte no cuente para la sugerencia hasta validarla |
| JR-06 | Usuarios y roles | Invitar a mis psicólogos | No existe rol de psicólogo; el sugerido es «Analista», y Analista **no puede** ver datos sensibles (pruebas de personalidad) | El que interpreta las pruebas es el psicólogo; así solo la gerente puede verlas | Alta | `GET /core/usuarios/permisos`: VerDatosSensibles → Analista «No» | Rol «Psicólogo de selección» con acceso a resultados sensibles, o permiso sensible por persona |
| JR-07 | Selección · terna | Revisar la terna de la masiva | Hay personas en Terna que respondieron «No» a una pregunta excluyente (turnos rotativos), y la sugerencia sigue siendo «Lista para la decisión de contratar» | Puedo contratar a alguien que ya dijo que no puede hacer el turno; retrabajo y rotación | Alta | `GET /proceso?etapa=Terna&buscar=Kelly` → `resultadoFiltros: NoCumple` con sugerencia «Contratar» | Si no cumple un excluyente, la sugerencia en terna debe advertirlo en rojo y pedir confirmación con motivo |
| JR-08 | Empleados · EST | Ver empleados en misión desde 2023 en la misma usuaria | No hay alerta por tiempo de misión (6 + 6 meses) en el expediente; la regla solo existe al contratar | Las EST perdemos demandas por misiones eternas o por «carrusel» de recontratación | Alta | Empleada «Bibiana Jaramillo», misión en Retail Express desde 06/07/2023, alertas solo de afiliaciones | Alerta en el expediente y en «Mi equipo» a los 5 y 11 meses de misión en la misma usuaria, y al recontratar a la misma persona |
| JR-09 | Vacantes · requisición | Mandar mi vacante a aprobación | Un solo clic de la misma persona aprobó RRHH y Gerencia; quien la pidió fue quien la aprobó | No hay separación de funciones; un auditor lo marca de una | Media | `aprobacion.decisiones`: dos «Aprobada» de Patricia Gaitán a la misma hora | Avisar «Usted pidió esta requisición» y permitir configurar que el solicitante no apruebe, o al menos dejar dos decisiones explícitas |
| JR-10 | Vacantes · EST | Pedir la vacante para un cliente | El flujo es líder → RRHH → gerencia interna; el cliente (empresa usuaria) no pide ni aprueba nada | En una EST la requisición la manda el cliente por correo o WhatsApp; sigo en Excel para eso | Alta | Pantalla «Nueva vacante»: solo elijo la empresa usuaria en una lista | Formulario o enlace para que el cliente pida la vacante y apruebe la terna sin ser usuario completo |
| JR-11 | Vacantes · volumen | Calcular cuánto me demoro en publicar 10 vacantes | No hay «Duplicar vacante» ni plantilla por cliente y cargo; elegir un cargo del catálogo **no copia** su perfil al formulario manual | Para 60 vacantes al mes tendría que reescribir requisitos, descripción y preguntas cada vez, o pagar IA en cada una | Alta | `VacanteNueva.tsx`: elegir cargo solo pone el nombre; no hay endpoint de duplicar | Botón «Duplicar» y «Llenar desde el perfil del cargo (sin IA)» |
| JR-12 | Estructura · IA escondida | Crear un cargo y dejar la misión vacía | Al guardar sin misión, el cargo se crea y **se le pide a la IA el perfil** sin que yo lo pida | Gasto de IA que no decidí; la pantalla no lo dice | Media | `CrearCargoHandler`: si `Perfil` es nulo publica `PerfilesCargoSolicitados` | Que guardar sin perfil deje «Sin perfil» y que la IA solo corra con «Generar con IA» |
| JR-13 | Vinculación · IA escondida | Aprobar la contratación | El agente redactor corre solo al aprobar; no puedo elegir «uso mi plantilla» o «lo escribo yo» antes | Cada contratación gasta IA aunque tenga mi propia minuta; con 40 contratos al mes se nota | Media | `AprobarContratacionHandler` publica la solicitud siempre | Opción en «Aprobar contratación»: redactar con IA, con la plantilla del tipo de contrato o en blanco |
| JR-14 | Vinculación · redacción | Leer la carta y el contrato de Natalia | «fuiste seleccionado», «identificado con cédula», «Bienvenido al equipo»: género equivocado; mezcla tú con el estilo jurídico; «PRIMERA … SEGUNDO» | Tengo que corregir a mano cada oferta antes de enviarla | Media | `oferta.carta` y `oferta.clausulas` | Lenguaje neutro o con el género del registro; numeración fija de cláusulas |
| JR-15 | Tipos de contrato | Revisar las plantillas | Ningún tipo trae plantilla y la alerta dice «Las plantillas aún no tienen revisión jurídica» | No puedo vender a mi gerente un contrato que nadie revisó | Alta | `GET /vinculacion/tipos-contrato` → `plantillaNombre: null`; alerta en condiciones | Minutas base revisadas por un abogado laboral para misión, obra o labor y fijo |
| JR-16 | Vinculación · firma | Ver cómo acepta la candidata | Acepta escribiendo su nombre y cédula; queda huella, IP y navegador, pero no hay código al celular ni al correo | Cualquiera con el enlace puede «firmar»; no sé si me vale como firma electrónica | Media | `oferta.respuesta`: `nombreEscrito`, `ip`, `huella` | Código de un solo uso al celular o correo antes de aceptar (Decreto 2364 de 2012) |
| JR-17 | Vinculación · inducción | Revisar procesos en Onboarding | Tres planes de inducción llevan 5 días «Generando», sin error ni aviso | Nadie se entera; el ingreso es el 03/11 | Media | Rosa Ibáñez, Luis Cárdenas, Bibiana Cifuentes: `induccion.estado: Generando`, actualizado el 25/10 | Si la IA no responde en minutos, pasar a «No se pudo» y ofrecer el plan base solo |
| JR-18 | Vinculación · alta | Revisar el alta de Rosa | Sede «Bogotá» con centro de costo «CC-210 Bodega Ibagué»; ARL clase 2 con razón «Cargo administrativo o de oficina» | Datos contradictorios que van a la nómina sin alerta | Media | `alta` y `afiliaciones` del proceso VIN de Rosa | Reglas: el centro de costo debe ser de la sede; la clase ARL debe coincidir con la razón |
| JR-19 | Invitaciones | Invitar a mis psicólogos | El enlace hay que copiarlo y mandarlo por WhatsApp; tampoco hay invitación masiva por archivo | Con 10 analistas y 20 líderes de cliente es tedioso | Media | `PestanaUsuarios.tsx`: «Mientras conectamos el correo, cópielo y envíeselo usted» | Correo de invitación (cuando lleguen las integraciones) y carga por CSV |
| JR-20 | Oferta | Enviar la oferta aprobada | También es un enlace que debo copiar y enviar uno por uno | 40 contrataciones de una masiva = 40 copias | Media | `POST /oferta/aprobar` devuelve solo el `token` | Envío en bloque y lista «enlaces por enviar» con botón de WhatsApp |
| JR-21 | Bandeja | Trabajar la bandeja de Talento Andino | 18 pendientes de 8 tipos mezclados; solo hay filtro Pendientes/Decididas | No puedo repartir: «ofertas a mí, documentos al analista, cambios de datos a nómina» | Media | `Bandeja.tsx`: segmentado solo de estado | Filtros por tipo, por empresa usuaria y por vacante; asignar a una persona |
| JR-22 | Selección masiva | Usar la terna con 40 cupos | 50 personas en etapa Terna, pero «Finalistas» muestra 4; el nombre «terna» no sirve para masivos | No sé a quién le estoy presentando al cliente | Media | `GET /vacantes/{masiva}/finalistas` → 4 con `cupos: 40` | En masivos: «Seleccionados» por lotes, comparativo paginado y envío del lote al cliente |
| JR-23 | Tablero de selección | Verlo como mi gerente | No se puede filtrar por empresa usuaria; no hay cupos cubiertos vs. pedidos ni tiempo prometido al cliente | Mi gerente mide por cliente y por ANS; tengo que exportar y hacer Excel | Alta | `GET /reclutamiento/tablero-seleccion` admite solo `dias` y `estado` | Filtro por empresa usuaria, «cupos cubiertos / pedidos» y semáforo de días contra el ANS del cliente; exportar a Excel |
| JR-24 | Vacante con IA | Pedir «salario mínimo más rodamiento» | La IA metió el rodamiento en el rango salarial (hasta $2.100.000) y lo justificó como «salario mínimo vigente más auxilio de rodamiento» | El rodamiento no es salario; si lo publico así, después me reclaman prestaciones sobre eso | Media | Vacante «Mercaderista con moto propia»: `justificacionSalario` | Campo aparte «Auxilios no salariales» y que la IA los ponga ahí |
| JR-25 | Vacante con IA | Revisar requisitos de la moto | Pidió «Tener moto propia» y «Documentos de la moto al día», pero no licencia A2 ni SOAT; la fecha de inicio (15/11) no quedó en ningún campo | Me llegan postulantes sin licencia; la fecha se pierde | Media | Vacante IA: `requisitos`, `supuestos` | Requisitos legales por tipo de vehículo; campo «Fecha de inicio» en la vacante |
| JR-26 | Vacantes | Elegir el cargo equivocado | Escribí «Auxiliar de bodega» pero elegí el cargo «Asesor de call center»; la vacante quedó con cargo de call center y batería de servicio al cliente, sin aviso | El aviso dice bodega y las pruebas son de call center | Baja | Vacante VAC-261030-3F30 | Advertir cuando el título y el cargo del catálogo no se parecen |
| JR-27 | Primeros pasos | Seguir el recorrido siendo EST | No incluye empresas usuarias, tipos de contrato ni baterías de pruebas; «Invite a su equipo» se marca hecho con invitaciones sin aceptar | Una EST nueva arranca a medias | Baja | `GET /core/primeros-pasos` | Pasos propios de EST y marcar hecho cuando la persona activa la cuenta |
| JR-28 | Usuarios | Cambiar mi propio rol | Pude pasar de Administrador a GerenteRrhh yo misma y así ganar acceso a datos sensibles; la empresa quedó sin Administrador | Cualquiera con «Invitar personas» puede subirse permisos | Media | `PUT /core/usuarios/{mi id}` → 204 | No permitir cambiar el propio rol; exigir al menos un Administrador activo |
| JR-29 | Registro | Registrar la empresa | Quien registra queda como Administrador, que no ve datos sensibles; la pantalla no lo dice | La jefa de RRHH que compra no ve los resultados de sus candidatos | Baja | Sesión tras el registro: `acciones` sin `VerDatosSensibles` | Preguntar en el registro «¿Es usted de RRHH?» y dar GerenteRrhh |
| JR-30 | Selección · reglas | Leer «Automático con límites» | La pantalla dice «La IA lo hace sola», pero son reglas fijas (excluyentes y afinidad mínima) | A mi equipo le explicaría mal quién decide | Baja | `ProcesoSeleccion.tsx`: pill «La IA lo hace sola» | «Selvana lo hace sola con estas reglas» y violeta solo para lo que hace el modelo |
| JR-31 | Estructura | Riesgo ARL del cargo | Solo acepta «Clase I…V»; con «III» el error es genérico | Baja, pero es el primer error que vi | Baja | `PerfilEditadoValidator` | Lista desplegable en la pantalla, ya existe; aceptar ambos formatos al importar |
| JR-32 | Datos de prueba | Leer historiales | Hay movimientos con fechas futuras (01/11/2026) en la vacante masiva | Confunde en una demo con cliente | Baja | Historial de «Kelly Quintero Ospina»: «2026-11-01» | Revisar la siembra de datos |

## 4. Lo que no entendí

- **«Terna»** en una vacante de 40 cupos: ¿es la lista que le mando al cliente o la final? Para masivos no es una terna.
- **«Afinidad»** y **«puntaje»**: ¿son lo mismo? En el proceso dice «Afinidad de 70 o más» y en la tarjeta «Puntaje 82/100».
- **«Automático con límites»**: no queda claro si decide la IA o las reglas, ni cada cuánto corre («cada cinco minutos»).
- **«Recibida → Preselección»** significa «invitar al chat», no «preseleccionada»; mi equipo usa «preselección» para el filtro de hoja de vida.
- **«Vacante propia»** como opción por defecto en una EST: casi nunca lo es.
- **«Provisional»** y **«validada: false»** en las pruebas: no dice qué hacer con eso.
- Estados de vinculación **«Onboarding»** y **«Vinculado»**: en Colombia decimos «inducción» y «contratado / activo».
- **«Huella»** en la aceptación de la oferta: para RRHH no es claro si es una firma.

## 5. Lo que me falta para usarlo en mi trabajo real

1. **Portal o enlace del cliente (empresa usuaria)**: que pida vacantes, vea la terna o el lote y apruebe sin llamarme.
2. **Duplicar vacantes y plantillas por cliente y cargo**: para publicar 60 vacantes al mes en minutos, sin IA en cada una.
3. **Contrato de misión completo**: empleador con NIT, causa de la misión, prórroga escrita y minutas revisadas por un abogado.
4. **Control del tiempo de misión** por persona y por usuaria (6 + 6 meses) en el expediente, con alertas.
5. **Envío masivo** de invitaciones, ofertas y enlaces de documentos (correo y WhatsApp) cuando lleguen las integraciones; mientras tanto, una lista
   «por enviar» con botón para copiar todos.
6. **Rol de psicólogo** con acceso a resultados sensibles y pruebas validadas.
7. **Tablero por cliente** con ANS, cupos cubiertos y exportación a Excel.
8. **Exportar el alta a SINCO** lo vi como archivo canónico; necesito el formato exacto de mi nómina (no lo probé).

## 6. Uso de IA

| # | Acción | Resultado | Utilidad |
| --- | --- | --- | --- |
| 1 | Vacante en una frase (Talento Express, «20 mercaderistas con moto propia… salario mínimo más rodamiento…») | ~20 s, `gemini-3.5-flash-lite`. Título, descripción, 2 requisitos, 3 competencias, 3 preguntas filtro, supuestos y 4 razones con la frase citada | Buena para arrancar; error de fondo al meter el rodamiento en el salario (JR-24) y faltan licencia y SOAT (JR-25). La explicación sí me deja ver qué supuso |
| 2 | «Aprobar contratación» de Natalia Gómez Ruiz (Talento Andino), que dispara el agente redactor | ~30 s, `claude-haiku-5-5`, USD 0,0027. Carta y 10 cláusulas, razones y 5 alertas muy útiles (comisiones, prórroga, fecha de pago) | Ahorra tiempo, pero no la puedo mandar sin corregir: empleador sin nombre (JR-01), sin causa de misión (JR-03) y género equivocado (JR-14). Y la IA corrió sin que yo la pidiera (JR-13) |

No usé «Calificar con IA», lector de hojas de vida ni de documentos, copiloto ni análisis de comentarios. La lectura de documentos la vi en datos sembrados.

## 7. Mi veredicto

**Hoy no lo compraría para mi EST, pero lo pondría a prueba con un cliente pequeño.** La selección masiva es rápida, explica sus sugerencias y
obliga a dejar motivo; eso es mejor que mi ATS actual. Lo que me frena es el riesgo legal de la vinculación en misión y el trabajo manual
repetido para el volumen.

Lo que tendría que cambiar primero:

1. El contrato y la oferta de misión: empleador con NIT, causa de la misión y la EST como empresa visible para el candidato (JR-01, JR-02, JR-03, JR-15).
2. Bloquear salarios bajo el mínimo desde la vacante (JR-04) y avisar cuando alguien en terna no cumple un excluyente (JR-07).
3. Duplicar vacantes y llenar desde el perfil del cargo sin IA (JR-11), y no gastar IA sin que yo la pida (JR-12, JR-13).
4. Rol de psicólogo y pruebas marcadas como «en validación» en todos los informes (JR-05, JR-06).
5. El cliente dentro del flujo y un tablero por cliente (JR-10, JR-23).

### Resumen para el equipo de producto (10 líneas)

1. Selección masiva rápida (1.000 postulaciones en < 0,2 s), sugerencias en bloque y explicaciones con evidencia: es lo mejor del producto.
2. El contrato de misión que redacta la IA no nombra a la EST como empleador ni la causa de la misión: hoy no se puede firmar (Bloquea).
3. El candidato ve a la empresa usuaria como «empresa» en su oferta; en una EST eso es un riesgo legal.
4. Se pueden publicar vacantes por debajo del salario mínimo; la regla existe solo al contratar.
5. Personas que no cumplen un excluyente llegan a la terna con la sugerencia «Lista para contratar».
6. No hay rol de psicólogo y las pruebas sin validar aparecen con puntaje y corte en el comparativo.
7. La IA corre sin pedirla al crear un cargo sin misión y al aprobar una contratación: hay que poder elegir.
8. Para 60 vacantes al mes faltan duplicar vacante, llenar desde el perfil del cargo sin IA y envíos en bloque.
9. Para una EST faltan el cliente en el flujo (requisición y terna) y el control de 6 + 6 meses de misión en el expediente.
10. El tablero de la gerencia necesita filtro por empresa usuaria, cupos cubiertos y ANS; hoy saldría a Excel.
