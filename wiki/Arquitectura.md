# Arquitectura: .NET + React, monolito modular

**Decisión: monolito modular en .NET con un servicio aparte para la IA.** No microservicios desde el inicio. Se diseña con límites claros por módulo para poder separar un módulo en microservicio cuando haya una razón medible.

**Por qué no microservicios ahora**

- Con un equipo de 7 personas, cada microservicio suma despliegue, monitoreo, versiones de contratos y transacciones distribuidas; eso frena la entrega de las primeras etapas.
- Los límites del dominio (qué es Selección y qué es Vinculación) se van a ajustar con los primeros clientes; mover código dentro de un monolito es barato, entre servicios es caro.
- Un solo despliegue simplifica las integraciones con los ERP y las pruebas de punta a punta.

**Por qué la IA sí va aparte desde el día uno**

- Sus tareas son largas (leer cientos de hojas de vida, conversaciones por WhatsApp) y deben correr en segundo plano sin afectar la API.
- Escala distinto: en una convocatoria masiva hay que multiplicar los workers de IA, no toda la aplicación.
- Permite cambiar de proveedor de modelo sin tocar los módulos de negocio.

**Piezas desplegables**

| Pieza | Tecnología | Responsabilidad |
| --- | --- | --- |
| Frontend | React + TypeScript (Vite), TanStack Query, librería de componentes del ERP | Consola de la empresa (RRHH y «Mi equipo» del líder), portal del empleado, portal de empleo, bandeja de aprobaciones |
| API Selvana (monolito modular) | ASP.NET Core Web API en .NET 10 (LTS), EF Core con proveedor Npgsql | Todos los módulos de negocio, seguridad, integración con ERP |
| Servicio de IA (worker) | .NET Worker Service con Microsoft.Extensions.AI o Semantic Kernel | Agentes, extracción de documentos, ranking, resúmenes, conversación |
| Mensajería | HTTP con outbox e inbox, sin broker (ADR 0008) | Eventos entre API y servicio de IA (“HV recibida”, “ranking listo”) |
| Autenticación | Sesión propia con cookie HttpOnly y contraseña PBKDF2, más OpenID Connect opcional (ADR 0009) | Ingreso a la consola, bloqueo por intentos |
| Auditoría | Bitácora encadenada con SHA-256 en Core, alimentada por el outbox de cada módulo (ADR 0010) | Quién hizo qué (persona, IA, sistema o titular), accesos a datos personales, Ley 1581 |
| Tiempo real | SignalR | Bandeja de aprobaciones y avance de agentes en vivo |
| Datos | PostgreSQL (gratuito, un esquema por módulo) con EF Core + Npgsql, extensión pgvector para búsqueda vectorial + almacenamiento de archivos cifrado | Datos de negocio, documentos, búsqueda semántica de candidatos |
| Observabilidad | OpenTelemetry, logs centralizados | Trazas por agente, costo de IA por cliente |

**Estructura de la solución .NET**

- `Selvana.Api` — punto de entrada, autenticación (OpenID Connect: identidad propia o del cliente, como Microsoft Entra ID o Google)
- `Selvana.Modules.Core` — Persona, empresa, sedes, cargos, roles
- `Selvana.Modules.Reclutamiento` — requisiciones, vacantes, candidaturas, pruebas
- `Selvana.Modules.Vinculacion` — contratos, documentos, onboarding, misiones
- `Selvana.Modules.Expediente` — hoja de vida corporativa, autoservicio
- `Selvana.Modules.Desempeno`, `.Formacion`, `.Clima` — se crean en su etapa
- `Selvana.Integrations` — contrato canónico de nómina + un adaptador por ERP (Sinco, Siigo, World Office, SAP, archivo plano…)
- `Selvana.AI.Worker` — servicio de IA independiente
- `Selvana.Shared` — contratos de eventos, utilidades

**Reglas para que el monolito no se vuelva un desorden**

- Cada módulo tiene su propio esquema de base de datos; ningún módulo consulta tablas de otro.
- Los módulos se comunican por interfaces públicas o eventos internos, nunca por clases internas.
- Pruebas de arquitectura automáticas (por ejemplo NetArchTest) que fallan si se rompe una regla.
- Cada módulo con arquitectura limpia: Dominio, Aplicación (casos de uso), Infraestructura, API.

**Arquitectura limpia, SOLID y CQRS en el backend:** cada módulo separa Domain, Application, Infrastructure, Api y Contracts, y las dependencias apuntan siempre al dominio. Los principios SOLID se aplican de forma estricta: un handler por caso de uso, decoradores para lo transversal, interfaces pequeñas y todo por inyección de dependencias. Con CQRS, los comandos escriben un solo agregado y devuelven Result, y las consultas proyectan a DTO sin tracking. Se usa un dispatcher propio en lugar de MediatR, que tiene licencia comercial. CORS usa una política con orígenes desde configuración. El detalle está en [Backend: arquitectura limpia, SOLID y CQRS](/Arquitectura/Backend-arquitectura-limpia-SOLID-CQRS).

**Cuándo extraer un módulo a microservicio:** cuando tenga una carga muy distinta al resto (por ejemplo el portal de empleo en convocatorias masivas), un ritmo de despliegue propio o un equipo dedicado. Gracias a las reglas anteriores, extraerlo es mover el módulo y cambiar llamadas internas por eventos.

La solución base, el pipeline de CI/CD y los contenedores Docker se montan en el sprint 2 de la Etapa 0.

**Base de datos: PostgreSQL.** Es gratuito, ya está instalado en el equipo de desarrollo y cubre todo lo que necesita Selvana: esquemas por módulo, JSONB para formularios configurables, búsqueda de texto en español y pgvector para la búsqueda semántica de candidatos, así no hace falta un motor vectorial aparte. Sin importar qué motor use el ERP del cliente, la integración se hace por API o eventos, no compartiendo base de datos.

---
_Actualizado: 2026-10-21 · Autor: Ricardo Nivia_
