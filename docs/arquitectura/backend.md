# Guía de arquitectura del backend — Arquitectura limpia, SOLID y CQRS

Esta guía es obligatoria para todo código del backend (personas y Claude Code). Decisión base: `docs/adr/0005-arquitectura-limpia-solid-cqrs.md`.

## 1. Capas por módulo

Cada módulo (`Core`, `Reclutamiento`, `Vinculacion`, `Expediente`, …) es un conjunto de proyectos:

```
backend/src/Modules/Reclutamiento/
  Selvana.Reclutamiento.Domain/          # entidades, agregados, value objects, eventos de dominio, interfaces de repositorio
  Selvana.Reclutamiento.Application/     # comandos, consultas, handlers, validadores, DTOs, puertos (interfaces)
  Selvana.Reclutamiento.Infrastructure/  # EF Core (DbContext del esquema), repositorios, adaptadores externos
  Selvana.Reclutamiento.Api/             # endpoints (Minimal API), mapeo HTTP ⇄ comandos/consultas
  Selvana.Reclutamiento.Contracts/       # lo único visible para otros módulos: eventos de integración y DTOs públicos
```

**Regla de dependencias** (verificada con NetArchTest en `backend/tests/Selvana.ArchitectureTests`):

| Proyecto | Puede referenciar | No puede referenciar |
| --- | --- | --- |
| Domain | `Selvana.SharedKernel` | Application, Infrastructure, Api, EF Core, ASP.NET, cualquier otro módulo |
| Application | Domain, SharedKernel, `Contracts` de otros módulos | Infrastructure, Api, EF Core, ASP.NET |
| Infrastructure | Application, Domain | Api, internos de otros módulos |
| Api | Application, Contracts | Domain directamente, Infrastructure (salvo el registro DI del módulo) |
| Contracts | SharedKernel | Todo lo demás |

`Selvana.Api` (host) solo compone: registra cada módulo con `services.AddReclutamientoModule(configuration)` y mapea sus endpoints.

## 2. Principios SOLID — cómo se aplican aquí

**S — Responsabilidad única**
- Un handler = un caso de uso (`CrearVacanteHandler`, `AprobarTernaHandler`). Nada de "servicios" con 20 métodos.
- Un endpoint solo traduce HTTP a comando/consulta y `Result` a respuesta HTTP; no tiene lógica.
- Validación en su propia clase (`CrearVacanteValidator` con FluentValidation), no dentro del handler.

**O — Abierto/cerrado**
- Comportamiento transversal (validación, logging, transacción, auditoría de IA) se agrega con **decoradores** sobre los handlers, sin modificarlos.
- Nuevo ERP = nuevo adaptador que implementa `IPayrollConnector`; nuevo proveedor de IA = nueva implementación de `IChatClient`/`IAiGateway`. Nunca `switch` por tipo de proveedor.
- Reglas variables por empresa o sector vía estrategias (`IRankingPolicy`, `IAutonomyPolicy`).

**L — Sustitución de Liskov**
- Toda implementación de un puerto cumple su contrato completo (mismos errores esperados, mismo significado de `Result`). Pruebas de contrato compartidas para cada puerto (p. ej. `PayrollConnectorContractTests` que corren contra cada adaptador).
- Prohibido lanzar `NotImplementedException` en implementaciones productivas.

**I — Segregación de interfaces**
- Interfaces pequeñas y por rol: `ICandidatoReader` / `ICandidatoWriter` en lugar de `ICandidatoRepository` gigante; `IDocumentTextExtractor`, `ICvParser`, `ICandidateRanker` por separado.
- Los módulos exponen en `Contracts` solo lo que otro módulo necesita.

