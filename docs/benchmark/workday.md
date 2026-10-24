# Benchmark · Workday (HCM y Workday Recruiting)

Consultado el 22 de octubre de 2026 en fuentes oficiales de Workday (producto, documentación, datasheets, sala de prensa), prensa y reseñas (G2, Gartner Peer Insights). Varias cifras son afirmaciones del fabricante o estimaciones de terceros.

## Qué es

HCM para **grandes empresas y mediana empresa alta** (típicamente más de 500 a 1.000 empleados): Core HCM, Recruiting, Talent Mobility, Payroll y Global Payroll Connect, Contingent Sourcing. [Agentes de selección](https://www.workday.com/en-us/ai-agents/hr/talent-acquisition.html)

## Reclutamiento

| Función | Workday | Selvana |
| --- | --- | --- |
| Requisición y aprobación | ✔ Responsabilidades, compensación y aprobaciones en la requisición ([doc](https://doc.workday.com/admin-guide/en-us/human-capital-management/staffing/job-requisitions/wfd1632483475890.html)) | ✔ Líder → RRHH → gerencia con cuatro ojos y devolución con motivo |
| Portal y bolsas | ✔ Sitios de carreras internos y externos y publicación en bolsas ([doc](https://doc.workday.com/admin-guide/en-us/human-capital-management/recruiting/recruiting-setup/lzi1594032985432.html)) | ✔ Portal público; bolsas pendientes |
| Emparejamiento / ranking | ✔ **Recruiting Agent (HiredScore)**: búsqueda, filtro y emparejamiento; calificación A/B/C/D frente a los requisitos ([doc](https://doc.workday.com/hiredscore/en-us/workday-hiredscore/recruiter-productivity-/concept--hiredscore-grades.html)) | ✔ Puntaje 0-100 con 3 a 5 razones y evidencia |
| Explicación | ✔ Desde el 7-ago-2026 (opcional): calificación con LLM requisito por requisito y vista **«Fit & Gap»** ([notas](https://doc.workday.com/hiredscore/en-us/workday-hiredscore/hiredscore-release-notes/2026/august/wednesday--5th-august.html)) | ✔ Por defecto: razones con evidencia, requisitos cumplidos y no demostrados |
| Variables protegidas y sesgo | ✔ Quita dirección exacta, aficiones, género y raza; auditorías de sesgo según NYC LL144 ([datasheet](https://www.workday.com/content/dam/web/en-us/documents/datasheets/responsible-ai-at-hiredscore.pdf)) | ✔ Perfil anónimo; reporte de sesgo por vacante pendiente (sprint 7) |
| Conversación con candidatos | ✔ **Paradox** (adquirida en 2025): postulación, filtro y agenda por SMS, WhatsApp y Messenger ([Paradox](https://www.workday.com/en-us/products/paradox-ai/overview.html)) | ◐ Preselección por WhatsApp planeada |
| Agente orquestador | ✔ Talent Acquisition Agent (Sana) con cinco «habilidades»; agenda entrevistas de forma autónoma | Motor de agentes con bandeja: la IA propone, una persona aprueba |
| Humano en el ciclo | El reclutador «define límites»; control explícito no encontrado | ✔ Toda decisión que afecta a una persona requiere aprobación |
| Kanban | No encontrado (hay una «Candidate Grid») | Siguiente bloque |

## Plataforma

- **Nómina de terceros:** Cloud Connect for Third-Party Payroll con socios certificados y Global Payroll Connect con API REST bidireccional ([datasheet](https://www.workday.com/content/dam/web/en-us/documents/datasheets/workday-global-payroll-connect-datasheet-enus.pdf)).
- **Precio:** no público. Estimación de terceros: Core HCM ~USD 34–42 por empleado al mes; entrada de más de USD 100.000 al año ([Redress](https://redresscompliance.com/workday-pricing-2026-what-enterprises-actually-pay.html)).
- **Colombia:** nómina colombiana mediante socios (Remote/Workday GO, CloudPay). Oficina en Bogotá, clientes colombianos nombrados, nómina electrónica DIAN y Ley 1581: no encontrado.
- **Riesgo legal:** *Mobley v. Workday* por sesgo en contratación con IA, sigue en curso (acción colectiva por edad certificada preliminarmente en mayo de 2025) ([Maynard Nexsen](https://www.maynardnexsen.com/publication-emerging-liability-for-ai-driven-hiring-tools-key-developments-in-mobley-v-workday-inc)).
- **Reseñas:** G2 Workday Recruiting 3,7/5 (~101); Gartner 4,3 (44). Se elogia la configuración y la integración con Workday HR; se critica la curva de aprendizaje y la navegación rígida.

## Lectura para Selvana

Workday es la referencia de **IA explicable y auditada** en selección (Fit & Gap, auditorías de sesgo) y de **conversación de alto volumen** (Paradox). Selvana iguala la explicación por defecto, con evidencia citada, a precio de pyme y con cumplimiento colombiano (Ley 1581) integrado. El caso *Mobley* confirma la regla de producto: la IA sugiere, una persona decide y todo queda auditado.
