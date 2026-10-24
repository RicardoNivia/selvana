# ADR 0009: Inicio de sesión propio con cookie, además de OpenID Connect

- Estado: Aceptado
- Fecha: 2026-10-22

## Contexto

El andamiaje dejó la autenticación solo por OpenID Connect (Microsoft Entra ID o Google) y, sin emisor configurado, un usuario de desarrollo fijo que entraba sin pedir nada. Para probar la consola, vender con prueba gratuita y atender empresas sin Entra ID ni Google Workspace se necesita un ingreso con correo y contraseña que funcione desde el primer día, sin depender de un proveedor externo.

## Decisión

- Selvana emite su propia sesión: `POST /api/v1/sesion/ingresar` valida correo y contraseña y responde con una **cookie** `selvana.sesion` **HttpOnly**, **SameSite=Strict** y **Secure** fuera de desarrollo, de 8 horas con renovación deslizante. `GET /api/v1/sesion` devuelve quién está en la consola y `POST /api/v1/sesion/salir` la cierra.
- El front y la API comparten sitio (localhost en desarrollo, el mismo dominio en producción), así que SameSite=Strict corta el CSRF sin tokens adicionales. El frontend envía `credentials: 'include'` y la política CORS `SelvanaWeb` ya permite credenciales solo a los orígenes configurados.
- Las contraseñas se guardan derivadas con **PBKDF2-SHA256** (600.000 iteraciones, sal de 16 bytes), nunca en texto. Si el correo no existe se hace el mismo cálculo para no revelarlo por el tiempo de respuesta, y el mensaje de error es el mismo.
- **Bloqueo**: 5 intentos fallidos seguidos bloquean al usuario 15 minutos; además, el endpoint tiene un límite de 10 intentos por minuto por IP.
- **OpenID Connect sigue**: si hay `Auth:Authority`, un selector acepta tokens Bearer del emisor y la cookie para todo lo demás. Los botones de Microsoft y Google del inicio de sesión se activan al configurarlo.
- Se elimina el usuario de desarrollo fijo. En desarrollo la semilla crea la empresa de ejemplo con dos usuarios (gerente de RRHH y administrador) y les pone la contraseña de los user-secrets de la API (`Desarrollo:Contrasena`); nunca hay contraseñas en el repositorio.

## Consecuencias

- La consola exige sesión en todos los ambientes; las pruebas de integración verifican que responde 401 sin cookie.
- Falta para producción: recuperar la contraseña por correo (va con las integraciones de correo, al final), invitar usuarios con enlace para crear la contraseña (bloque 2.2) y doble factor para administradores.
- Si un día se adopta un proveedor de identidad único, el ingreso propio se puede apagar sin tocar los módulos: ellos solo leen `IContextoUsuario`.
