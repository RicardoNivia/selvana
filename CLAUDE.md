# CLAUDE.md — Selvana

Contexto para Claude Code. Léelo completo antes de trabajar en este repositorio.

## Qué es Selvana

Módulo de talento humano **potenciado con IA**, vendido como producto SaaS independiente y **integrable con cualquier ERP que liquide nómina** (SINCO es solo el primer conector). Multisector: empresas de servicios temporales (EST) y outsourcing, BPO/call centers, retail, industria y logística, salud, construcción, corporativo.

**Regla de oro del producto: la IA trabaja, el humano decide.** Cada proceso tiene un agente que ejecuta el trabajo operativo y deja una bandeja de aprobaciones. Ninguna pantalla debe ser un formulario para llenar a mano si la IA puede prellenarlo. No es "una aplicación normal de talento humano que gestiona hojas de vida".

Las decisiones que afectan a una persona (descartar, contratar, sancionar, calificar desempeño) **siempre** requieren aprobación humana.

## Estado actual (2026-10-22)

- Fase: **Etapa 0 en curso**. Marca Dosel aprobada y mock de 9 pantallas (Etapa D). Hecho: gateway de IA (ADR 0007), mensajería HTTP outbox → inbox (ADR 0008), bloque 2.1 del sprint 2 (conectar nómina por archivo y estructura y cargos con IA, en back y front). Adelantado de la Etapa 1 (para tener postulantes): módulo `Reclutamiento` con vacante en una frase, portal de empleo público `/empleos`, postulación con HV leída por la IA, autorización Ley 1581 y captura en la consola; página comercial `/` e inicio de sesión `/ingresar` con sesión propia por cookie (ADR 0009; la consola vive en `/consola` y exige sesión). Bloque 2.2 hecho: usuarios y roles (invitación con enlace, rol sugerido por cargo, matriz de permisos como políticas de la API), empresas usuarias con NIT validado y alcance por usuario en Reclutamiento. Usuarios de prueba: `laura.moreno@serviciosandinos.co` (Gerente RRHH), `admin@serviciosandinos.co` (Administrador) y `camilo.duarte@serviciosandinos.co` (Analista, solo dos empresas usuarias); contraseña en los user-secrets de la API (`Desarrollo:Contrasena`). Bloque 2.3 hecho: bitácora encadenada (ADR 0010), autorizaciones Ley 1581, solicitudes de titulares con plazos hábiles y supresión, apertura auditada de hojas de vida y formulario público `/empleos/mis-datos`. Sprint 3 en curso: bandeja de aprobaciones genérica hecha (`/consola/bandeja`, eventos `Selvana.Core.Contracts.Bandeja`). Adelantada la suite psicotécnica propia, fase 1 (ADR 0011): batería por cargo y vacante, enlace `/pruebas/:token`, calificación y resultados; las pruebas siguen «en validación» hasta que el psicólogo las valide. Banco de pruebas sintético y evaluación automática hechos (`backend/bancos-ia`). Motor de agentes hecho (`probar-agente`). Etapa 1, sprint 4: flujo de aprobación de la requisición hecho (líder → RRHH → gerencia en la bandeja; la última aprobación publica). Vacante desde el catálogo de cargos o desde cero hecha. Sprint 6 hecho: banco de talento en lenguaje natural (`/consola/candidatos`) y ficha del candidato con alertas de la hoja de vida y posibles duplicados (reglas, sin IA). Sprint 7, bloque 1 hecho: agente de ranking con puntaje explicable sobre un perfil anónimo y ajuste con motivo. Bloque 2 hecho: tablero Kanban por vacante y terna propuesta en la bandeja. Benchmark de la competencia en `docs/benchmark/`. Sprint 7 completo: retroalimentación que calibra el ranking por empresa y reporte de sesgo por vacante. Sprint 8 hecho por web: preselección conversacional con enlace `/preseleccion/:token`, resumen en la vacante y la ficha, y escalamiento a una persona (bandeja). WhatsApp llega con las integraciones. Sprint 9: informe comparativo de finalistas hecho (pestaña «Finalistas»); las fases 2 y 3 de la suite psicotécnica esperan al psicólogo y el envío automático llega con las integraciones. El 23/10/2026 se agregaron los filtros ATS (pestaña «Filtros ATS»: excluyentes sugeridos por reglas, evaluación con evidencia y descarte sugerido que aprueba una persona en la bandeja) y se adelantó la Etapa 2 según `docs/backlog/Modulos-Vinculacion-Empleados-Desempeno-Clima.md`: bloque V1 de Vinculación hecho (módulo `Vinculacion`, «Aprobar contratación» desde la terna, oferta y contrato del agente redactor, aceptación del candidato en `/vinculacion/:token` con evidencia). Bloque V2 hecho (documentos de ingreso: el candidato sube por su enlace, la IA los lee, las reglas comparan con el titular, RRHH aprueba o rechaza; antecedentes y examen médico con concepto cifrado que nunca pasa por la IA). Bloque V3 hecho (afiliaciones prellenadas, alertas de ARL y alta, «Crear el empleado» con archivo de alta canónico; el detalle del proceso va por pestañas). Sigue V4 (onboarding) y luego Empleados, Desempeño y Clima. Sprint 10 (estabilización y lanzamiento) pendiente. Para después: portal del candidato y del empleado (mis postulaciones, mis pendientes, mis documentos; sección 5 de esa planeación). El sprint 5 (portal con marca propia) queda para después. Cuidar el presupuesto de IA: casos mínimos por defecto.
- Plan aprobado: 6 etapas, 30 sprints de 2 semanas (~14 meses). Punto final: **v1.0 en el sprint 30**.
- Por dónde empieza el producto: **Etapa 1 – Agente Reclutador IA** (tras la Etapa 0 de cimientos).
- No hay clientes piloto: la validación sale del benchmark de competencia, pruebas de usabilidad con profesionales de RRHH externos y analítica de uso con prueba gratuita pública.

