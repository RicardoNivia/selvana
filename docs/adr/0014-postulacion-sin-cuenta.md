# ADR 0014: Postularse sin cuenta, solo con la hoja de vida

- Estado: Aceptado
- Fecha: 2026-10-29
- Cambia: ADR 0013 (en la parte que exigía cuenta del portal para postularse)

## Contexto

El ADR 0013 pidió cuenta en el portal para postularse, para que nadie se postulara a nombre de otra persona. En la práctica es una barrera
para la selección masiva: la persona que ve una vacante en el celular quiere postularse en dos minutos, sin registrarse. El producto pide
postularse subiendo la hoja de vida (o el PDF del perfil de LinkedIn) y que la IA la lea.

## Decisión

- **La postulación no exige sesión.** La persona sube su hoja de vida (PDF, Word o foto, incluido el PDF que genera LinkedIn), la IA la lee, ella
  confirma o corrige sus datos, responde las preguntas de la empresa y acepta la autorización Ley 1581 (queda la evidencia: versión, fecha,
  IP, navegador y huella).
- **Si hay sesión del portal**, se usa: la cédula y el correo salen de su cuenta (no se pueden cambiar en la postulación) y puede postularse con
  su perfil en un clic. El endpoint es público con la sesión opcional (`SesionesPortal.UsarSiHay`).
- **Unión con la cuenta:** como antes, si la persona crea su cuenta después con el mismo correo y la misma cédula, sus postulaciones aparecen
  en su portal (ADR 0013).
- **Protección:** límite de peticiones por IP del portal público, archivos de hasta 10 MB, hoja de vida cifrada y borrador con vencimiento.

## Consecuencias

- Alguien podría postularse con datos de otra persona. Se acepta el riesgo, igual que en cualquier bolsa de empleo: la autorización queda con
  su evidencia, la empresa ve la hoja de vida y el titular puede pedir la supresión (solicitudes del titular).
- Cada hoja de vida subida sin cuenta gasta IA al leerse; el límite por IP acota el abuso. Si hiciera falta, se agrega un captcha.
