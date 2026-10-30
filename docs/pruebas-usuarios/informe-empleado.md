# Informe de prueba como usuario: empleado y candidato

Agente: `empleado-cliente` · Fecha: 30/10/2026 · Ambiente: local (API `http://localhost:5000`, web `http://localhost:5173`)

## 1. Quién soy y qué hice

Soy **Leonardo Montoya**, tengo 27 años y soy bachiller. Trabajé en bodega (cargue, conteos, picking) y hoy estoy en misión con Talento Andino EST como
asesor comercial en Retail Express. Todo lo hago desde el celular, con datos limitados y poco tiempo. No me gustan los formularios largos ni las palabras
raras, y no entrego mis datos si no entiendo para qué son. Quiero seguir en mi trabajo y también mirar ofertas de bodega.

Usé la API con curl, como si tocara los botones de la pantalla, y leí las pantallas en `frontend/src/features/portal`, `portal-persona`, `pruebas`,
`preseleccion`, `vinculacion` y `clima` para ver qué textos me salen en el celular. Mi cuenta es `leonardo.montoya506@ejemplo.co`. También entré como
`fernanda.patino104@ejemplo.co` y `andres.cardenas504@ejemplo.co` (candidatos con postulaciones) y miré, sin responder nada, lo que tiene pendiente
`fabian.henao500@ejemplo.co`.

### Semana 1 · Buscar empleo (`/empleos`)

- Listé las vacantes (`GET /portal/vacantes`): son 15 en 5 empresas (16 al final del día, porque otra persona publicó una en Funza). Las tarjetas traen
  ciudad, jornada, cupos, salario y un resumen corto. Me gustó.
- Busqué «bodega», «auxiliar de bodega», «montacarga», «operario» y «cargue» y funcionó. En cambio «bodeguero», «ibague», «logistica», «enfermeria» y
  «Barranquilla» no dieron resultados (ver EMP-14).
- Abrí «Auxiliar de bodega» en Barranquilla (`GET /portal/vacantes/VAC-261013-5034`). La pantalla trae «Sobre el trabajo», «Lo que buscamos» y «Te
  preguntaremos», y en la columna de la derecha, el salario y el botón «Postularme en 2 minutos».
- Revisé cómo es postularse **sin cuenta** leyendo `Postular.tsx`: hay que subir la hoja de vida, la IA la lee y yo confirmo. No hay otra forma de
  hacerlo (EMP-03).
- Leí la autorización de datos (`PoliticaDatos` v3 y `PoliticaDatosPortal` portal-v1). Se entiende, pero en una sola casilla autorizo dos cosas
  distintas (EMP-09).

### Semana 2 · Mi portal (`/mi-portal`)

- Entré con `POST /portal/cuenta/ingresar`. El correo con mayúsculas y un espacio al final igual entra. Con una contraseña mala sale «El correo o la
  contraseña no coinciden». No encontré la opción de recuperar la contraseña (EMP-10).
- Mi cuenta llegó sin hoja de vida y sin experiencia, así que «Ofertas para ti» estaba vacía y el mensaje me mandaba a completar la hoja de vida. Lo
  entendí.
- **Camino manual:** escribí a mano mi experiencia, mis estudios, mis cursos y mis habilidades (`PUT /portal/cuenta/perfil`). Enseguida aparecieron 12
  «ofertas para ti», cada una con su porqué (EMP-13).
- **Acción de IA 1 de 2:** subí mi hoja de vida en PDF (`POST /portal/cuenta/hoja-de-vida`). La leyó en unos 20 segundos y sacó bien los datos, las tres
  experiencias, los estudios, los cursos y las habilidades. Repitió una sola habilidad («Picking» y «Picking con lector»). La guardé corregida.
