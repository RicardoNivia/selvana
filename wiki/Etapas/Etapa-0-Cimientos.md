# Etapa 0 – Cimientos (sprints 1 a 3, 6 semanas)

Objetivo: dejar listos el descubrimiento con clientes, el núcleo de datos y la plataforma de IA sobre la que se montan todos los agentes.

**Sprint 1 – Descubrimiento y diseño**

- [x] Benchmark de la competencia: revisar sitios, demos públicas, precios y reseñas de Buk, Factorial, Humand, Heinsohn, Workday y SAP (ver Comparativo)
- [ ] Lista de funciones “obligatorias” (las que todos tienen) y “diferenciadoras” (las que nadie resuelve bien)
- [ ] Propuesta de valor, nombre de planes y precio de lanzamiento
- [ ] Prototipo navegable (Claude Design) del flujo vacante → candidatos → aprobación, probado con 5 profesionales de RRHH externos (pruebas de usabilidad pagas, no clientes)
- [ ] Documento de decisiones: proveedor de modelo de IA, proveedor de WhatsApp Business (pruebas psicotécnicas: suite propia, ADR 0011)

**Sprint 2 – Núcleo de datos y seguridad**

- [x] Modelo de datos base: Persona, Empresa, Sede, Área, Centro de costo, Cargo, Usuario, Rol — *sede, área, centro de costo y cargo (bloque 2.1); empresa, empresa usuaria, usuario y rol (bloque 2.2); la persona vive como candidato sin duplicar por documento*
- [ ] Capa de integración agnóstica (ver Integración con cualquier ERP de nómina) y primer conector: SINCO — *hecho: contrato canónico y conector de archivo CSV (carga manual); falta el conector nativo de SINCO (credenciales y documentación de su API)*
- [x] Roles y permisos: administrador, gerente RRHH, analista, líder, empleado, candidato — *bloque 2.2: matriz rol × acción única (la API autoriza con ella), invitación con enlace para crear la contraseña, rol sugerido según el cargo de la nómina con razón y evidencia, y registro si se cambia*
- [x] Multiempresa y, para empresas temporales, multicliente (empresa usuaria) — *bloque 2.2: empresas usuarias con NIT validado (DIAN), selector en la barra lateral y alcance por usuario: las vacantes y postulantes de un cliente solo los ve quien lo tiene asignado. Falta: marca blanca del portal por empresa usuaria*
- [x] Registro de autorización de tratamiento de datos (Ley 1581) y bitácora de auditoría — *bloque 2.3 (ADR 0010): bitácora encadenada con huellas SHA-256 y verificación de integridad, que distingue IA, personas, sistema y titulares; registro de autorizaciones con evidencia; solicitudes de titulares con plazo legal en días hábiles colombianos, respuesta preparada con los datos reales y supresión ejecutada en todos los módulos; formulario público «Tus datos personales»*
- [ ] Almacenamiento documental cifrado — *hojas de vida cifradas (Data Protection) y cada apertura con motivo queda en la bitácora; falta: llaves administradas (Key Vault) y el resto de documentos*

**Sprint 3 – Plataforma de IA mínima**

- [x] Servicio de IA desacoplado (gateway) con proveedor intercambiable y control de costos por cliente
- [x] Motor de agentes: herramientas, memoria del caso, registro de cada acción (lectura se ejecuta, escritura queda propuesta; `ia.memorias_caso` y `ia.acciones_agentes`)
- [x] Bandeja de aprobaciones genérica (“la IA propone, tú apruebas”) reutilizable en todo el módulo — *cualquier módulo publica una propuesta (Core directo, los demás por su outbox) con razones, evidencia, campos y su acción; la decisión la ejecuta el módulo dueño y queda en la bitácora; la consola abre en la bandeja con la actividad de los agentes del día. Hoy entran vacantes redactadas, perfiles de cargo, uniones de cargos y solicitudes de titulares. Falta: tiempo real con SignalR (hoy se refresca cada 15 s)*
- [ ] Banco de pruebas de IA: 200 hojas de vida reales anonimizadas y 20 vacantes etiquetadas por el experto funcional de RRHH (hojas de vida con autorización o sintéticas) — *hecho el banco sintético: 24 hojas de vida (4 formatos, con trampas de variables protegidas y grupos para sesgo) y 20 frases de vacantes (`backend/bancos-ia`); faltan las reales anonimizadas que etiqueta el experto de RRHH*
- [x] Pipeline de evaluación automática (precisión de extracción, calidad del ranking, sesgo) — *comando `evaluar` del worker: corre el banco contra los modelos reales, mide campos clave, variables protegidas, brecha entre grupos, salario legal y explicación, guarda en `ia.evaluaciones_agentes` y escribe el informe; por defecto una muestra mínima para cuidar el presupuesto. Primera corrida: lector 100 % y redactor 100 %. El ranking se mide cuando exista (sprint 7)*

**Gate de salida:** prototipo validado en pruebas de usabilidad, benchmark cerrado, núcleo de datos integrado al ERP y primer agente de prueba respondiendo en el entorno de desarrollo.

## Demo local del bloque 2.1

1. `dotnet run --project backend/src/Host/Selvana.Api --no-launch-profile --urls http://localhost:5000`
2. `dotnet run --project backend/src/Workers/Selvana.AI.Worker`
3. `cd frontend && npm run dev` y abrir http://localhost:5173 → **Conectar nómina** → *Usar nómina de ejemplo*.
4. La IA propone unir cargos repetidos; se revisa, se aplica y en **Estructura y cargos** la IA redacta el perfil de cada cargo para aprobar.

Swagger: http://localhost:5000/swagger

---
_Actualizado: 2026-10-22 · Autor: Ricardo Nivia_