## Dónde está cada cosa

| Ruta | Contenido |
| --- | --- |
| `CLAUDE.md` | Este archivo: contexto, reglas y convenciones |
| `docs/Selvana-Plan-de-trabajo.md` | Plan de trabajo completo (fuente de verdad del alcance) |
| `docs/CONTEXTO.md` | Historial de cómo se llegó al plan y qué se descartó |
| `docs/adr/` | Decisiones de arquitectura (ADR) — no contradecirlas sin un ADR nuevo |
| `docs/arquitectura/backend.md` | Guía obligatoria: arquitectura limpia, SOLID, CQRS, CORS, pruebas |
| `docs/backlog/` | Backlog ejecutable por sprint |
| `docs/diseno/marca.md` | Marca aprobada (ruta Dosel): color, tipografía, logo, tono y patrones de IA |
| `wiki/` | Wiki del proyecto en Markdown |
| `backend/` | Todo lo de .NET: `Selvana.slnx`, `src/`, `tests/`, `Directory.*.props`, Dockerfiles de API y worker |
| `frontend/` | Todo lo de React + TypeScript (Vite), con su Dockerfile |
| `docker-compose.yml` | Aplicación completa en contenedores (PostgreSQL, API, worker, web) |
| `.env.example` | Plantilla del `.env` (git ignora el `.env`): contraseñas de compose y `AZURE_DEVOPS_PAT` |

Repositorio: GitHub `https://github.com/RicardoNivia/selvana`, rama `main` (ADR 0012). Carpeta local: `C:\Soluciones\selvana`. Nunca se hace commit directo en `main`: cada funcionalidad va en su propia rama (`feature/<nombre>`) con pull request en GitHub. Azure DevOps se usa solo para Azure Boards (backlog); su repositorio de código es una copia histórica.

## Stack (decidido)

- **Backend:** .NET 10 (LTS), ASP.NET Core Web API, EF Core con proveedor **Npgsql**.
- **Frontend:** React + TypeScript (Vite), TanStack Query.
- **Base de datos:** **PostgreSQL** (gratuito, ya instalado en el equipo de desarrollo) + extensión **pgvector** para búsqueda semántica. JSONB para formularios configurables. Un esquema por módulo.
- **IA:** servicio worker .NET separado con Microsoft.Extensions.AI. Gateway `CadenaModelosChatClient` (`backend/src/Workers/Selvana.AI`): Gemini gratis → Gemini pago → Anthropic (Haiku primero, Opus de respaldo), costo por intento en `ia.usos_modelo` (ADR 0007). Los datos personales nunca van a modelos gratis. Claves en user-secrets del worker (`Ia:Proveedores:<Proveedor>:ApiKey`).
- **Mensajería:** HTTP entre la API y el worker de IA con outbox → HTTP → inbox (ADR 0008). **No usar RabbitMQ ni brokers**: es un monolito modular, no microservicios.
- **Tiempo real:** SignalR (bandeja de aprobaciones, avance de agentes).
- **Observabilidad:** OpenTelemetry.
- **Contenedores:** Docker; CI/CD en Azure Pipelines.

## Arquitectura: monolito modular + worker de IA, arquitectura limpia, SOLID y CQRS