- Me postulé con mi perfil a «Mercaderista» en Medellín (`POST /portal/cuenta/postular/VAC-261024-4832`). Quedó el borrador con las dos preguntas de la
  empresa y la autorización v3. Lo mandé sin aceptar la autorización para ver el error («Para enviar tu postulación debes aceptar la autorización de
  datos.»). **No envié la postulación de verdad**, porque al enviarla arranca el ranking con IA y yo ya no tenía más acciones de IA. El borrador quedó sin
  enviar.
- Como Fernanda (con el perfil completo escrito a mano y sin archivo), intenté postularme con un clic a «Auxiliar de bodega» en Ibagué y salió
  `400 Portal.SinHojaDeVida` (EMP-02). Su proceso de «Operario de producción» dice «Estás en la etapa de pruebas psicotécnicas», pero no tiene ningún
  pendiente para presentarlas (EMP-01). A Andrés Cárdenas le pasa lo mismo.
- En «Mis documentos» subí un certificado bancario con fecha de vencimiento de 2024: lo aceptó sin decirme nada y quedó «Vencido». Probé con un `.txt` y
  lo rechazó con un mensaje claro. Después quité el certificado.

### Semana 3 · Mi empleo

- `GET /portal/cuenta/empleo`: veo Talento Andino «en Retail Express Colombia», el cargo, el contrato por obra o labor, el salario, la fecha de fin, mi
  celular, mi ciudad, la EPS, la pensión, la ARL, la caja y mi carpeta (cédula y contrato). Mi ciudad aparece como Medellín y mi sede como «Bogotá ·
  Calle 80», y nadie me avisa que eso no cuadra (EMP-29).
- **Propuse cambios a mis datos** (`PUT /empleo/{id}/datos`):
  - Con los mismos datos: «No cambiaste ningún dato. Si todo sigue igual, confírmalo.» Bien.
  - Con el celular «abc» y la EPS, la ciudad, la pensión y la caja vacías: lo **aceptó** (204) (EMP-06).
  - Volví a enviar con datos buenos (celular nuevo, Bogotá, Nueva EPS): reemplazó lo anterior. En pantalla eso no se puede hacer (EMP-07).
- **Acción de IA 2 de 2:** subí un soporte «Afiliación · Certificado Nueva EPS» que vence el 27/11/2026 (`POST /empleo/{id}/soportes`). Enseguida salió
  el aviso «"Certificado Nueva EPS" vence el 27/11/2026. Ten listo el nuevo.» (EMP-12). A los 60 segundos el documento seguía «En revisión», y yo no
  puedo ver qué leyó la IA ni cuándo decide RRHH (EMP-11).
- **Autoevaluación** (`POST /empleo/autoevaluacion/{id}`): ya la había enviado y la actualicé con un comentario mío. Con una calificación de 7 sale «Las
  calificaciones van de 1 a 5.» Los objetivos muestran un avance (44 %, 37 %, 30 %) que no sé quién midió (EMP-23).
- **Campaña de actualización:** a mí no me la habían pedido, así que solo leí el texto de la pantalla («Tu empresa te pide confirmar tus datos» con «Algo
  cambió» y «Todo sigue igual»). No la pude probar.
- **Pulso de 30, 60 y 90 días:** a mí no me salió ninguno. Fabián tiene un pulso de 30 días pendiente desde el 14/10 y una autoevaluación sin hacer, y
  nada de eso aparece en su inicio (EMP-05). No respondí por él.

### Semana 4 · Mis derechos

- Envié una **consulta Ley 1581** por `/empleos/mis-datos` (`POST /portal/solicitudes-titular`). Respondió `202` sin número de radicado. Con la sesión
  abierta igual tuve que escribir mi nombre y mi cédula, y como contacto aceptó «x» (EMP-17).
- Revisé qué pasaría si pido **suprimir** mis datos, sin ejecutarlo. Leí `MisDatos.tsx` y `SupresionDeTitularAprobadaConsumer`: solo se borra lo de
  Reclutamiento de esa empresa. No me explican qué pasa con mi expediente de empleado, con mi cuenta del portal ni con «Mis documentos» (EMP-18).
