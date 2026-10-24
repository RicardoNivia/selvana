# ADR 0005: Arquitectura limpia, SOLID estricto y CQRS en el backend

- Estado: Aceptado
- Fecha: 2026-10-21

## Contexto
El backend debe ser mantenible durante 30 sprints y más, con varios desarrolladores y agentes de IA generando código. Se exige arquitectura limpia con principios SOLID muy marcados y CQRS.

## Decisión
- Cada módulo con cuatro capas: `Domain` ← `Application` ← `Infrastructure` y `Api` (la dependencia siempre apunta hacia el dominio).
- **CQRS** en `Application`: comandos (escriben, devuelven `Result`) y consultas (solo leen, devuelven DTOs) separados, cada uno con su handler.
- Dispatcher propio (`ICommandHandler<TCommand>`, `IQueryHandler<TQuery,TResult>`) con decoradores para validación, logging, transacción y auditoría (registro con Scrutor). **No usar MediatR** (licencia comercial desde 2025).
- Escritura: agregados de dominio + EF Core con unidad de trabajo por módulo. Lectura: proyecciones `AsNoTracking` o Dapper directo a DTOs, en la misma base (sin base de lectura separada por ahora).
- SOLID verificado con pruebas de arquitectura (NetArchTest) y revisión de código. Detalle en `docs/arquitectura/backend.md`.

## Consecuencias
Más archivos por caso de uso, a cambio de handlers pequeños, testeables y fáciles de generar con IA. Las consultas pueden optimizarse sin tocar el modelo de dominio.
