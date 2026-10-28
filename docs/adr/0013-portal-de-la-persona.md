# ADR 0013: Portal de la persona con cuenta propia, unido a sus postulaciones por correo y cédula

- Estado: Aceptado
- Fecha: 2026-10-28

## Contexto

Hasta hoy cada cosa le llega al candidato por un enlace distinto con token (chat de preselección, pruebas, oferta y firma, documentos de ingreso), y no tiene dónde ver cómo van sus postulaciones. El plan (sección 5 de `docs/backlog/Modulos-Vinculacion-Empleados-Desempeno-Clima.md`) pedía un portal que lo reúna. La persona no es un usuario de la consola: no pertenece a una empresa ni tiene rol, y puede postularse a vacantes de varias empresas que usan Selvana.

## Decisión

- **Cuenta propia de la persona** (`CuentaCandidato`), distinta de los usuarios de la consola. Vive en el módulo Reclutamiento (esquema `reclutamiento`), porque ahí están el candidato, la hoja de vida y el portal de empleo. Si el portal crece hacia el empleado (bloque P5), se evalúa moverla a un módulo propio.
- **Acceso con correo y contraseña**, como la consola (ADR 0009): PBKDF2-SHA256, bloqueo tras 5 intentos y límite por IP. La sesión es **otra cookie** (`selvana.portal`) con **otro esquema**: la sesión del portal nunca sirve en la consola ni al revés. Recuperar la contraseña llega con la integración de correo.
- **Registro con la hoja de vida**: la persona sube su hoja de vida, la IA la lee (mismo lector de la postulación) y confirma sus datos; acepta la autorización Ley 1581 de Selvana para su perfil.
- **Unión con lo que ya hizo**: las postulaciones (y sus chats, pruebas, ofertas y firmas) se muestran en el portal **solo si el correo y la cédula de la cuenta coinciden** con los del candidato. Así nadie ve lo de otra persona solo sabiendo su correo. No se guarda un vínculo aparte: se consulta por esos dos datos.
- **Los enlaces llevan al portal**: el chat, las pruebas, la oferta y la firma se responden desde el portal con la sesión de la persona. Los enlaces con token siguen funcionando y ofrecen entrar al portal o crear la cuenta con el correo ya escrito, para que nadie se quede sin poder responder.
- **Lo que ve la persona**: estados en palabras claras (recibida, en revisión, en preselección, finalista, contratada, no seleccionada), nunca puntajes internos, razones del ranking ni motivos de descarte.

## Consecuencias

- La API tiene dos esquemas de cookie; los endpoints del portal (`/api/v1/portal/cuenta/...`) exigen el esquema del portal y los de la consola el suyo.
- Si una persona cambia de correo o su cédula quedó mal leída, sus postulaciones anteriores no aparecen hasta que corrija sus datos; la corrección de la cédula pasa por RRHH (Ley 1581, solicitudes del titular).
- Falta para producción: verificar el correo, recuperar la contraseña y avisos por WhatsApp o correo (van con las integraciones).
