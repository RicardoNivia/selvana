# Plataforma de IA transversal

Todos los agentes comparten la misma plataforma, construida en la Etapa 0 y ampliada en cada etapa; así cada agente nuevo cuesta menos.

| Componente | Qué hace | Etapa en que nace |
| --- | --- | --- |
| Gateway de modelos | Conecta a uno o varios proveedores de IA, controla costos y límites por cliente | 0 |
| Motor de agentes | Orquesta pasos, usa herramientas (ERP, WhatsApp, calendario, firma) y guarda el estado de cada caso | 0 |
| Bandeja de aprobaciones | Muestra lo que la IA preparó, con explicación, para aprobar, corregir o rechazar | 0 |
| Extracción de documentos | Lee HV, cédulas, diplomas, certificados e incapacidades | 1 |
| Búsqueda semántica | Encuentra candidatos y empleados por significado, no solo palabras | 1 |
| Modelos predictivos | Rotación, ausentismo, planta requerida | 5 |
| Evaluación continua | Bancos de prueba por agente: precisión, sesgo, alucinaciones; corre en cada versión | 0 |
| Controles | Variables protegidas excluidas, datos sensibles enmascarados, registro de cada sugerencia y decisión | 0 |

**Catálogo de agentes por etapa:** Reclutador y Preselección (1) · Documentos y Onboarding (2) · Actualización del expediente y consultas del líder (3) · Copiloto de desempeño (4) · Formación, Clima y Retención (5).

## Gateway de modelos (construido)

Decisión: [ADR 0007](/docs/adr/0007-gateway-ia-cadena-de-modelos.md). Código: `backend/src/Workers/Selvana.AI`.

| Orden | Modelo | Nivel | Cuándo se usa |
| --- | --- | --- | --- |
| 1 | Gemini `gemini-3.5-flash-lite` | Gratis | Siempre primero, si la solicitud no lleva datos personales |
| 2 | Gemini `gemini-3.5-flash` | Gratis | Si el anterior falla o agota su cuota |
| 3 | Gemini `gemini-3.1-pro-preview` | Pago | Si los gratis no responden o hay datos personales |
| 4 | Anthropic `claude-haiku-5-5` | Pago | Primero en Anthropic: barato y rápido |
| 5 | Anthropic `claude-opus-5-5` | Pago | Último respaldo, si Haiku falla |

- Un modelo que responde 429 se omite 10 minutos; uno retirado o sin permiso, una hora.
- Cada intento queda en `ia.usos_modelo` con empresa, agente, tokens y costo en USD.
- Los datos personales (hojas de vida, cédulas, salud) nunca van a un modelo gratis (Ley 1581).

Demo local: `cd backend && dotnet run --project src/Workers/Selvana.AI.Worker -- probar-vacante "Necesito 25 auxiliares de bodega para Funza, turno noche"`.

---
_Actualizado: 2026-10-21 · Autor: Ricardo Nivia_

## Motor de agentes

`IMotorAgentes` (`backend/src/Workers/Selvana.AI/Motor`) ejecuta un agente sobre un caso (`vacante:…`, `candidato:…`):

- **Herramientas:** `HerramientaAgente.Lectura` se ejecuta; `HerramientaAgente.Escritura` nunca se ejecuta: sus argumentos quedan como propuesta para la bandeja de aprobaciones.
- **Memoria del caso:** la herramienta integrada `recordar` guarda hechos (máximo 30) en `ia.memorias_caso`; vuelven en las instrucciones de la siguiente ejecución.
- **Registro:** cada paso (modelo, herramienta, recuerdo, propuesta, respuesta, error) queda en `ia.acciones_agentes` con costo y duración, aunque el agente falle.
- Tope de pasos por ejecución (6 por defecto). Si la cadena cambia de modelo a mitad de la conversación, el gateway quita el razonamiento firmado por el modelo anterior.
- Demo: `dotnet run --project src/Workers/Selvana.AI.Worker -- probar-agente`.
