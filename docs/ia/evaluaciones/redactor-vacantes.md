# Evaluación · Redactor de vacantes

- Fecha: 2026-10-22 16:55 UTC · 20 casos sintéticos · 0 con alguna falla
- Resultado: **cumple las metas**
- Costo: USD 0.0819 · Modelos: gemini-3.5-flash-lite ×16, claude-opus-5-5 ×2, gemini-3.5-flash ×2

| Métrica | Valor | Meta |
| --- | --- | --- |
| ciudadYCupos | 100.0 % | 95 % ✔ |
| explicacion | 100.0 % | 95 % ✔ |
| salarioLegal | 100.0 % | 100 % ✔ |
| sinDiscriminacion | 100.0 % | 100 % ✔ |

Generado por `dotnet run --project backend/src/Workers/Selvana.AI.Worker -- evaluar`. El historial completo está en `ia.evaluaciones_agentes`.
