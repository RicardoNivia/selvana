# Etapa 1 – Agente Reclutador IA (sprints 4 a 10, 14 semanas)

Objetivo: que una vacante pase de solicitud a terna aprobada con la IA haciendo el trabajo operativo. Es la primera versión que se vende.

**Sprint 4 – Vacante en un minuto**

- [x] El líder escribe en lenguaje natural (“necesito 5 auxiliares de bodega para Funza, turno noche”) y la IA crea la requisición
- [x] Generación de perfil de cargo, aviso, preguntas filtro y competencias desde el catálogo de cargos o desde cero (el redactor parte del perfil del cargo elegido; sin cargo, desde cero y se puede agregar al catálogo)
- [x] Flujo de aprobación de la requisición (líder → RRHH → gerencia) en la bandeja de aprobaciones: la etapa que puede aprobar quien la pide se aprueba sola, cuatro ojos, devolver con motivo y la última aprobación publica

**Sprint 5 – Portal de empleo y captación**

- [x] Portal de empleo público de Selvana (`/empleos`): reúne las vacantes publicadas de todas las empresas clientes (y de sus empresas usuarias), con búsqueda, filtros (ciudad, cargo, empresa, jornada, salario) y una página por vacante para compartir
- [ ] Portal de empleo por empresa (y por empresa usuaria en temporales) con marca propia
- [x] Postulación con hoja de vida interpretada por la IA: el candidato sube su HV (PDF, Word o foto), la IA extrae todos los datos, el candidato los confirma o corrige y envía; sin formularios en blanco
- [x] Postular con LinkedIn: el candidato pega el enlace de su perfil y sube el PDF que LinkedIn genera («Más → Guardar en PDF»); la IA lo lee como una hoja de vida y la empresa ve el enlace. LinkedIn no permite leer perfiles solo con el enlace
- [x] Portal rediseñado: búsqueda con oficios que rotan, búsquedas rápidas, filtros en chips con la URL compartible, tarjetas animadas, detalle con barra fija para postularse en el celular y postulación por pasos con celebración al enviar
- [x] Autorización de datos en la postulación (Ley 1581): texto versionado, aceptación obligatoria y evidencia guardada
- [x] Captura del postulante en la consola: queda en la vacante con su HV y sus datos, sin duplicar a la persona (misma cédula) y arranca el proceso (lector, ranking y preselección). *Construido en back y front; falta la prueba de punta a punta contra PostgreSQL*
- [ ] Postulación en 2 minutos desde el celular o por WhatsApp *(WhatsApp va al final, con las integraciones)*
- [ ] Publicación en bolsas externas por enlace o integración
- [ ] *(Integración, al final)* Feed XML de vacantes para LinkedIn (Limited Listings), Jooble y Jora: un endpoint por empresa cliente, casilla «Publicar en LinkedIn (gratis)» en cada vacante, asistente en Configuración → Canales → LinkedIn (URL de la página de empresa, activar *basic job posting*, texto del ticket para LinkedIn listo para copiar), `applyUrl` que siempre vuelve al portal de Selvana con `utm_source` y estado por vacante (pendiente, visible o rechazada). Registro previo de Selvana como fuente ATS escribiendo a LTSATSINTEGRATIONS@linkedin.com. Riesgo: desde marzo de 2025 LinkedIn restringe los empleos de terceros; el producto muestra el estado de cada vacante y no promete que todas se publiquen

**Sprint 6 – Lector inteligente de hojas de vida**

- [x] Extracción de datos desde PDF, Word, imagen y texto: estudios, experiencia, habilidades, certificaciones
- [x] Normalización de cargos y habilidades contra un catálogo propio
- [x] Detección de vacíos e inconsistencias (fechas superpuestas, experiencia no sustentada)
- [x] Deduplicación de candidatos y banco de talento buscable en lenguaje natural (“montacarguistas con licencia C1 en Mosquera”)
- [x] Meta de calidad: ≥ 95 % de campos clave extraídos correctamente en el banco de pruebas

**Sprint 7 – Ranking explicable**

- [x] Puntaje de afinidad candidato-vacante con explicación en 3 a 5 razones y evidencia citada de la HV
- [x] Exclusión de variables protegidas (edad, género, estado civil, foto, dirección exacta) del puntaje
- [x] Propuesta de terna y tablero Kanban por vacante
- [x] Retroalimentación del reclutador (“buen/mal candidato”) que mejora el ranking por empresa
- [x] Reporte de sesgo por vacante
- [x] Filtros ATS con descarte sugerido que aprueba una persona (agregado el 23/10/2026)

**Sprint 8 – Agente conversacional de preselección**

- [ ] Conversación por WhatsApp y web: confirmar interés, aspiración salarial, disponibilidad, ubicación y preguntas técnicas básicas
- [x] Respuestas a preguntas del candidato sobre la vacante
- [x] Resumen de cada conversación en la ficha del candidato
- [x] Escalamiento a humano cuando el candidato lo pide o la IA no está segura

**Sprint 9 – Pruebas psicotécnicas automatizadas (suite propia, ADR 0011)**

- [x] Fase 1 · Motor y pruebas básicas: batería por cargo y por vacante, envío por enlace de un solo uso, pantalla del candidato con tiempo límite y consentimiento, calificación automática y resultados en la vacante; suite básica (atención y precisión, razonamiento numérico, juicio situacional de servicio, personalidad laboral IPIP en validación)
- [ ] Fase 2 · Instrumentos completos: bancos calibrados de personalidad y razonamiento, juicio situacional por cargo generado por la IA y validado por el psicólogo, informes narrativos con evidencia, edición de la suite desde la consola
- [ ] Fase 3 · Validación con datos colombianos: muestras de 300 a 500 personas por instrumento, confiabilidad, análisis factorial, baremos por sector e impacto adverso (en paralelo, 3 a 6 meses)
- [ ] Envío automático de pruebas, recordatorios y calificación (envío por WhatsApp y correo con las integraciones)
- [ ] Pruebas técnicas generadas por IA según el cargo (conocimientos, situacionales, digitación)
- [ ] Controles antifraude: tiempo límite, preguntas aleatorias, detección de respuestas copiadas
- [x] Informe comparativo de finalistas: HV, conversación y pruebas en una sola vista

**Sprint 10 – Estabilización y lanzamiento comercial**

- [ ] Tablero de selección: vacantes abiertas, tiempo por etapa, fuentes, costo IA por vacante
- [ ] Pruebas de carga para selección masiva (cientos de postulaciones por vacante)
- [ ] Autoservicio de alta: registro, prueba gratuita de 30 días, onboarding guiado dentro del producto
- [ ] Landing, video demo, casos de uso por sector y publicación del plan “Selección IA”
- [ ] Analítica de uso del producto para medir adopción sin depender de clientes piloto

**Gate de salida:** plan “Selección IA” publicado y a la venta; las primeras empresas en prueba gratuita cierran vacantes con el agente; los usuarios aceptan al menos 7 de cada 10 candidatos de la terna propuesta (medido en el producto).

---
_Actualizado: 2026-10-22 · Autor: Ricardo Nivia_
