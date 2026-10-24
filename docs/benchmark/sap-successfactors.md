# Benchmark · SAP SuccessFactors (Recruiting, Joule y SmartRecruiters)

Consultado el 22 de octubre de 2026 en fuentes oficiales de SAP (producto, SAP Learning, sala de prensa, notas de soporte), prensa e integradores, y reseñas (G2, Gartner). Dos páginas de SAP devolvieron 403; la fecha de la integración nativa de marzo de 2026 tiene una sola fuente.

## Qué es

Suite HCM para **grandes empresas globales**, normalmente clientes SAP: Employee Central (localizado para más de 100 países), Central Payroll, Recruiting, Performance & Goals, Compensation, Time Tracking, Learning. [Employee Central](https://www.sap.com/hk/products/hcm/employee-central-hris.html)

## Reclutamiento

| Función | SAP SuccessFactors | Selvana |
| --- | --- | --- |
| Requisición y aprobación | ✔ La requisición se aprueba antes de publicar; aprobadores definidos en la implementación ([SAP Learning](https://learning.sap.com/courses/navigating-the-employee-lifecycle-through-sap-successfactors/overview-of-sap-successfactors-recruiting)) | ✔ Líder → RRHH → gerencia, configurado en el producto |
| Portal | ✔ Career Site Builder ([alcance](https://assets.cdn.sap.com/agreements/product-policy/css/service-specifications/feature-scope-description-for-sap-successfactors-recruiting-english-v5-2025.pdf)) | ✔ Portal público |
| Lectura y emparejamiento | ✔ Extrae habilidades de la hoja de vida y muestra el ajuste con el cargo; **Winston Match** de SmartRecruiters califica de 1 a 4 estrellas ([curso](https://learning.sap.com/courses/smartrecruiters-for-sap-successfactors-academy/introducing-winston-match)) | ✔ Puntaje 0-100 con razones y evidencia |
| Explicación por candidato y auditoría de sesgo publicada | No encontrado (solo con complementos de socios como MeVitae) | ✔ Por defecto |
| Conversación | Winston Screen hace preguntas y ordena por respuestas; Winston Companion para gerentes en Slack, Teams, SMS y WhatsApp ([SmartRecruiters](https://www.smartrecruiters.com/news/smartrecruiters-unveils-winston-the-ai-that-keeps-hiring-human)); WhatsApp con candidatos no confirmado | ◐ Planeado |
| Agentes Joule (1S-2026) | Carrera y talento, servicio de RRHH, nómina y people intelligence; **sin agente de selección** ([SAP News](https://news.sap.com/?p=237189)) | Agentes de redacción, lectura y ranking ya funcionando |
| Pruebas | Por integraciones de terceros | Suite propia (fase 1) |

**Cambio de plataforma:** SAP compró SmartRecruiters (cerró en septiembre de 2025) y lo va a usar para **reemplazar** el módulo Recruiting de SuccessFactors, con una migración de 3 a 5 años ([CIO](https://www.cio.com/article/4068172/sap-sets-timeline-to-replace-successfactors-recruiting-module-with-smartrecruiters.html)).

## Plataforma

- **Integración:** OData API, Integration Center y SAP Cloud Integration para nómina de terceros. SmartRecruiters también se vende con otros HCM.
- **Precio:** Employee Central USD 6,30 por usuario al mes en contratos de 1 a 5 años ([SAP](https://www.sap.com/products/hcm/employee-central-hris/pricing.html)); Recruiting «a solicitud» (estimación de terceros: USD 2–3 por empleado al mes, requiere Employee Central, mínimo ~USD 5.000 al año).
- **Colombia:** localización colombiana en Employee Central y validación de cédula; nota de soporte sobre nómina electrónica DIAN para SAP ERP HCM (no confirmada para SuccessFactors). Ley 1581 y clientes nombrados: no encontrado.
- **Reseñas:** G2 suite HCM 3,9/5 (766); Gartner SmartRecruiters para SAP SF 4,4 (104). Se elogia la automatización y la agenda autoservicio; se critica la integración entre módulos.

## Lectura para Selvana

SAP es la referencia de **catálogo de habilidades transversal** y de **localización multipaís**, pero su selección está en plena transición a SmartRecruiters y sin explicación por candidato a la vista. Para empresas colombianas medianas el costo y la implementación de SAP son una barrera: Selvana compite con selección explicable lista para usar, conectada al ERP que ya tienen.