- **Enlaces públicos:** no tenía ninguno pendiente. Probé enlaces que no existen: el de pruebas dice «Este enlace de pruebas no existe o ya no sirve.»,
  y los de preselección y vinculación dicen «…Pídele uno nuevo a la empresa.». Leí las pantallas de pruebas, chat, oferta y clima (EMP-19, EMP-20,
  EMP-24).

## 2. Lo que me funcionó

1. **El portal de empleo se entiende rápido:** salario siempre visible, «Te preguntaremos» antes de postularme, «Postularme en 2 minutos» y una barra fija
   abajo en el celular.
2. **La lectura de la hoja de vida con IA es muy buena:** en unos 20 segundos llenó todo y me deja corregir. Lo que leyó la IA sale en violeta con «Leído de
   tu hoja de vida».
3. **«Mi empleo» me muestra lo mío** (contrato, salario, afiliaciones, carpeta) y cada cambio que propongo queda como «Tu empresa los está revisando», con
   el antes y el después.
4. **Los mensajes de error están en mi idioma:** «No cambiaste ningún dato. Si todo sigue igual, confírmalo.», «Sube el documento en PDF o como foto (JPG
   o PNG).», «Las calificaciones van de 1 a 5.».
5. **Hay frases que me dan confianza:** «nadie te descarta solo por un puntaje», «Nunca te preguntaremos tu edad, estado civil ni dirección», «Es
   anónima… grupos de 5 o más personas».

## 3. Hallazgos