**D — Inversión de dependencias**
- `Domain` y `Application` dependen de abstracciones definidas en ellos mismos (puertos); `Infrastructure` las implementa.
- Nada de `new` de servicios ni `static` con estado; todo por inyección de dependencias en el constructor (primary constructors de C#).
- El reloj, el usuario actual y los IDs se inyectan (`TimeProvider`, `ICurrentUser`, `IIdGenerator`) para que todo sea testeable.

## 3. CQRS

Separación estricta entre escribir y leer dentro de `Application`:

```
Selvana.Reclutamiento.Application/
  Vacantes/
    Crear/
      CrearVacanteCommand.cs
      CrearVacanteHandler.cs
      CrearVacanteValidator.cs
    ObtenerPorId/
      ObtenerVacanteQuery.cs
      ObtenerVacanteHandler.cs
      VacanteDto.cs
```

**Abstracciones** (en `Selvana.SharedKernel`; sin MediatR, que tiene licencia comercial desde 2025):

```csharp
public interface ICommand;                         // sin valor de retorno
public interface ICommand<TResponse>;              // con valor (p. ej. Id creado)
public interface IQuery<TResponse>;

public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task<Result> Handle(TCommand command, CancellationToken ct);
}
public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct);
}
public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken ct);
}
```

Registro automático con Scrutor (`services.Scan(...)`) y decoradores en este orden: `Logging` → `Validation` → `Transaction` (solo comandos) → `AiAudit` (cuando el comando aplica una sugerencia de IA).

**Reglas de comandos (escritura)**
- Modifican **un solo agregado** por transacción; efectos en otros agregados o módulos van por eventos.
- Pasan por el dominio: el handler carga el agregado, invoca un método de negocio (`vacante.Publicar()`), guarda con la unidad de trabajo del módulo.
- Devuelven `Result` o `Result<Id>`; nunca entidades.
- Errores de negocio como `Error` tipados (`VacanteErrors.YaPublicada`), no excepciones.

**Reglas de consultas (lectura)**
- No modifican estado y no usan agregados.
- Proyectan directo a DTO: EF Core `AsNoTracking().Select(...)` o Dapper con SQL explícito.
- Pueden leer vistas o tablas desnormalizadas del propio esquema; nunca de otro módulo.
- Paginación obligatoria en listados.

**Eventos**
- Eventos de dominio (dentro del módulo) se despachan al guardar, antes de confirmar la transacción.
- Eventos de integración (entre módulos o hacia el `AI.Worker`) se publican con **outbox** (tabla `outbox_messages` por esquema) para garantizar entrega.

## 4. Dominio

- Entidades con constructor privado y fábrica estática (`Vacante.Crear(...)` devuelve `Result<Vacante>`); setters privados.
- Value objects inmutables (`record`) para conceptos con reglas: `Documento`, `Correo`, `RangoSalarial`, `PuntajeAfinidad`.
- Invariantes dentro del agregado, nunca en el handler.
- Nada de atributos de EF Core en el dominio; el mapeo va en `Infrastructure` con `IEntityTypeConfiguration<T>`.

## 5. API

- Minimal APIs agrupadas por módulo (`app.MapGroup("/api/reclutamiento")`), versionadas (`/api/v1/...`).
- `Result` → HTTP: éxito 200/201, validación 400 con `ProblemDetails`, no encontrado 404, conflicto de negocio 409.
- OpenAPI generado y publicado (es parte de la API abierta para ERP).
- Autenticación OpenID Connect; autorización por políticas y por empresa (multi-tenant).

## 6. CORS

El frontend React y los portales (empleo, empresa usuaria) corren en otros orígenes, por eso la API define CORS explícito:

- Una política con nombre (`"SelvanaWeb"`) con orígenes **desde configuración** (`Cors:AllowedOrigins` en `appsettings.{Environment}.json`); nunca `AllowAnyOrigin()` en producción.
- Desarrollo: `http://localhost:5173` (Vite).
- Métodos permitidos: `GET, POST, PUT, PATCH, DELETE`; encabezados: `Authorization, Content-Type, X-Tenant-Id`; `AllowCredentials()` solo si se usan cookies (SignalR).
- El portal de empleo público y los webhooks de ERP no dependen de CORS: los webhooks se autentican por firma HMAC.

```csharp
builder.Services.AddCors(o => o.AddPolicy("SelvanaWeb", p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()!)
    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
    .WithHeaders("Authorization", "Content-Type", "X-Tenant-Id")
    .AllowCredentials()));
// ...
app.UseCors("SelvanaWeb");
```

## 7. Pruebas

| Tipo | Proyecto | Qué prueba |
| --- | --- | --- |
| Unitarias de dominio | `backend/tests/<Modulo>.Domain.Tests` | Invariantes y métodos de agregados, sin mocks |
| Unitarias de aplicación | `backend/tests/<Modulo>.Application.Tests` | Handlers con dobles de los puertos |
| Integración | `backend/tests/<Modulo>.IntegrationTests` | Endpoints + PostgreSQL real con Testcontainers |
| Arquitectura | `backend/tests/Selvana.ArchitectureTests` | Reglas de dependencias, nombres (`*Handler`, `*Command`, `*Query`), que el dominio no use EF Core |
| Contrato | `backend/tests/Selvana.Integrations.ContractTests` | Cada adaptador de ERP cumple `IPayrollConnector` |
| Evaluación de IA | `backend/tests/Selvana.AI.Evals` | Bancos de prueba por agente: precisión, sesgo, costo |

Un PR no se aprueba si baja la cobertura de dominio y aplicación por debajo del 80 % o si falla una prueba de arquitectura.

## 8. Lista de revisión para cada PR

- [ ] Cada caso de uso es un comando o una consulta con su handler y validador
- [ ] La lógica de negocio vive en el dominio, no en handlers ni endpoints
- [ ] No hay referencias entre módulos salvo `Contracts`
- [ ] Interfaces pequeñas; nada de `new` de dependencias
- [ ] Consultas sin tracking y paginadas
- [ ] Eventos de integración por outbox
- [ ] Pruebas unitarias e integración incluidas; pruebas de arquitectura en verde
- [ ] Sugerencias de IA auditadas y explicables
