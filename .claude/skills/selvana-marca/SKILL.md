---
name: selvana-marca
description: Marca Dosel y design system de Selvana. Úsala SIEMPRE antes de diseñar o programar cualquier pantalla, componente, mock, prototipo, artboard, página, correo, presentación, ícono o pieza visual de Selvana (frontend React, Claude Design, artifacts, portal de empleo, WhatsApp). Aplica colores, tipografía, tokens, tono de voz, regla del violeta y patrones de IA aprobados.
---

# Marca y design system de Selvana (ruta Dosel)

Antes de producir cualquier cosa visual de Selvana, lee estas fuentes y aplícalas. No inventes colores, fuentes ni estilos nuevos.

| Fuente | Qué tiene |
| --- | --- |
| `docs/diseno/marca.md` | Manual de marca aprobado: concepto, tono, logo, color, tipografía, tokens, patrones de IA, principios de UX |
| `frontend/src/theme/tokens.css` | Tokens en código (claro y oscuro). Fuente de verdad de los valores |
| `docs/diseno/propuesta-marca.html` | Propuesta visual completa con componentes y patrones renderizados |
| `docs/diseno/mock-app-web/project/*.dc.html` | Mock de la consola RRHH: referencia de layout, navegación y copy |
| `frontend/public/favicon.svg` | Isotipo (dos hojas y semilla) |

## Reglas que nunca se rompen

