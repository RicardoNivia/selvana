# ADR 0001: Monolito modular con worker de IA separado

- Estado: Aceptado
- Fecha: 2026-10-21

## Contexto
Equipo de ~7 personas, 30 sprints, dominio todavía cambiante (los límites entre Reclutamiento y Vinculación se ajustarán con los primeros clientes). Las tareas de IA son largas y escalan distinto al resto.

## Decisión
- Un solo despliegue `Selvana.Api` con módulos independientes (Core, Reclutamiento, Vinculacion, Expediente, Desempeno, Formacion, Clima, Integrations).
- Un despliegue separado `Selvana.AI.Worker` comunicado por mensajería; desde el ADR 0008, por HTTP con outbox e inbox, sin broker.
- Reglas: esquema de BD por módulo, sin consultas entre esquemas, comunicación solo por `*.Contracts` o eventos, pruebas de arquitectura con NetArchTest.

## Consecuencias
- Entrega más rápida y pruebas de punta a punta simples.
- Un módulo se puede extraer a microservicio cuando tenga carga, ritmo de despliegue o equipo propios.
- Requiere disciplina: las pruebas de arquitectura deben correr en cada build.
