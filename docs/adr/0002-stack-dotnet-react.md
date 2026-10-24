# ADR 0002: .NET 10 + React con TypeScript

- Estado: Aceptado
- Fecha: 2026-10-21

## Decisión
- Backend: .NET 10 (LTS), ASP.NET Core Web API, EF Core.
- IA: .NET Worker Service con Microsoft.Extensions.AI (o Semantic Kernel), proveedor de modelo intercambiable.
- Frontend: React + TypeScript (Vite), TanStack Query.
- Tiempo real: SignalR. Observabilidad: OpenTelemetry. Contenedores: Docker. CI/CD: Azure Pipelines.

## Consecuencias
Stack conocido por el equipo, LTS hasta 2028, ecosistema maduro para arquitectura limpia y CQRS.