1. **Solo tokens.** En el frontend usa las variables de `tokens.css` (`var(--color-primario)`, `var(--espacio-4)`, `var(--radio-m)`, `var(--fuente-display)`). Nunca un hex suelto en un componente. Si falta un token, agrégalo en `tokens.css` (claro y oscuro) y en `marca.md`, no en el componente.
2. **Regla del violeta.** `--color-ia` (orquídea #8A3FC4 / #C590F0) se usa **solo** en lo que produjo la IA y nadie ha confirmado: sugerencias, campos prellenados, estados del agente, botón "Pedir a la IA". Cuando una persona confirma, el elemento pasa a tinta o verde. El botón violeta nunca confirma una decisión sobre una persona.
3. **Verde selva para decidir.** `--color-primario` (#0B5D52) es la marca y las acciones humanas. Un solo botón primario por vista.
4. **Tipografía.** Bricolage Grotesque (títulos y cifras grandes), Instrument Sans (toda la UI), IBM Plex Mono (puntajes, cédulas, montos, citas de evidencia, auditoría). Ninguna otra familia.
5. **Neutros con sesgo verde**, nunca gris puro. Modo claro y oscuro siempre. Contraste WCAG 2.1 AA (texto pequeño ≥ 4,5:1; para texto tenue sobre blanco usa #5F7169 o más oscuro).
6. **Espaciado base 4 px** (4 · 8 · 12 · 16 · 24 · 32 · 48). Radios: s 6 controles, m 10 tarjetas, l 16 paneles, full chips. Sombra 2 solo en modales y panel de decisión.
7. **Enlaces de marca**, nunca el azul ni el morado del navegador: verde selva sin subrayado (aparece suave al pasar) y, dentro de tablas, color de texto con la fila resaltada en `--color-superficie-2`.
8. **Íconos de línea** (trazo 1,6, esquinas redondeadas), nunca emoji en la interfaz.

## Patrones obligatorios en pantallas con IA

- **Explicación:** toda sugerencia o puntaje muestra 3 a 5 razones con signo (+/−) y una cita de evidencia en monoespaciada (`HV p.1`, `WhatsApp 14:32`), la nota "No se usaron para el puntaje: edad, género, estado civil, foto ni dirección exacta" y una acción para corregir.
- **Estados del agente:** pensando · trabajando (con avance real, nunca un spinner sin número) · espera aprobación · listo (con quién aprobó) · necesita ayuda.
- **Decisión:** barra con el botón primario, "Ajustar" y el texto "Se registran la sugerencia y tu decisión". Descartar, contratar, sancionar o calificar siempre requiere aprobación humana.
- **Autonomía:** control segmentado Sugerir · Preparar y aprobar (por defecto) · Autónomo con límites.

## Normas generales de interfaz (obligatorias en toda pantalla)

1. **Paginación en toda tabla o lista que pueda crecer** (cargos, empleados, candidatos, vacantes, bitácora, autorizaciones…). Siempre del lado del servidor: la API recibe `pagina` y `tamano` (**10 por defecto**, máximo 100) y devuelve `{ items, pagina, tamano, total }`. La interfaz muestra el rango en monoespaciada ("1–10 de 954"), el selector de registros por página **10 · 25 · 50 · 100** (`usePaginacion` + `Paginacion` con `onCambiarTamano`); excepción: la **bandeja de aprobaciones** arranca en **5** con selector 5 · 10 · 25 · 50 (`TAMANOS_BANDEJA`), Anterior/Siguiente y un buscador cuando hay más de una página. Nunca traer todo para paginar en el navegador.
2. **Todo botón que llame a la API muestra su carga**: se deshabilita, marca `aria-busy`, muestra un indicador circular pequeño y cambia el texto al gerundio ("Guardando…", "Aplicando…", "Aprobando…"). Se usa el componente `Boton` con la propiedad `cargando`; nunca un botón que se pueda pulsar dos veces mientras espera.
3. **Esqueletos de carga, nunca pantallas en blanco ni "Cargando…" sueltos**: mientras llegan los datos se muestra la forma final de la pantalla (tarjetas, filas, panel de detalle) con bloques `Esqueleto` del tono `--color-superficie-2`. El brillo respeta `prefers-reduced-motion`. Un esqueleto por cada bloque que espera datos, con el mismo alto que el contenido real para que nada salte.
4. **Datos maestros editables**: lo que llega del ERP (sedes, áreas, centros de costo, cargos) se puede editar y activar o desactivar desde Selvana. Lo que edita una persona no lo pisa la siguiente sincronización. Editar un campo que propuso la IA cuenta como confirmación: pierde el violeta y queda registrado quién lo cambió.
5. **Movimiento con la marca**: usa los tokens `--duracion-corta/media/larga` y `--curva-salida/organica`. Las páginas públicas (comercial, ingreso, portal de empleo, postulación) son expresivas: `LogoAnimado`, `HojasCayendo`, entradas escalonadas, apariciones al desplazar (`useRevelarAlDesplazar`), contadores (`useContador`) y transiciones entre pasos. La consola usa movimiento mínimo y funcional. Nada de rebotes de caricatura, y todo se apaga con `prefers-reduced-motion`. Las hojas decorativas van en verde; el violeta sigue reservado para la IA.

## Principios de UX

1. **Bandeja antes que menú:** cada rol abre en lo que tiene que decidir hoy.
2. **Nada sin explicación.**
3. **Prellenado, nunca en blanco:** ningún formulario vacío si la IA puede llenarlo.
4. **Celular primero para el operativo:** WhatsApp y web móvil liviana.

## Tono de voz

Clara, cálida, rigurosa, colombiana. Tutea a candidatos y empleados; usted con el cliente empresa. Frases cortas y activas. Los errores dicen qué pasó y cómo arreglarlo, sin códigos ("No pudimos leer la cédula: la foto salió borrosa. Tómala con buena luz y sin flash."). Nada de "¡Proceso completado exitosamente!". Español de Colombia: pesos con punto de miles (`$1.950.000`), cédula `CC 1.023.456.789`, EPS, ARL.

## Marca blanca

En el portal de empleo, el cliente cambia logo y color primario. Lo que no cambia: el violeta de IA, la tipografía, los patrones de explicación y la autorización de la Ley 1581.

## Antes de entregar

Revisa: ¿las tablas grandes paginan en el servidor? ¿cada botón que llama a la API muestra carga? ¿hay esqueleto mientras cargan los datos? ¿solo tokens? ¿el violeta está solo en lo de IA? ¿funciona en oscuro? ¿contraste AA? ¿toda sugerencia tiene explicación? ¿el copy suena a Selvana? Si cambias la marca, actualiza `marca.md` y `tokens.css` en el mismo cambio.