Ver `docs/adr/0001-monolito-modular.md`, `docs/adr/0005-arquitectura-limpia-solid-cqrs.md` y la guía obligatoria **`docs/arquitectura/backend.md`** (léela antes de escribir código de backend). No crear microservicios. Estructura objetivo:

```
backend/
  Selvana.slnx
backend/src/
  Host/Selvana.Api/                    # host: compone módulos, auth OpenID Connect, CORS
  BuildingBlocks/Selvana.SharedKernel/ # Result, Error, ICommand/IQuery y handlers, entidades base
  BuildingBlocks/Selvana.Infrastructure.Common/  # outbox, inbox, mensajería HTTP, decoradores, EF Core base
  BuildingBlocks/Selvana.Web.Common/   # Result → HTTP (ProblemDetails)
  Modules/<Modulo>/                       # Core, Reclutamiento, Vinculacion, Expediente, Desempeno, Formacion, Clima
    Selvana.<Modulo>.Domain/
    Selvana.<Modulo>.Application/      # comandos y consultas (CQRS)
    Selvana.<Modulo>.Infrastructure/
    Selvana.<Modulo>.Api/
    Selvana.<Modulo>.Contracts/        # lo único visible para otros módulos
  Integrations/Selvana.Integrations/   # contrato canónico de nómina + adaptadores por ERP
  Workers/Selvana.AI.Worker/           # agentes, extracción, ranking, conversación
backend/tests/                         # xUnit: SharedKernel, arquitectura (NetArchTest), integración (Testcontainers)
frontend/                              # React + TS (sin pruebas automatizadas)
  src/app/  src/features/<modulo>/  src/shared/  src/theme/
```

**Reglas obligatorias**

1. Arquitectura limpia: `Domain` ← `Application` ← `Infrastructure`/`Api`. El dominio no conoce EF Core ni ASP.NET.
2. **SOLID muy marcado:** un handler por caso de uso, comportamiento transversal por decoradores, interfaces pequeñas por rol, todo por inyección de dependencias (incluidos `TimeProvider` y usuario actual).
3. **CQRS:** comandos (escriben un solo agregado, devuelven `Result`) y consultas (solo leen, proyectan a DTO sin tracking, paginadas) separados. Dispatcher propio en SharedKernel; **no usar MediatR**.
4. Cada módulo tiene su propio esquema de PostgreSQL; ningún módulo consulta tablas de otro.
5. Los módulos se comunican solo por `*.Contracts` o eventos de integración con **outbox**.
6. Pruebas de arquitectura (NetArchTest) en cada build.
7. Las tareas largas de IA van al `AI.Worker` por eventos (outbox → HTTP → inbox), nunca bloquean la API. Puertos locales: API 5000, worker 5090, web 5173 (nunca 3000 ni 3001).
8. **CORS:** política con nombre `SelvanaWeb`, orígenes desde `Cors:AllowedOrigins`, nunca `AllowAnyOrigin()` en producción (detalle en la guía).

## Integración con ERP de nómina

Ver `docs/adr/0004-integracion-erp-agnostica.md`. Selvana **no depende de ningún ERP**.

- Contrato canónico propio: empleado, contrato, cargo, salario, centro de costo, novedades, vacaciones, incapacidades, retiro.
- Un **adaptador** por ERP traduce entre el contrato canónico y el ERP. Agregar un ERP = escribir un adaptador.
- Cuatro niveles de conexión: conector nativo · API REST abierta (OpenAPI) + webhooks · archivo programado CSV/Excel por SFTP · descarga manual asistida.
- Nunca compartir base de datos con el ERP del cliente.

## Reglas de IA (no negociables)

- Toda sugerencia muestra su **explicación** (3–5 razones con evidencia citada) y permite corregir.
- **Excluir variables protegidas** del puntaje: edad, género, estado civil, foto, dirección exacta.
- Registrar cada sugerencia de IA, su explicación y la decisión del usuario (auditoría).
- Niveles de autonomía configurables por empresa: Sugerir · Preparar y aprobar (por defecto) · Autónomo con límites (solo tareas de bajo riesgo).
- Cada agente tiene banco de pruebas y metas de calidad; medir costo de IA por uso y por cliente.
- Datos sensibles (salud, pruebas psicológicas) cifrados, enmascarados y con acceso por rol.
- Cumplir Ley 1581 de 2012 (datos personales): autorización explícita registrada en la postulación.

## Alcance — lo que NO va

- **No hay módulo de entrevistas** (ni agenda, ni guías, ni transcripción de entrevistas). La selección es: vacante → postulación → lector de HV → ranking → preselección conversacional por WhatsApp → pruebas → terna aprobada.
- No hay clientes piloto en el plan.
- Fuera de la v1.0: nómina propia, app nativa instalable, otros países.
- Por ahora Selvana **no maneja presupuestos** (ni presupuesto de planta ni validación de requisiciones contra presupuesto); decisión del 2026-10-22.

