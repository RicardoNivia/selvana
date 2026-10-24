# Sprint 2 — Núcleo de datos, seguridad y andamiaje (Etapa 0)

Objetivo: solución .NET + React compilando, con arquitectura limpia, CQRS y pruebas de arquitectura desde el primer commit. Seguir `docs/arquitectura/backend.md`.

## Andamiaje
- [x] `backend/Selvana.slnx` con la estructura de `CLAUDE.md` (Host, BuildingBlocks, Modules/Core, Integrations, Workers, tests)
- [x] `Directory.Build.props`: .NET 10, nullable, `TreatWarningsAsErrors`, analizadores
- [x] `Directory.Packages.props` (gestión central de paquetes)
- [x] SharedKernel: `Result`, `Error`, `Entity`, `AggregateRoot`, `ValueObject`, `IDomainEvent`, `ICommand`/`IQuery` y sus handlers
- [x] Decoradores: Logging, Validation, Transaction; registro con Scrutor
- [x] Outbox base (tabla `outbox_messages`) y publicador
- [x] Host `Selvana.Api`: Minimal APIs, ProblemDetails, OpenAPI, versionado, CORS `SelvanaWeb`, health checks
- [x] `backend/tests/Selvana.ArchitectureTests` con reglas de dependencias y nombres
- [x] `frontend/` con Vite + React + TS + TanStack Query
- [x] `docker-compose.yml` (PostgreSQL con pgvector; sin RabbitMQ desde el ADR 0008) para quien no use el PostgreSQL local
- [x] Pipeline de Azure Pipelines: build, pruebas, cobertura

## Módulo Core
- [ ] Dominio: Persona, Empresa, Sede, Area, CentroCosto, Cargo, Usuario, Rol
- [ ] Multiempresa y multicliente (empresa usuaria para EST)
- [ ] Comandos/consultas CRUD básicos con validadores
- [ ] Autorización de tratamiento de datos (Ley 1581) y bitácora de auditoría
- [ ] Migraciones EF Core en esquema `core`

## Integraciones
- [ ] `IPayrollConnector` + contrato canónico de nómina en `Selvana.Integrations`
- [ ] Esqueleto del adaptador SINCO + pruebas de contrato

Criterio de cierre: `dotnet build` y `dotnet test` en verde en el pipeline; pruebas de arquitectura pasan; la API responde con OpenAPI; el frontend consume una consulta de Core.
