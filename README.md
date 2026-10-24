# Selvana

Módulo de talento humano potenciado con IA, integrable con cualquier ERP de nómina.
Stack: .NET 10 (monolito modular, arquitectura limpia, SOLID, CQRS) + React/TypeScript + PostgreSQL (pgvector).

- `CLAUDE.md` — contexto completo para trabajar con Claude Code
- `docs/Selvana-Plan-de-trabajo.md` — plan de trabajo (6 etapas, 30 sprints, v1.0 en el sprint 30)
- `docs/CONTEXTO.md` — historial de decisiones
- `docs/adr/` — decisiones de arquitectura
- `docs/arquitectura/backend.md` — guía de backend: arquitectura limpia, SOLID, CQRS, CORS
- `docs/backlog/` — backlog por sprint
- `wiki/` — wiki (publicar como code wiki en Azure DevOps, carpeta `/wiki`)
- `backend/` — todo lo de .NET (`Selvana.slnx`, `src/`, `tests/`)
- `frontend/` — todo lo de React + TypeScript
- `docker-compose.yml` — aplicación completa en contenedores

## Arrancar

```bash
cp .env.example .env            # complete POSTGRES_PASSWORD y RABBITMQ_PASSWORD
docker compose up --build       # web: http://localhost:5173 · API: http://localhost:5000/openapi/v1.json
```

Sin Docker:

```bash
cd backend && dotnet test Selvana.slnx
dotnet run --project src/Host/Selvana.Api      # requiere PostgreSQL y la cadena ConnectionStrings:Selvana
cd ../frontend && cp .env.example .env && npm install && npm run dev
```
