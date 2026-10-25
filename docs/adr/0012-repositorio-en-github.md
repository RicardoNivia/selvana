# ADR 0012: El código vive en GitHub y cada funcionalidad entra por pull request

- Estado: Aceptado (reemplaza la parte de «repositorio en Azure DevOps» del ADR 0006)
- Fecha: 2026-10-25

## Contexto

El ADR 0006 y el CLAUDE.md ubicaban el código, la documentación y la wiki en el repositorio de Azure DevOps (`ricardonivia0145/selvana`). Hoy el repositorio con el que se trabaja es el de GitHub: es el `origin` de la carpeta local y de los worktrees. Las dos ramas `main` tienen el mismo contenido pero historias distintas, sin ningún commit en común: Azure DevOps conserva la historia por bloques hasta Vinculación V3 y GitHub, una versión importada («Merge con repo remoto»). Mantener los dos como repositorio vivo crea PR sin base común y confunde a dónde va cada cambio.

## Decisión

- El repositorio del código es **GitHub `https://github.com/RicardoNivia/selvana`**, rama `main`. Ahí viven `/backend`, `/frontend`, `/docs` y `/wiki`.
- **Nunca se hace commit directo en `main`.** Cada funcionalidad o cambio va en su propia rama (`feature/<nombre>`, `docs/<nombre>`, `fix/<nombre>`) y entra por un pull request en GitHub, con descripción en Markdown.
- **Azure DevOps sigue para Azure Boards** (épicas, features e historias del backlog; script `scripts/crear-backlog-azure.ps1` y agente `azure-backlog`). Su repositorio de código queda como copia histórica: no se le hacen push ni PR.
- La wiki se mantiene como Markdown en `/wiki` del repositorio de GitHub.

## Consecuencias

- CLAUDE.md, README y CONTEXTO apuntan a GitHub.
- Las ramas que nacieron de la historia de Azure DevOps se pasan a la de GitHub con `cherry-pick` (el contenido es el mismo).
- Pendiente: `azure-pipelines.yml` debe conectarse al repositorio de GitHub (Azure Pipelines lo permite) o reemplazarse por GitHub Actions; se decide aparte.
