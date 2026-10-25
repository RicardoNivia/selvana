# Contexto e historial de decisiones — Selvana

Resumen de la conversación de definición (20 y 21 de octubre de 2026) entre Ricardo Nivia y Claude. Explica por qué el plan es como es.

## 1. Punto de partida

- Necesidad: un módulo completo de RRHH que busque personal, analice hojas de vida, envíe pruebas psicotécnicas, evalúe empleados con evaluaciones de desempeño periódicas y permita al empleado actualizar su hoja de vida corporativa.
- Motivación: el ERP de SINCO no tiene módulo de talento humano; competidores como Buk sí ofrecen suites de RRHH.
- Primer entregable: una guía funcional con 10 submódulos (planeación de planta, reclutamiento ATS, selección inteligente, pruebas psicotécnicas, contratación y onboarding, expediente y autoservicio, desempeño, formación, clima/bienestar/SST, retiro y analítica).

## 2. Ajustes que se pidieron y cómo cambiaron el plan

| # | Pedido | Decisión resultante |
| --- | --- | --- |
| 1 | Comparar con la competencia (Buk, SAP) y orientarlo a un software potenciado con IA | Se adoptó "IA primero": agentes que ejecutan y el usuario solo aprueba. Benchmark con Buk, SAP SuccessFactors y Siesa. |
| 2 | No enfocarlo solo en obras/construcción | Producto **multisector**; paquetes por sector, con énfasis en empresas de servicios temporales (EST) y outsourcing. |
| 3 | Dividir el módulo y hacer un plan super detallado, empezando por algo básico pero con IA de verdad | **6 etapas, 30 sprints**, sprint por sprint en las Etapas 0 y 1. Se empieza por el **Agente Reclutador IA**. |
| 4 | Usar .NET Core y React; decidir monolito o microservicios | **Monolito modular** en .NET + **worker de IA separado**. Ver ADR 0001 y 0002. |
| 5 | Base de datos gratuita, ya instalada en el equipo | **PostgreSQL** + pgvector. Ver ADR 0003. |
| 6 | Quitar el tema de entrevistas | Eliminadas del flujo; el Sprint 9 pasó a "Pruebas psicotécnicas automatizadas" y la entrevista de salida pasó a encuesta de salida. |
| 7 | Compararlo con la competencia en la web | Comparativo con Buk, Factorial, Humand, Heinsohn, Workday y SAP (fuentes consultadas el 21-10-2026). |
| 8 | No hay tres clientes piloto; publicarlo y venderlo según la práctica del mercado | Se eliminaron los pilotos. Validación por benchmark + usabilidad con RRHH externos + analítica de uso. Venta por planes con prueba gratuita de 30 días desde el sprint 10. |
| 9 | Debe tener un punto final | **v1.0 en el sprint 30**, con criterios de terminado explícitos. Lo posterior va a un roadmap de evolución aparte. |
| 10 | Integrable con cualquier ERP que maneje nómina | Contrato canónico + adaptadores; SINCO es solo el primer conector. Ver ADR 0004. |
| 11 | Repositorio en Azure DevOps para wiki y documentación | Wiki en `/wiki` (code wiki) y documentos en `/docs`. *Cambiado el 25-10-2026 (ADR 0012): el repositorio es GitHub y cada funcionalidad entra por PR; Azure DevOps queda para Azure Boards.* |

## 3. Hallazgos clave del benchmark (21-10-2026)

- **Buk:** suite RRHH + nómina, ~2.000 empresas en Colombia; precio por colaborador activo al mes, plan base (Esencial/Pro) + módulos con descuento; anuncia ~7 "trabajadores digitales" de IA, sobre todo en selección. https://www.buk.co/precios · https://www.buk.co/blog/buk-ai-ia-para-rrhh
- **Factorial:** agente "One" por empleado que responde citando el manual y pide confirmación antes de actuar; cribado de candidatos con IA. https://factorial.com/ai
- **Humand:** foco en trabajadores operativos sin escritorio; asistente Sammy AI para vacaciones, turnos, cartas laborales. https://emprendedor.com/humand-serie-a-66-millones-hr-tech-digitalizacion-trabajadores-operativos-latinoamerica/
- **Heinsohn:** suite colombiana con nómina DIAN, IA en selección y clima. https://www.heinsohn.co/us/hr-payroll-software/hr-software/
- **Workday:** Recruiting Agent explicable y auditado en sesgos + agente conversacional para alto volumen. https://blog.workday.com/en-us/workday-leads-talent-acquisition-agentic-era.html
- **SAP SuccessFactors:** agentes Joule + SmartRecruiters. https://www.savictech.com/insights/sap-successfactors-1h-2026-smartrecruiters-agentic-hr/

**Diferenciadores elegidos:** se integra con cualquier ERP de nómina (los demás venden su propia nómina), selección masiva por WhatsApp de punta a punta, paquete para empresas temporales, IA explicable a precio de pyme.

## 4. Planes comerciales

| Plan | Se publica | Incluye |
| --- | --- | --- |
| Selección IA | Sprint 10 | Vacantes con IA, portal de empleo, lector de HV, ranking explicable, preselección por WhatsApp, pruebas |
| Selección + Vinculación | Sprint 14 | + contratos con firma, agente de documentos, alta en nómina por conector, onboarding, paquete temporales |
| Talento Core | Sprint 18 | + expediente vivo, asistente del empleado y del líder |
| Selvana Completo | Sprint 30 | + desempeño con copiloto, formación, clima, retención predictiva |

Precio: suscripción mensual por colaborador activo con descuentos por volumen y por módulos; Selección IA también por vacante o paquete de postulaciones.

## 5. Opciones descartadas

- Microservicios desde el inicio (sobrecosto operativo para un equipo de ~7 personas; dominio aún cambiante).
- SQL Server (costo de licencia; PostgreSQL ya instalado y cubre JSONB y vectores).
- Depender de SINCO como ERP obligatorio.
- Validar con clientes piloto.
- Módulo de entrevistas.
- Enfoque exclusivo en construcción.

## 6. Decisiones pendientes

- Proveedor de modelo de IA y política de dónde se alojan los datos.
- Proveedor de WhatsApp Business y de firma electrónica.
- ~~Proveedor psicométrico licenciado.~~ Decidido el 2026-10-22: suite psicotécnica propia con el psicólogo del equipo (ADR 0011).
- Modelo de precio final y precio de lanzamiento.
- Sectores prioritarios para el lanzamiento.
- Equipo asignado y fecha de inicio del Sprint 1.

## 7. Documentos fuente en Claude

- Plan de trabajo detallado (vigente): https://claude.ai/artifact/RhDDLhZ2zFBEhYjH32FYfu
- Guía funcional inicial (supersedida por el plan; útil solo como catálogo de funciones): https://claude.ai/artifact/12RLSBrUCdbArL2UVWoJgw
