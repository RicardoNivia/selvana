# ADR 0007: Gateway de IA con cadena de modelos (Gemini gratis → Gemini pago → Anthropic)

- Estado: Aceptado
- Fecha: 2026-10-21

## Contexto

Cada agente necesita un modelo de lenguaje. El costo de IA por cliente es una métrica del producto y los primeros meses no hay ingresos, así que conviene usar primero lo gratuito. Además, ningún proveedor tiene disponibilidad perfecta: hay cuotas (429), modelos que se retiran (404) y caídas (5xx).

## Decisión

- Los agentes dependen solo de `IChatClient` (Microsoft.Extensions.AI). El gateway es `CadenaModelosChatClient` en `backend/src/Workers/Selvana.AI/Gateway`.
- La cadena prueba los modelos en el orden de `Ia:Cadena` (configuración) y devuelve la primera respuesta:
  1. Gemini gratis (`gemini-3.5-flash-lite`, `gemini-3.5-flash`)
  2. Gemini de pago (`gemini-3.1-pro-preview`)
  3. Anthropic: primero `claude-haiku-5-5` (USD 0,10 / 0,50 por millón) y, solo si falla, `claude-opus-5-5` como último respaldo
- Un modelo que responde 429 se omite 10 minutos; uno que responde 401/403/404, una hora. Los errores transitorios pasan al siguiente modelo sin castigo.
- Cada intento, exitoso o fallido, se guarda en `ia.usos_modelo` con empresa, agente, modelo, nivel, tokens, costo en USD y duración.
- **Datos personales nunca van a un modelo gratis.** En el plan gratuito de Gemini, Google puede usar el contenido para mejorar sus productos, lo que no es compatible con la Ley 1581 para hojas de vida, cédulas o datos de salud. Cada solicitud declara `ContextoIa.ContieneDatosPersonales`; si no lo declara, se asume que sí.
- Agregar un proveedor = una clase nueva que implementa `IProveedorModelos`. La cadena no cambia.
- Las claves van en `user-secrets` (desarrollo) o variables de entorno (`Ia__Proveedores__Gemini__ApiKey`, `Ia__Proveedores__Anthropic__ApiKey`), nunca en el repositorio.

## Consecuencias

- La calidad puede variar según el modelo que responda. Por eso toda salida pasa por validaciones en código (esquema, variables protegidas, salario mínimo) y la explicación muestra qué modelo la produjo.
- El respaldo en streaming solo es seguro antes de emitir texto; por ahora la cadena resuelve la respuesta completa y la reparte en partes.
- Los precios por millón de tokens están en la configuración y hay que revisarlos cuando cambien las tarifas de los proveedores.
- Pendiente: límite de costo mensual por empresa y alertas (con el módulo de facturación).
