# ADR 0004: Integración agnóstica con cualquier ERP de nómina

- Estado: Aceptado
- Fecha: 2026-10-21

## Decisión
- Selvana es independiente: no liquida nómina ni depende de un ERP.
- Contrato canónico propio (empleado, contrato, cargo, salario, centro de costo, novedades, vacaciones, incapacidades, retiro).
- Patrón puerto/adaptador: interfaz `IPayrollConnector` en `Selvana.Integrations`; un adaptador por ERP (SINCO primero).
- Niveles: conector nativo, API REST abierta (OpenAPI) + webhooks, archivo programado CSV/Excel por SFTP, descarga manual.
- Bitácora de sincronización, reintentos, cola de errores y conciliación mensual.

## Consecuencias
Agregar un ERP no toca los módulos de negocio. v1.0 exige 3 conectores nativos (SINCO + 2).
