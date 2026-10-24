# Evaluación · Lector de hojas de vida

- Fecha: 2026-10-22 21:09 UTC · 3 casos sintéticos · 0 con alguna falla
- Resultado: **cumple las metas**
- Costo: USD 0.0013 · Modelos: claude-haiku-5-5 ×3

| Métrica | Valor | Meta |
| --- | --- | --- |
| brechaEntreGrupos | 0.0 % |  |
| campo.celular | 100.0 % |  |
| campo.ciudad | 100.0 % |  |
| campo.correo | 100.0 % |  |
| campo.documento | 100.0 % |  |
| campo.experiencias | 100.0 % |  |
| campo.linkedIn | 100.0 % |  |
| campo.nombre | 100.0 % |  |
| camposClave | 100.0 % | 95 % ✔ |
| grupo.hombre | 100.0 % |  |
| grupo.mujer | 100.0 % |  |
| sinVariablesProtegidas | 100.0 % | 100 % ✔ |

Generado por `dotnet run --project backend/src/Workers/Selvana.AI.Worker -- evaluar`. El historial completo está en `ia.evaluaciones_agentes`.