## Convenciones de código

- Idioma: nombres de dominio y módulos en español sin tildes (`Vinculacion`, `Desempeno`), como en el plan; código técnico, comentarios cortos y commits en español. Textos de UI en español (Colombia).
- C#: nullable habilitado, `TreatWarningsAsErrors`, `async` de punta a punta, records para DTOs/comandos/consultas/eventos, primary constructors para DI, validación con FluentValidation, `Result`/`Error` en lugar de excepciones de negocio.
- Nombres CQRS: `<Accion><Entidad>Command`, `<Accion><Entidad>Query`, `...Handler`, `...Validator`; una carpeta por caso de uso.
- EF Core: un `DbContext` por módulo, migraciones por módulo (`dotnet ef migrations add <Nombre> --project backend/src/Modules/<Modulo>/Selvana.<Modulo>.Infrastructure`), `snake_case` en PostgreSQL.
- Pruebas: solo en el backend. xUnit + FluentAssertions 7 (la 8 tiene licencia comercial); Testcontainers para PostgreSQL en pruebas de integración (se omiten sin Docker). El frontend no lleva pruebas automatizadas.
- Frontend: marca **Dosel** aprobada (`docs/diseno/marca.md`); usar solo las variables de `frontend/src/theme/tokens.css`. El violeta `--color-ia` es exclusivo de lo que produce la IA. **Antes de crear o cambiar cualquier pantalla, componente o pieza visual, usar la skill `selvana-marca`** (`.claude/skills/selvana-marca/SKILL.md`).
- Frontend: carpetas por funcionalidad (`frontend/src/features/<modulo>`), componentes funcionales, TanStack Query para datos.
- Pull requests y comentarios: descripción y comentarios de los PR (en GitHub), y comentarios en Azure Boards, en **Markdown** (no HTML). Al terminar cada bloque se sube su rama y se abre el PR.
- Secretos: nunca en el repo; usar `dotnet user-secrets` en desarrollo y el `.env` de la raíz para docker-compose.
- Base de datos local: PostgreSQL 17 en `localhost:5432`, base `selvana`. Hoy corre la versión portátil de `C:\Users\ricardo.nivia\pgsql-portable` (el servicio `postgresql-x64-17` quedó sin `postgres.exe`). La cadena con contraseña está en los user-secrets de `Selvana.Api` (`ConnectionStrings:Selvana`). Para `dotnet ef` definir antes `ConnectionStrings__Selvana` en la sesión. pgvector aún no está instalado en ese servidor.

## Definición de terminado de una función con IA

- Funciona en el flujo completo con datos anonimizados.
- Supera la meta de calidad en el banco de pruebas.
- Muestra explicación y permite corregir.
- Registra sugerencia y decisión del usuario.
- Mide costo de IA por uso.
- Pasa revisión de datos personales y sesgo.

## Comandos

```
cd backend && dotnet build Selvana.slnx && dotnet test Selvana.slnx
ASPNETCORE_ENVIRONMENT=Development dotnet run --project backend/src/Host/Selvana.Api --no-launch-profile --urls http://localhost:5000   # API + Swagger en /swagger (sin el ambiente no corren la semilla ni el CORS de desarrollo)
dotnet run --project backend/src/Workers/Selvana.AI.Worker      # worker de IA en http://localhost:5090
cd backend && dotnet ef migrations add <Nombre> --project src/Modules/<Modulo>/Selvana.<Modulo>.Infrastructure --output-dir Persistence/Migrations
cd frontend && npm install && npm run dev
cd backend && dotnet run --project src/Workers/Selvana.AI.Worker -- probar-vacante "<frase>"   # demo del gateway de IA
cd backend && dotnet run --project src/Workers/Selvana.AI.Worker -- probar-agente              # demo del motor de agentes (herramientas, memoria, propuestas)
cd backend && DOTNET_ENVIRONMENT=Development dotnet run --project src/Workers/Selvana.AI.Worker -- evaluar   # banco de pruebas de IA (muestra mínima; --completo para todo)
docker compose up --build        # todo en contenedores (requiere .env)
```

## Próximo paso

Primero la **Etapa D – Diseño, marca y design system** (ver el plan de trabajo): ninguna pantalla se diseña ni se programa sin la marca y el design system. En paralelo, ejecutar `docs/backlog/Sprint-01.md` y luego `docs/backlog/Sprint-02.md` (andamiaje de la solución). Antes de cambiar una decisión de `docs/adr/`, proponer un ADR nuevo.