| ID | Etapa | Qué intenté | Qué pasó | Por qué me estorba | Severidad | Evidencia | Mejora que propongo |
| --- | --- | --- | --- | --- | --- | --- | --- |
| EMP-01 | Proceso de selección | Como Fernanda y como Andrés, ver qué me falta en un proceso que está en «Pruebas» | El proceso dice «Estás en la etapa de pruebas psicotécnicas», pero «Pendientes» está vacío y el inicio dice «Estás al día». Abrir el pendiente de pruebas da 404 «Esto ya no está pendiente». | Me dicen que estoy en pruebas, pero no tengo cómo presentarlas. Me quedo esperando y pierdo la vacante. | Alta | `GET /portal/cuenta/portal` → `estado: "Pruebas"`, `pendientes: []`; `POST /pendientes/Pruebas/{id}/abrir` → 404 | Si el estado es «Pruebas», mostrar el enlace o decir claramente «La empresa aún no te envía las pruebas; te avisamos». Revisar por qué la semilla deja procesos en «Pruebas» sin pruebas creadas. |
| EMP-02 | Postularme con mi perfil | Postularme con un clic teniendo el perfil completo escrito a mano | `400 Portal.SinHojaDeVida`: «Sube tu hoja de vida en tu perfil para postularte con un clic». «Ofertas para ti» me muestra el botón «Postularme» y falla al tocarlo. | Hice todo a mano como me lo permite el portal, y no me sirve para lo principal. Es justo lo que haría alguien sin computador. | Alta | `PostularmeConMiPerfilHandler`, Fernanda en `VAC-260929-E50E` | Que el perfil escrito sirva para postularse (generar la hoja de vida en PDF desde el perfil, como ya hace «Ver y descargar»). |
| EMP-03 | Postularme sin cuenta | Postularme sin tener hoja de vida en archivo | La única puerta es «Sube tu hoja de vida». No hay «No tengo hoja de vida, la escribo aquí». | Muchos operarios no tienen la hoja de vida en el celular. Me voy. | Alta | `Postular.tsx`, `SubirHojaDeVida` | Agregar la opción «No tengo hoja de vida» con un formulario corto (datos, último trabajo y estudios). Así también hay un camino sin IA. |
| EMP-04 | Ver ofertas | Mirar los salarios | «Operario de producción · selección masiva» paga desde $1.423.500 y «Auxiliar de bodega - Funza», publicada hoy, paga $1.500.000–$1.600.000. Las demás arrancan en $1.750.905. | Un salario por debajo del mínimo me hace desconfiar del portal y de la empresa. | Alta | `GET /portal/vacantes`; `VAC-261010-94B2`, `VAC-261030-3CB1` | No dejar publicar por debajo del salario mínimo vigente, o avisar a quien publica antes de que la vacante salga. |
| EMP-05 | Mi empleo | Saber qué tengo pendiente en mi trabajo | La autoevaluación y el pulso pendientes solo aparecen dentro de la pestaña «Empleo». No cuentan en «Tienes N cosas por hacer» ni en el número de la pestaña: el inicio dice «Estás al día». | Si no abro esa pestaña, nunca me entero. Fabián tiene un pulso sin responder desde el 14/10. | Alta | `MiPortal.tsx` (`insignias.empleo` solo cuenta campañas); Fabián en `GET /empleo/pendientes` | Llevar al inicio y a los números de las pestañas la autoevaluación, el pulso y la campaña, con su fecha límite. |
| EMP-06 | Actualizar mis datos | Enviar un celular con letras y dejar la EPS y la ciudad vacías | Lo aceptó (204) y quedó como propuesta «Celular: 3365007014 → abc», «EPS: Sura → —». | Una equivocación mía puede dejar mal mi expediente y mis afiliaciones si RRHH aplica sin mirar. | Alta | `PUT /portal/cuenta/empleo/{id}/datos` con `celular: "abc"` | Validar el celular (10 dígitos), no dejar campos obligatorios vacíos y mostrar el error en el formulario. |
| EMP-07 | Actualizar mis datos | Corregir un cambio que ya envié | Mientras hay un cambio en revisión, la pantalla esconde «Actualizar mis datos» y «Subir un soporte». La API sí deja reemplazarlo. | Si me equivoqué, no lo puedo arreglar ni cancelar, y tampoco puedo subir soportes. | Media | `MisDocumentos.tsx`, `TarjetaEmpleo` (`!e.cambioPendiente`) | Agregar «Corregir lo que envié» y «Cancelar», y dejar siempre disponible «Subir un soporte». |
| EMP-08 | Mis datos | Cambiar mi celular | Hay dos lugares: «Mi hoja de vida» (se guarda de una vez) y «Mi empleo» (necesita la aprobación de RRHH). No se hablan entre sí. | Lo cambio en uno, creo que ya quedó, y la empresa me sigue llamando al número viejo. | Alta | `PUT /portal/cuenta/perfil` frente a `PUT /empleo/{id}/datos` | Al cambiar el celular en el perfil, preguntar «¿También se lo enviamos a tu empresa?». |
| EMP-09 | Autorización de datos | Leer y aceptar la autorización | Una sola casilla cubre «este proceso de selección» **y** «ofrecerme vacantes similares». No dice que una IA lee mi hoja de vida ni que me califica. | No puedo aceptar el proceso sin aceptar que me escriban para otras vacantes. Me da miedo no saber quién o qué me evalúa. | Alta | `PoliticaDatos.Texto` v3 | Separar la casilla de vacantes similares (opcional). Decir en una línea: «Una IA lee tu hoja de vida y ordena a los postulantes; una persona decide». Agregar el enlace a la política completa. |
| EMP-10 | Mi cuenta | Recuperar la contraseña, cambiarla o borrar mi cuenta | No existe «¿Olvidaste tu contraseña?», ni cambiar la contraseña, ni eliminar la cuenta. | Si se me olvida la contraseña, pierdo mis procesos. Si quiero irme, no sé cómo. | Alta | `Acceso.tsx`, `MiCuentaPanel`, `PortalPersonaEndpoints.cs` (no hay rutas) | Agregar recuperación por correo o celular, cambio de contraseña y «Eliminar mi cuenta» con confirmación. |
| EMP-11 | Subir soporte | Subir mi certificado de EPS | La IA lo lee siempre, sin avisarme y sin opción de no usarla. Queda «En revisión» y no sé qué leyó ni cuándo me responden. | No sé quién ve mi documento. Tampoco sé si ya me cambiaron la EPS. | Media | `EntregarSoporteDesdePortalHandler` (siempre publica la lectura); `enRevision: true` después de 60 s | Avisar en el formulario «Lo lee la IA y lo revisa RRHH». Mostrar «Leímos: Nueva EPS, activo desde 01/10/2026» y el plazo de respuesta. |
| EMP-12 | Subir soporte | Subir un certificado de EPS recién expedido | Apenas lo subo sale en amarillo «vence el 27/11/2026. Ten listo el nuevo.». | Recién lo subí y ya me regañan. El certificado de EPS siempre dura 30 días. | Media | `AvisosParaElEmpleado` y `DiasAvisoVencimiento`; `GET /empleo` | No avisar «por vencer» en los primeros días, o tener un plazo de aviso según el tipo de documento. |
| EMP-13 | Ofertas para ti | Ver las ofertas que encajan conmigo | Me encajan 12 de 16, entre ellas camillero, cajero y auxiliar de farmacia, con la única razón «Tus estudios: Bachiller académico». Me ofrece «Conductor C2» sin que yo tenga licencia y «Mercaderista» sin moto. La de mi ciudad sale de quinta. | Si todo encaja, nada encaja. Gasto mis datos abriendo ofertas que no son para mí. | Media | `GET /portal/cuenta/ofertas`; `EncajeOferta.Porque` | No contar «bachiller» como única razón, mostrar primero las de mi ciudad y avisar «Pide moto o licencia C2». |
| EMP-14 | Buscar | Buscar sin tildes o por ciudad | «ibague», «logistica», «enfermeria», «bodeguero» y «Barranquilla» no dan resultados. | En el celular no pongo tildes. El buscador dice «palabra clave» y no encuentra la ciudad. | Media | `GET /portal/vacantes?buscar=ibague` → 0 | Buscar sin importar tildes ni mayúsculas, incluir la ciudad en la búsqueda y entender sinónimos (bodeguero → bodega). |
| EMP-15 | Detalle de la vacante | Saber qué me están ofreciendo | No dice el tipo de contrato, ni si es por empresa temporal o en misión, ni el horario exacto, ni el barrio o la zona, ni los beneficios. El título público trae «· selección masiva». | Antes de postularme quiero saber si es temporal y dónde queda. «Selección masiva» suena a que soy uno más. | Media | `GET /portal/vacantes/VAC-261010-94B2` (no hay tipo de contrato) | Agregar tipo de contrato, «Te contrata [EST] para trabajar en [empresa]», la zona y el horario. Quitar palabras internas del título. |
| EMP-16 | Postulación enviada | Leer la pantalla final sin cuenta | Dice «Puedes postularte a más vacantes con la misma hoja de vida, sin volver a llenar nada», pero sin cuenta toca subirla otra vez. | Me prometen algo que no se cumple. | Media | `Postular.tsx`, `Enviada` | Sin cuenta, decir «Crea tu cuenta y la próxima vez te postulas con un clic». |
| EMP-17 | Mis derechos | Enviar una consulta Ley 1581 | Respuesta 202 sin número. Con sesión abierta igual me pide nombre y cédula. Aceptó «x» como contacto. No hay dónde ver mis solicitudes. | No sé si llegó ni cómo preguntar después. Si escribo mal el contacto, nunca me responden. | Media | `POST /portal/solicitudes-titular` | Dar un número de radicado, llenar los datos con la sesión, validar el correo o el celular y agregar «Mis solicitudes» en «Mi cuenta». |
| EMP-18 | Mis derechos | Entender qué pasa si pido borrar mis datos | El texto dice «Eliminan tu hoja de vida y tus datos de sus procesos de selección». No dice qué pasa con mi expediente de empleado, con mi cuenta del portal ni con «Mis documentos», y solo se borra lo de Reclutamiento de esa empresa. | Soy empleado: ¿me borran el contrato? ¿Me quedo sin cuenta? Me da miedo usarlo. | Media | `MisDatos.tsx`; `SupresionDeTitularAprobadaConsumer` | Explicar qué se borra, qué se guarda por obligación legal (el contrato y la nómina) y cómo borrar la cuenta del portal. |
| EMP-19 | Pruebas | Leer la pantalla de pruebas | «Prefiero no presentarlas» actúa con un solo toque, sin confirmar y sin decir qué pasa después. El tiempo «corre desde que la abres» y no hay nada que guarde mi avance si se me cae la señal. | En el celular un toque equivocado me saca del proceso. Con pocos datos, si se cae la señal pierdo la prueba. | Media | `PresentarPruebas.tsx`, línea `consentir.mutate(false)` y temporizador | Pedir confirmación y explicar la consecuencia. Guardar el avance y pausar el reloj si se cae la conexión. |
| EMP-20 | Chat de preselección | Saber con quién hablo | El encabezado dice «Asistente de selección · empresa» y el saludo «Soy el asistente de selección de…». Nunca dice que es automático. | Pienso que es una persona y le cuento cosas como a una persona. | Media | `ConversacionCandidato.tsx`; saludo en `PreseleccionCasosDeUso.cs` | Decir «Soy un asistente automático (IA). Una persona revisa tus respuestas y puedes pedir hablar con alguien». |
| EMP-21 | Mi portal en el celular | Moverme entre las secciones | En el celular la barra de abajo tiene 7 pestañas con letra de 11 px («Hoja de vida», «Documentos»…). | En 360 px de ancho quedan apretadas y toco la que no es. | Media | `portal-persona.css` `.mp-nav` (`@media max-width: 720px`) | Dejar 4 o 5 pestañas (Inicio, Procesos, Ofertas, Empleo, Más) y mover Documentos y Cuenta a «Más». |
| EMP-22 | Autoevaluación y pulso | Calificarme de 1 a 5 | Solo salen los números. No dice qué es 1 ni qué es 5. La encuesta de clima sí dice «Muy en desacuerdo» y «Muy de acuerdo». | Me califico al revés sin darme cuenta. | Baja | `Escala` en `MisDocumentos.tsx` frente a `EncuestaPublica.tsx` | Poner las palabras en los extremos de la escala, igual que en clima. |
| EMP-23 | Autoevaluación | Revisar mis objetivos | Salen avances de 44 %, 37 % y 30 % sin decir quién los midió ni cuándo. Un objetivo es «Cuadre de caja o cierre de casos» y yo soy asesor comercial. No puedo adjuntar evidencias. | Siento que me evalúan con números que no conozco y no puedo mostrar lo que hice. | Media | `GET /empleo/pendientes` (objetivos); `AutoevaluacionRequest` solo trae calificaciones y comentario | Mostrar quién y cuándo midió el avance, dejar adjuntar evidencias desde el portal y revisar que los objetivos correspondan al cargo. |
| EMP-24 | Oferta y contrato | Leer la pantalla de la oferta | Usa palabras como «Huella: …», «tu IP», «una huella del texto» y «la firma electrónica certificada llega más adelante». No hay PDF del contrato para guardar ni a quién preguntar. | No entiendo si estoy firmando de verdad. Antes de aceptar quiero preguntar algo. | Baja | `OfertaCandidato.tsx` | Usar palabras simples («Guardamos la fecha y la hora en que aceptaste»), poner «Descargar contrato en PDF» y «Tengo una pregunta» (WhatsApp o chat). |
| EMP-25 | Pulso de ingreso | Leer el pulso | Pregunta «¿Recomendarías esta empresa a un amigo?». Estoy en misión: ¿cuál empresa, la EST o Retail Express? Dice «Recursos Humanos lo lee» pero no qué hacen con mi respuesta. | Si no sé de qué empresa me preguntan, respondo cualquier cosa. Me da miedo que una respuesta mala me perjudique. | Media | `PulsoEmpleado` en `MisDocumentos.tsx` | Nombrar la empresa en la pregunta y decir «No es anónimo; lo usamos para acompañarte, no para evaluarte». |
| EMP-26 | Hoja de vida en PDF | Descargar mi hoja de vida | «Descargar en PDF» abre el diálogo de imprimir del navegador. | En el celular (sobre todo en iPhone) no entiendo cómo guardarla. | Baja | `HojaDeVidaPdf.tsx` (`window.print()`) | Generar y descargar el archivo PDF directamente, o compartirlo por WhatsApp. |
| EMP-27 | Mi hoja de vida | Subir una hoja de vida nueva después de escribir la mía a mano | Lo que leyó la IA **reemplaza** la experiencia y los estudios que escribí, y solo se recupera con «Descartar». | Pierdo lo que escribí con esfuerzo si no me fijo. | Baja | `MiHojaDeVida.tsx` (`experiencia: d.experiencia`) | Unir lo que escribí con lo que leyó la IA, o preguntar «¿Reemplazar o agregar?». |
| EMP-28 | Mis documentos | Subir un certificado ya vencido y quitar un documento | Aceptó un vencimiento de 2024 sin decir nada y luego salió «Vencido». «Quitar» borra sin preguntar. | Envío documentos vencidos sin saberlo, o borro uno por error. | Baja | `POST /portal/cuenta/documentos?venceEn=2024-01-01` → 200; `FilaDocumento` | Avisar al subir «Esta fecha ya pasó» y confirmar antes de quitar. |
| EMP-29 | Mi empleo | Revisar mis datos | Mi ciudad dice Medellín y mi sede «Bogotá · Calle 80». Nadie avisa que no cuadra. | Si los datos no cuadran, me pueden mandar papeles o la caja de compensación a la ciudad equivocada. | Baja | `GET /portal/cuenta/empleo` | Una regla que me pregunte «¿Sigues viviendo en Medellín? Tu sede es en Bogotá». |
| EMP-30 | Actualizar mis datos | Escribir la ciudad y la EPS | Aquí son texto libre. En la postulación la ciudad sale de una lista (DANE). | Escribo «Bogota» o «nueva eps» y queda mal escrito en mi expediente. | Baja | `FormularioDatos` en `MisDocumentos.tsx` | Usar el mismo buscador de municipios y una lista de EPS, fondos y cajas. |
| EMP-31 | Preguntas de la empresa | Responder las preguntas de una vacante | Algunas me tratan de usted («¿Tiene disponibilidad para turnos nocturnos?») y otras de tú. | Parece de otra empresa. Es un detalle, pero se nota. | Baja | `VAC-261030-3CB1` | Guiar a quien publica para que escriba siempre de tú, como el resto del portal. |

