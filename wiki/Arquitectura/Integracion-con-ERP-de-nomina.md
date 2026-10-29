# Integración con cualquier ERP de nómina

Selvana es un producto independiente: **no depende de ningún ERP** y se conecta con cualquiera que liquide nómina. SINCO es solo el primer conector.

**Cómo se logra: contrato canónico + adaptadores**

- Selvana define su propio modelo estándar de nómina (“contrato canónico”): empleado, contrato, cargo, salario, centro de costo, novedades, vacaciones, incapacidades, retiro.
- Cada ERP tiene un **adaptador** que traduce entre ese modelo y el del ERP. Agregar un ERP nuevo es escribir un adaptador, sin tocar los módulos.
- Un mapeador visual permite al cliente relacionar sus códigos (tipos de contrato, conceptos, centros de costo) con los de Selvana; la IA sugiere el mapeo inicial.

**Cuatro formas de conectarse, de la más a la menos automatizada**

| Nivel | Mecanismo | Para quién |
| --- | --- | --- |
| 1. Conector nativo | Adaptador mantenido por Selvana (SINCO primero, luego los ERP más usados del mercado) | ERP con API conocida |
| 2. API abierta | API REST documentada (OpenAPI) + webhooks de eventos (“empleado contratado”, “novedad aprobada”, “retiro”) | ERP propios o integradores |
| 3. Archivo programado | Exportación/importación automática CSV/Excel por SFTP con plantilla configurable | ERP sin API |
| 4. Manual asistido | Descarga del archivo de novedades listo para cargar | Cualquier ERP, desde el día uno |

**Qué se sincroniza:** maestros (empresas, sedes, centros de costo, cargos) del ERP hacia Selvana; altas, cambios, novedades y retiros de Selvana hacia el ERP.

**Garantías:** bitácora de cada sincronización, reintentos automáticos, cola de errores para revisar y conciliación mensual empleados Selvana vs. ERP.

---
_Actualizado: 2026-10-21 · Autor: Ricardo Nivia_
