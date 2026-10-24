# Diseño y marca

Marca aprobada el 21 de octubre de 2026: **ruta Dosel**. Verde selva para la marca y las decisiones humanas, violeta orquídea solo para lo que produce la IA.

> **La IA trabaja, el humano decide.**

## Dónde está todo

| Qué | Dónde |
| --- | --- |
| Manual de marca (concepto, tono, logo, color, tipografía, tokens, patrones de IA) | [`docs/diseno/marca.md`](/docs/diseno/marca.md) en el repositorio |
| Propuesta visual completa (HTML, se abre en el navegador) | [`docs/diseno/propuesta-marca.html`](/docs/diseno/propuesta-marca.html) · versión publicada: https://claude.ai/artifact/L7aMUy8QvJPfbjev2wk2V8 |
| Validación del diseño contra las historias de la Etapa 0 (matriz y criterios de aceptación) | [`docs/diseno/validacion-etapa-0.md`](/docs/diseno/validacion-etapa-0.md) |
| Mock navegable de la consola RRHH (9 pantallas) | [`docs/diseno/mock-app-web/`](/docs/diseno/mock-app-web) · versión publicada: https://claude.ai/artifact/LZQbmn2f1efZhUML9Wibfn |
| Tokens en código (claro y oscuro) | [`frontend/src/theme/tokens.css`](/frontend/src/theme/tokens.css) |
| Isotipo | [`frontend/public/favicon.svg`](/frontend/public/favicon.svg) |
| Skill de Claude Code que aplica la marca en cada pantalla | [`.claude/skills/selvana-marca/SKILL.md`](/.claude/skills/selvana-marca/SKILL.md) |

## Resumen de la marca

- **Concepto:** el dosel es la capa alta de la selva, donde llega la luz. Los agentes hacen el trabajo pesado y dejan la decisión lista, explicada y con evidencia.
- **Isotipo:** dos hojas que forman una S (la empresa y la persona) y una semilla violeta (la IA). Versión animada (`LogoAnimado`) en la página comercial, el inicio de sesión y el portal de empleo: las hojas brotan, la semilla cae y todo se mece.
- **Movimiento:** orgánico, como hojas con viento (tokens `--duracion-*` y `--curva-*`). Expresivo en las páginas públicas, mínimo en la consola, y siempre se apaga con movimiento reducido.
- **Personalidad:** clara, cálida, rigurosa, colombiana.
- **Color:** Selva `#0B5D52` · Orquídea `#8A3FC4` (solo IA) · Tinta `#10211B` · Claro de bosque `#F3F6F3`. Semánticos: éxito `#2A8A3E`, alerta `#B86E00`, error `#C23A2E`, información `#2A62B8`.
- **Tipografía:** Bricolage Grotesque (títulos), Instrument Sans (interfaz), IBM Plex Mono (datos y evidencia).
- **Regla del violeta:** si un elemento es violeta, lo produjo la IA y nadie lo ha confirmado. Al aprobarlo pasa a tinta o verde.

## Mock de la consola RRHH

| Pantalla | Qué muestra |
| --- | --- |
| 1 · Bandeja de aprobaciones | Inicio de RRHH: estado de los agentes, decisiones pendientes y terna con explicación por candidato |
| 2 · Vacantes | Crear una vacante con una frase; borrador de la IA en violeta; tabla de vacantes activas |
| 3 · Embudo de la vacante | Métricas (tiempo a terna, respuesta en WhatsApp, correcciones a la IA, costo de IA) y etapas tipo tablero |
| 4 · Perfil del candidato | Datos leídos de la HV, inconsistencias, explicación corregible, conversación de WhatsApp y pruebas |
| 5 · Agentes y autonomía | Nivel de autonomía por agente, meta de calidad y costo del mes |
| 6 · Conectar nómina | Cuatro niveles de conexión; la IA ordena lo que trae SINCO (cargos repetidos, centros de costo vacíos) y usted aprueba antes de aplicar |
| 7 · Estructura y cargos | Sedes, áreas, centros de costo y cargos; la IA redacta el perfil de cada cargo con su explicación |
| 8 · Usuarios, roles y empresas usuarias | Invitar con rol sugerido por la IA, matriz de permisos legible y empresas usuarias para EST |
| 9 · Privacidad y auditoría | Bitácora que distingue IA y personas, autorizaciones Ley 1581 con evidencia y solicitudes de titulares con plazo legal |

Para abrir el mock localmente se necesita el editor de Design de Claude; la versión publicada es la forma rápida de navegarlo.

## Principios de UX

1. Bandeja antes que menú.
2. Nada sin explicación.
3. Prellenado, nunca en blanco.
4. Celular primero para el personal operativo.

## Pendiente de la Etapa D

- Estilo de ilustración.
- Completar en Claude Design el design system (tokens y componentes) y las pantallas restantes.
- Portal de empleo, portal del líder, portal del empleado y flujos de WhatsApp completos.
- Storybook y marca blanca configurable.