## 4. Lo que no entendí

- **«Selección masiva»** en el título de una vacante: ¿es para mucha gente? ¿Me van a tratar distinto?
- **«Huella»**, **«IP»**, **«firma electrónica certificada llega más adelante»** en la oferta: ¿entonces no estoy firmando?
- **«En revisión»** en mi soporte: ¿lo está leyendo la IA o lo tiene RRHH? ¿Cuánto se demora?
- **«Pruebas»** como estado de mi proceso sin ninguna prueba que presentar.
- **«Autorización versión v3» o «portal-v1»** en «Mi cuenta»: no sé qué significa la versión.
- **«Revocatoria»** frente a **«Supresión»**: en la pantalla dicen «Retirar mi autorización» y «Borrar mis datos». No sé cuál me sirve si sigo trabajando en la empresa.
- **El avance de mis objetivos (44 %)**: ¿de dónde sale?
- **«Por qué encaja contigo: Tus estudios: Bachiller académico»**: todos somos bachilleres, eso no me dice nada.
- ¿Quién es **Selvana**? La autorización dice «Autorizo a Selvana…», pero yo me postulé a Retail Express o a Talento Andino.

## 5. Lo que me falta para usarlo en mi trabajo real

- **Recuperar la contraseña**, y entrar con el celular o un código por WhatsApp en vez de correo y contraseña.
- **Avisos por WhatsApp o SMS** cuando tengo un pendiente (pruebas, oferta, pulso, campaña). Hoy tengo que entrar a mirar. Sé que las integraciones van
  al final, pero sin avisos se me pasan los plazos.
