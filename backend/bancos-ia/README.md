# Bancos de prueba de los agentes de IA

Casos con la respuesta esperada para medir la calidad de cada agente antes de publicarlo (definición de terminado de una función con IA).

| Banco | Agente | Casos | Metas |
| --- | --- | --- | --- |
| `lector-hojas-de-vida.json` | Lector de hojas de vida | 24 hojas de vida sintéticas en 4 formatos (secciones, narrativa, tabla, desordenada), 12 de mujeres y 12 de hombres | ≥ 95 % de campos clave correctos; 0 variables protegidas extraídas |
| `redactor-vacantes.json` | Redactor de vacantes | 20 frases de líderes en 9 ciudades | ≥ 95 % ciudad y cupos; 100 % salario legal; 100 % sin requisitos discriminatorios; ≥ 95 % con explicación (3 a 5 razones con evidencia) |

- **Datos sintéticos:** ninguna persona existe. Los regenera `python backend/bancos-ia/generar_bancos.py`. Los datos reales anonimizados (con autorización) no van al repositorio.
- **Trampas:** cada hoja de vida trae fecha de nacimiento, estado civil y dirección exacta; el lector nunca debe extraerlos.
- **Sesgo:** se compara la precisión entre grupos; `brechaEntreGrupos` debe ser cercana a 0.

## Cómo correrlo

```
cd backend
DOTNET_ENVIRONMENT=Development dotnet run --project src/Workers/Selvana.AI.Worker -- evaluar                # muestra mínima: 3 casos por agente (≈ USD 0,05)
DOTNET_ENVIRONMENT=Development dotnet run --project src/Workers/Selvana.AI.Worker -- evaluar lector --max 5
DOTNET_ENVIRONMENT=Development dotnet run --project src/Workers/Selvana.AI.Worker -- evaluar todo --completo  # banco completo (≈ USD 0,45)
```

Usa los modelos reales (consume presupuesto de IA): por defecto corre una muestra mínima y el banco completo solo antes de publicar un cambio de un agente. El costo queda aparte, con la empresa de evaluación `e7a1e7a1-…`. Cada corrida se guarda en `ia.evaluaciones_agentes` y el último informe en `docs/ia/evaluaciones/<agente>.md`. El comando termina con código 2 si no se cumplen las metas.

## Primera corrida completa (2026-10-22)

- Lector: 100 % de campos clave, 0 variables protegidas, brecha entre grupos 0 %, USD 0,34 (claude-opus-5-5: los datos personales no van a modelos gratis y Gemini de pago no tiene facturación activa).
- Redactor: 100 % en las cuatro metas, USD 0,08 (gemini-3.5-flash-lite en la mayoría).

## Haiku primero en Anthropic (2026-10-22)

- Lector, muestra de 3 casos con `claude-haiku-5-5`: 100 % de campos clave, 0 variables protegidas, USD 0,0014 (unas 30 veces menos que con Opus). Opus queda solo de respaldo si Haiku falla.
