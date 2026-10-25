# Etapa 2 – Vinculación y onboarding autónomo (sprints 11 a 14, 8 semanas)

Objetivo: que el candidato aprobado quede contratado, afiliado y creado en nómina sin que RRHH digite nada.

**Sprint 11 – Oferta y contrato**

- [x] Carta oferta y contrato generados por IA desde plantillas y tipo de contrato (adelantado: aceptación electrónica con evidencia; la firma certificada llega con las integraciones)
- [ ] Firma electrónica del candidato y del representante
- [ ] Revisión automática de cláusulas contra la plantilla aprobada por jurídico

**Sprint 12 – Agente de documentos**

- [ ] El agente pide por WhatsApp los documentos de ingreso y hace seguimiento
- [x] Lectura y validación de cédula, certificados, diplomas, cuenta bancaria y certificaciones (adelantado; el pedido por WhatsApp llega con las integraciones)
- [x] Verificación de antecedentes (Procuraduría, Contraloría, Policía) con soporte guardado (la consulta automática llega con las integraciones)
- [x] Orden y resultado de examen médico de ingreso (resultado con concepto cifrado; la orden a la IPS llega con las integraciones)

**Sprint 13 – Alta en nómina y paquete temporales**

- [ ] Creación automática del empleado en la nómina del ERP conectado y en su centro de costo (por conector, API o archivo); API abierta, webhooks y exportación por archivo publicadas para cualquier ERP
- [x] Checklist de afiliaciones (EPS, AFP, ARL, caja) con alertas (adelantado: prellenado con los certificados leídos; «Crear el empleado» genera el archivo de alta canónico)
- [ ] Empresas temporales: asignación a empresa usuaria, control de misión y su duración, portal del cliente para pedir y aprobar personal

**Sprint 14 – Onboarding guiado**

- [x] Plan de inducción generado por cargo: tareas, responsables y fechas — *bloque V4: al crear el empleado nace el plan con las tareas legales por reglas (inducción en SST, reglamento interno, EPP desde riesgo III, inducción en el sitio de la empresa usuaria y evaluación del periodo de prueba) y el planificador de IA (`PlanificadorInduccionAgente`, sin datos de la persona) propone las del cargo para los primeros 90 días. RRHH ajusta y aprueba en la bandeja; seguimiento tarea por tarea con vencidas, «no aplica» con motivo (nunca en las legales) y «Cerrar el onboarding» deja a la persona vinculada. El trabajador ve su plan en el enlace de su oferta. Si la IA falla, quedan las legales y RRHH completa a mano*
- [ ] Asistente de bienvenida que responde dudas los primeros días
- [ ] Encuestas automáticas a los 30, 60 y 90 días con alerta temprana de retiro

**Gate de salida:** de candidato aprobado a empleado en nómina sin redigitar datos; plan “Selección + Vinculación” a la venta.

---
_Actualizado: 2026-10-21 · Autor: Ricardo Nivia_
