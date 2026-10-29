# Marca de Selvana · ruta Dosel

Aprobada el 2026-10-21. Propuesta visual completa (logo, componentes, patrones de IA y pantallas): https://claude.ai/artifact/L7aMUy8QvJPfbjev2wk2V8

Tokens en código: `frontend/src/theme/tokens.css`. Este documento es la referencia hasta que el manual de marca y el design system queden en Claude Design.

## Concepto

El dosel es la capa alta de la selva, donde llega la luz y desde donde crece todo lo de abajo. Así trabaja Selvana: los agentes hacen el trabajo pesado y dejan la decisión lista, explicada y con evidencia. **La IA trabaja, el humano decide.**

Se descartaron las rutas Guacamaya (rojo, amarillo y azul; choca con los colores semánticos y cansa en la consola) y Páramo (serif editorial fría; distante para EST y BPO).

## Personalidad

- **Clara:** dice qué hizo, por qué y qué falta. Nunca esconde la lógica de un puntaje.
- **Cálida:** habla de personas, no de registros. Tutea al candidato y al empleado, usa usted con el cliente empresa.
- **Rigurosa:** cita la evidencia, registra cada decisión y respeta la Ley 1581.
- **Colombiana:** pesos, cédulas, EPS, ARL y WhatsApp como canal principal, sin acentos forzados.

## Tono de voz

| Contexto | Sí | No |
| --- | --- | --- |
| Agente al reclutador | Leí 128 hojas de vida. 34 cumplen todo. Te propongo esta terna; revisa las razones antes de aprobar. | ¡Proceso completado exitosamente! La IA ha seleccionado a los mejores candidatos. |
| Candidato por WhatsApp | Hola, Andrés. Vi tu hoja de vida para Asesor de servicio. ¿Puedes trabajar turnos rotativos en Chapinero? | Estimado aspirante, su postulación ha sido recibida en nuestro sistema de gestión. |
| Error | No pudimos leer la cédula: la foto salió borrosa. Tómala con buena luz y sin flash. | Error 422: documento inválido. Intente nuevamente. |

## Logo