- **Postularme sin hoja de vida en archivo** y con el perfil que escribí a mano (EMP-02, EMP-03).
- **Saber dónde saco mis desprendibles y mi certificado laboral.** Entiendo que eso no lo hace Selvana sino la nómina, pero en «Mi empleo» debería haber
  una línea que diga «Tus desprendibles y certificados los pides en [canal de la nómina]».
- **Ver qué pasa con lo que envío:** el número de radicado de mis solicitudes, el estado de mis cambios de datos y lo que leyó la IA de mis soportes.
- **Datos que sí cambian en la vida real:** cuenta bancaria, dirección de residencia (aunque sea solo para RRHH) y contacto de emergencia. Hoy solo puedo
  proponer celular, ciudad, EPS, pensión y caja.
- **Guardar el avance de las pruebas** si se me cae la señal.

## 6. Uso de IA

Hice las 2 acciones de IA permitidas:

1. **Lector de hoja de vida del portal** (`POST /portal/cuenta/hoja-de-vida`, PDF de una página). Tardó unos 20 segundos y quedó en estado «Leida». Sacó
   bien el nombre, la cédula, el celular, el correo, la ciudad, las tres experiencias con fechas, los estudios, los dos cursos y siete habilidades. El
   único error fue una habilidad repetida. **Muy útil**: me ahorró escribir todo en el celular. Lo que leyó llenó el perfil y quedó editable.
