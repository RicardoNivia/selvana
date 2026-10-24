# ADR 0003: PostgreSQL con pgvector

- Estado: Aceptado
- Fecha: 2026-10-21

## Contexto
Se prefiere una base gratuita y ya está instalada en el equipo de desarrollo.

## Decisión
- PostgreSQL con EF Core + Npgsql, `snake_case`, un esquema por módulo.
- pgvector para búsqueda semántica de candidatos (sin motor vectorial aparte).
- JSONB para formularios configurables; búsqueda de texto en español.

## Consecuencias
Sin costo de licencia. Integración con ERP de clientes siempre por API, eventos o archivo, nunca compartiendo base de datos.
