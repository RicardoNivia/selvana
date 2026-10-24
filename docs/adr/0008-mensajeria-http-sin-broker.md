# ADR 0008: Mensajería HTTP entre la API y el AI.Worker, sin broker

- Estado: Aceptado (reemplaza la parte de mensajería de ADR 0001)
- Fecha: 2026-10-22

## Contexto

ADR 0001 dejó la comunicación entre la API y el `AI.Worker` por un broker (RabbitMQ o Azure Service Bus). Selvana es un monolito modular, no una plataforma de microservicios: los módulos se hablan en proceso y solo el worker de IA corre aparte. Un broker agrega un servicio más que instalar, operar y monitorear, y en el equipo de desarrollo no hay Docker ni RabbitMQ. El dueño del producto decidió no usar RabbitMQ y comunicar la API y el worker por HTTP.

La regla de oro se mantiene: una tarea larga de IA nunca bloquea la API, y un evento no se pierde si un proceso se cae.

## Decisión

- La API y el worker se comunican por **HTTP POST** con el patrón **outbox → HTTP → inbox**:
  1. El comando guarda el agregado y el evento en el **outbox** de su esquema, en la misma transacción.
  2. `OutboxProcessor` envía cada evento con `HttpEventBus` al destino cuyo prefijo coincide con el tipo del evento (`Mensajeria:Destinos`). Si falla, reintenta en la siguiente vuelta.
  3. El destino recibe en `MapEventosEntrantes<TContext>` (`/eventos` en el worker, `/internal/eventos` en la API), guarda en su **inbox** (el Id del evento es la clave: los duplicados se ignoran) y responde **202** de inmediato.
  4. `InboxProcessor` despacha en segundo plano a los `IIntegrationEventConsumer<T>`. Hasta 5 intentos por evento.
  5. Las respuestas del worker vuelven a la API por el mismo camino.
- Los endpoints internos se autentican con una clave compartida en el encabezado `X-Selvana-Clave` (`Mensajeria:ClaveInterna`), comparada en tiempo constante. No pasan por el usuario ni por CORS.
- Los eventos viven en `*.Contracts`: `Selvana.Core.Contracts` (Core → IA) y `Selvana.AI.Contracts` (IA → Core).
- Se elimina RabbitMQ del código, de los paquetes y de `docker-compose.yml`.

## Consecuencias

- Una pieza menos que operar; en local basta con PostgreSQL, la API y el worker.
- Entrega al menos una vez con idempotencia por Id de evento en el inbox.
- Cada destino se configura por URL. Si un día hay varios consumidores para el mismo evento, se agregan más destinos con el mismo prefijo; si el volumen lo exige, se puede volver a un broker implementando otro `IEventBus` sin tocar los módulos.
- Puertos locales: API 5000, worker 5090, web 5173.
