# Lanzamiento y venta sin clientes piloto

Sin pilotos, la validación sale del benchmark del mercado, de pruebas de usabilidad con profesionales de RRHH externos y de la analítica de uso de una **prueba gratuita pública**. El producto se publica y se vende por planes desde el sprint 10.

| Plan | Se publica | Incluye | Cliente objetivo |
| --- | --- | --- | --- |
| Selección IA | Sprint 10 (fin Etapa 1) | Vacantes con IA, portal de empleo, lector de HV, ranking explicable, preselección por WhatsApp, pruebas | Empresas temporales, retail, BPO, cualquier empresa que contrate seguido |
| Selección + Vinculación | Sprint 14 | + contratos con firma, agente de documentos, alta en nómina por conector, onboarding, paquete temporales | Quien quiere eliminar la digitación de ingresos |
| Talento Core | Sprint 18 | + expediente vivo, asistente del empleado y del líder | Empresas con mucho trámite de empleados |
| Selvana Completo | Sprint 30 | + desempeño con copiloto, formación, clima, retención predictiva | Empresa mediana y grande |

**Modelo de precio** (alineado con la práctica del mercado, por ejemplo Buk): suscripción mensual por colaborador activo, con descuento por volumen y por módulos combinados; el plan Selección IA también por vacante o paquete de postulaciones para empresas con poca planta y mucha rotación. El consumo de IA queda incluido hasta un límite por plan.

**Canales de venta**

- Prueba gratuita de 30 días con registro propio y onboarding dentro del producto.
- Base de clientes actual de SINCO como primer canal (con conector nativo listo).
- Socios integradores de otros ERP de nómina que revendan Selvana con su conector.
- Contenido especializado: comparativos, guías legales de selección en Colombia, webinars mensuales.

**Sitio comercial y acceso (va con el portal de empleo, antes de la prueba gratuita)**

- [x] Página comercial en `/`: resume el producto para vender con la marca Dosel (la bandeja «la IA propone, tú decides» como pieza central, regla del violeta, cómo avanza una vacante, sectores, diferenciales para Colombia y módulos), con enlaces al inicio de sesión y al portal de empleo
- [x] Página de inicio de sesión en `/ingresar` con logo animado: correo y contraseña con sesión propia por cookie HttpOnly, bloqueo tras 5 intentos y límite por IP (ADR 0009); botones de Microsoft y Google listos para OpenID Connect
- [x] Movimiento de la marca: logo con hojas que brotan y se mecen, página comercial con escena viva de la bandeja, contadores, hojas que caen y apariciones al desplazar (respeta movimiento reducido)
- [ ] Recuperar contraseña por correo (va con las integraciones de correo) y doble factor para administradores
- [ ] Activar el proveedor de identidad (Microsoft Entra ID o Google) y cerrar sesión de verdad
- [ ] Registro de la prueba gratuita de 30 días desde la página comercial (crea la empresa y su administrador)
- [ ] Página de planes y precios, preguntas frecuentes y política de datos publicada
- [ ] SEO (metadatos, sitemap, datos estructurados `JobPosting` en el portal) y analítica de uso de la página comercial

**Cómo se valida sin pilotos:** cada función se compara con el benchmark antes de construirla, se prueba con 5 profesionales de RRHH externos antes de publicarla y, ya publicada, se mide con analítica de uso (activación, vacantes cerradas, % de sugerencias de IA aceptadas).

---
_Actualizado: 2026-10-22 · Autor: Ricardo Nivia_