2. **Lectura de un soporte en «Mi empleo»** (certificado de afiliación a Nueva EPS). Lo subí y arrancó la lectura, pero desde el portal **no puedo ver
   el resultado**: el documento sigue «En revisión» y RRHH decide en la bandeja. Para mí, como empleado, la utilidad todavía no se nota (EMP-11).

No envié la postulación a «Mercaderista» porque al enviarla arranca el ranking con IA, y eso habría sido una tercera acción.

## 7. Mi veredicto

**Para buscar empleo lo recomendaría hoy a un amigo, con cuidado. Para mi trabajo actual, todavía no.** El portal de empleo es claro y rápido, y que la
IA lea la hoja de vida es lo mejor que he visto en un portal. Pero hay cosas que hacen que alguien como yo abandone o desconfíe: salarios por debajo del
mínimo publicados, no poder postularme sin archivo, quedarme «en pruebas» sin pruebas, no poder recuperar la contraseña y no ver en el inicio lo que me
pide mi empresa.

Lo primero que cambiaría:

1. Que el perfil escrito a mano sirva para postularse, y la opción «No tengo hoja de vida» (EMP-02, EMP-03).
2. Recuperar la contraseña (EMP-10).
3. Que todos los pendientes, incluidos los de «Mi empleo», salgan en el inicio con su fecha (EMP-01, EMP-05).
4. No dejar publicar salarios por debajo del mínimo (EMP-04).
5. Validar los datos que propongo y dejarme corregirlos (EMP-06, EMP-07, EMP-08).
6. Separar en la autorización la parte de «vacantes similares» y decir claramente dónde actúa la IA (EMP-09, EMP-20).