- **Wordmark:** `selvana` en minúscula, Bricolage Grotesque 800, tracking −0,045 em.
- **Isotipo:** dos hojas que forman una S (la hoja superior es la empresa, la inferior la persona) y una semilla violeta (la IA, pequeña y nunca al centro). Funciona desde 16 px. Archivo base: `frontend/public/favicon.svg`.
- **Variantes:** horizontal principal, sobre fondo selva (hojas blanca y #7FDCC8, semilla #C590F0; tokens `--color-sobre-selva`, `--color-sobre-selva-2` y `--color-ia-sobre-selva`), monocromo, ícono de app (fondo #0B5D52, radio 24/96).
- **Logo animado** (`LogoAnimado` en `frontend/src/shared/ui/LogoAnimado.tsx`): solo en la página comercial, el inicio de sesión y pantallas de espera. Al entrar, las dos hojas brotan desde el centro y la semilla cae a su lugar; después las hojas se mecen en contrafase (±4°, 6 s) y la semilla late suave como la IA que trabaja. Con `prefers-reduced-motion` queda quieto. En la consola se usa el isotipo estático.

## Color

| Token | Claro | Oscuro | Uso |
| --- | --- | --- | --- |
| Selva (`--color-primario`) | #0B5D52 | #3FC2AA | Marca, acciones humanas, botón primario, navegación activa |
| Orquídea (`--color-ia`) | #8A3FC4 | #C590F0 | **Solo IA**: sugerencias, campos prellenados, estados del agente |
| Tinta (`--color-texto`) | #10211B | #E6EFEB | Texto |
| Selva profundo (`--color-selva-profundo`) | #073F38 | #0B2C27 | Fondo del riel de la consola y paneles de marca; encima van `--color-sobre-selva` y `--color-sobre-selva-2` |
| Claro de bosque (`--color-fondo`) | #F3F6F3 | #0C1513 | Fondo de la aplicación |
| Éxito / alerta / error / info | #2A8A3E / #B86E00 / #C23A2E / #2A62B8 | #5CC774 / #F0A63A / #F07A6E / #7AA8F0 | Estados |

Neutros siempre con sesgo verde, nunca gris puro. Todos los pares texto/fondo cumplen WCAG 2.1 AA.

**Regla del violeta.** Si un elemento tiene violeta orquídea, lo produjo la IA y todavía no lo ha confirmado una persona. Al aprobarlo pierde el violeta y pasa a tinta o verde. El botón violeta solo delega trabajo a un agente; nunca confirma una decisión sobre una persona.

## Tipografía

Las tres son libres (Google Fonts), sin costo de licencia para el SaaS ni para la marca blanca.

| Rol | Familia | Uso |
| --- | --- | --- |
| Display | Bricolage Grotesque 600/800 | Títulos, cifras grandes, wordmark |
| Texto e interfaz | Instrument Sans 400–700 | Todo el texto de la UI |
| Datos y evidencia | IBM Plex Mono 400/500 | Puntajes, cédulas, montos, citas de evidencia, auditoría |

Escala: display 56/60 · h1 32/38 · h2 22/28 · body 15/24 · label 13/18 · data 14/20.

## Tokens

- **Espaciado:** base 4 px → 4 · 8 · 12 · 16 · 24 · 32 · 48.
- **Radios:** s 6 (controles) · m 10 (tarjetas) · l 16 (paneles) · full (chips y estados).
- **Elevación:** 0 para la mayoría, 1 para menús, 2 solo para modales y el panel de decisión.
- **Breakpoints:** 400 · 768 · 1024 · 1440.
- **Movimiento:** duraciones corta 160 ms (hover, foco), media 320 ms (paneles, cambios de estado) y larga 700 ms (entradas de sección); curvas `--curva-salida` para entrar y `--curva-organica` para lo que se mece. El movimiento es orgánico, como hojas con viento: aparecer subiendo un poco, mecerse, crecer. Nada de rebotes de caricatura. En la consola el movimiento es mínimo y funcional; la página comercial puede ser expresiva. Todo respeta `prefers-reduced-motion`.

## Patrones de IA

- **Tarjeta de explicación:** 3 a 5 razones con signo (+/−) y cita de evidencia (`HV p.1`, `WhatsApp 14:32`), nota de variables protegidas excluidas y acción para corregir.
- **Estados del agente:** pensando · trabajando (con avance real, nunca un spinner sin número) · espera aprobación · listo (con quién aprobó) · necesita ayuda.
- **Asistente con fuentes:** cada respuesta cita su origen.
- **Nivel de autonomía:** control segmentado Sugerir · Preparar y aprobar (por defecto) · Autónomo con límites.

## Normas de interfaz

- **Paginación del lado del servidor** en toda tabla o lista que pueda crecer: 10 filas por defecto con selector 10 · 25 · 50 · 100 (la bandeja de aprobaciones arranca en 5, con selector 5 · 10 · 25 · 50), rango visible en monoespaciada ("1–10 de 954"), Anterior/Siguiente y buscador.
- **Enlaces:** verde selva, sin subrayado hasta pasar el cursor; en tablas, color de texto y fila resaltada. Nunca el azul o morado del navegador.
- **Carga en los botones**: todo botón que llama a la API se deshabilita, muestra un indicador circular y cambia el texto al gerundio ("Guardando…").
- **Esqueletos**: mientras llegan los datos se dibuja la forma final de la pantalla con bloques en `--color-superficie-2`; nunca una pantalla en blanco ni un "Cargando…" suelto.
- **Menú de la consola (riel)**: riel fijo en selva profundo que nunca se desplaza: contraído (76 px) muestra solo íconos de línea y al acercar el mouse o con el teclado se expande (248 px) encima de la página con los nombres y el logo animado; al fondo caen hojas suaves y se mece una hoja grande. Solo lo diario va en el riel (Bandeja, Vacantes, Candidatos, Vinculación, Empleados); la configuración de la empresa y la cuenta (empresa en la que trabaja, tema, salir) se abren en cajones flotantes al lado, con velo sobre la página y cierre con Escape o clic afuera. El activo lleva la píldora menta y una marca lateral que crece. En celular el riel pasa a ser una barra abajo y los cajones suben como hojas.
- **Datos maestros editables**: sedes, áreas, centros de costo y cargos se editan y se activan o desactivan en Selvana; la sincronización con el ERP no pisa lo que editó una persona. Editar lo que propuso la IA lo confirma (pierde el violeta).

## Principios de UX

1. **Bandeja antes que menú:** cada rol abre en lo que tiene que decidir hoy; la consola RRHH abre en la bandeja de aprobaciones.
2. **Nada sin explicación:** todo puntaje o sugerencia muestra sus razones con fuente y se puede corregir.
3. **Prellenado, nunca en blanco:** ningún formulario aparece vacío si la IA puede llenarlo.
4. **Celular primero para el operativo:** WhatsApp y web móvil liviana, sin app que instalar.
5. **Todo lo de la IA también se hace a mano:** cada propuesta de la IA se edita, se crea desde cero o parte de una plantilla o regla de base, y lo que la IA usa para trabajar se configura en la pantalla. Si la IA falla o está apagada, el flujo sigue a mano: nunca un botón violeta como único camino.
