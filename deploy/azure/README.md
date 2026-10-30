# Ambiente de pruebas en Azure

URL: https://selvana-pruebas.northcentralus.cloudapp.azure.com

Cuenta gratuita de Azure (12 meses), suscripción «Azure subscription 1», grupo `rg-selvana-pruebas`, región North Central US
(la única que permitía a la vez PostgreSQL B1MS y una VM gratuita x64 para esta suscripción).

| Recurso | Nombre | Plan |
| --- | --- | --- |
| VM Ubuntu 24.04 | `vm-selvana` (B2ats v2, disco P6 de 64 GB, 2 GB de swap) | Gratis 750 h/mes |
| PostgreSQL 17 | `selvana-db-abfe08`, base `selvana` (B1MS, 32 GB, sin HA) | Gratis 750 h/mes |
| Registro de imágenes | `selvanapruebasabfe08.azurecr.io` (Standard) | Gratis 12 meses |

En la VM corre `docker-compose.yml` de esta carpeta: Caddy (HTTPS automático) publica la web y `/api`; `/internal`,
`/swagger` y `/openapi` no se publican. La base solo acepta conexiones desde la IP de la VM; el SSH, solo desde la IP de quien
la administra (regla `ssh-mi-ip` del NSG). Los secretos viven en `/opt/selvana/.env` del servidor y en
`deploy/azure/.env.pruebas` local (git lo ignora; plantilla en `.env.example`).

## Desplegar

- **Automático:** cada push a `main` corre `.github/workflows/ci-cd.yml`: compila, prueba y, si todo pasa, construye las
  imágenes, las sube al registro y las levanta en la VM. Si algo falla no despliega.
- **Manual desde este equipo:** `./deploy/azure/desplegar.ps1` (versión = commit actual), o
  `./deploy/azure/desplegar.ps1 -Version <commit>` para volver a una versión ya subida.

## Secreto de GitHub (una sola vez)

El workflow entra a Azure con una identidad de servicio limitada al grupo `rg-selvana-pruebas`:

Lo más simple es `.\scripts\configurar-cicd.ps1` (requiere `az login` y `gh auth login`): crea la identidad, guarda el secreto en GitHub sin mostrarlo y lanza el workflow. A mano:

```powershell
az ad sp create-for-rbac --name sp-selvana-github --role Contributor `
  --scopes /subscriptions/<id-suscripcion>/resourceGroups/rg-selvana-pruebas --json-auth
```

Copie el JSON completo en GitHub → Settings → Secrets and variables → Actions → New repository secret, con el nombre
`AZURE_CREDENTIALS`.

## Datos de ejemplo

Con `Ejemplo:Sembrar=true` (lo pone el `docker-compose.yml` de esta carpeta) la API crea al arrancar tres empresas
independientes con el flujo completo de selección y vinculación. Es idempotente: si la empresa ya tiene vacantes, no la toca.
Se hace con los métodos de dominio y **sin IA**: lo que normalmente produce un agente (borrador de la vacante, lectura de la HV,
ranking, preselección, carta oferta, lectura de documentos) se fija a mano con el modelo «ejemplo (sin IA)». No se encola nada
hacia el AI.Worker, así que sembrar no gasta tokens; la IA solo gasta cuando alguien usa una función de IA en el sitio. Las hojas
de vida y los documentos son PDF ficticios de una página, cifrados como los reales.

Código: `SemillaEjemplo` en `Selvana.Core.Infrastructure/Empresas`, `Selvana.Reclutamiento.Infrastructure/Ejemplo` y
`Selvana.Vinculacion.Infrastructure/Ejemplo` y `Selvana.Expediente.Infrastructure/Ejemplo` (más `SemillaAmpliada` en Reclutamiento); se llaman desde `Program.cs`.

| Empresa | Tipo | Quién entra | Qué trae |
| --- | --- | --- | --- |
| Talento Andino EST S.A.S. | EST con dos empresas usuarias (Retail Express Colombia, Agroindustrial del Tolima) | `EJEMPLO_CORREO` (Administrador, contraseña `EJEMPLO_CONTRASENA`) y `valentina.rios@talentoandino.co` (Gerente RRHH) | Asesor comercial (8 postulantes en todas las etapas: recibida, puntaje ajustado, descartada por filtros, preselección invitada y completa, pruebas, terna y oferta por aprobar), auxiliar de bodega (5 postulantes: pruebas enviadas, documentos por revisar y un empleado creado), analista de nómina esperando aprobación de gerencia y un coordinador en borrador |
| Logística Caribe S.A.S. | Empresa directa | `mateo.herrera@logisticacaribe.co` (Administrador) | Conductor de camión C2 publicada con 4 postulantes (preselección completa y un descarte por filtros) |
| Clínica del Valle IPS | Empresa directa | `juliana.pardo@clinicadelvalle.co` (Administrador) | Auxiliar de enfermería publicada con 3 postulantes (pruebas completas, preselección invitada) y una recepcionista en borrador |

En la bandeja de Talento Andino quedan tres decisiones: la requisición de analista de nómina (gerencia), la oferta de
Natalia Gómez y los documentos de ingreso de Yeison Bermúdez.

**Datos ampliados (29/10/2026).** Además de lo anterior, cada arranque completa (sin duplicar) diez vacantes más, cada una con catorce
postulantes en todas las etapas (recibida, ajustada, descartada, preselección, pruebas, terna, oferta, documentos y contratada):

| Empresa | Vacantes nuevas |
| --- | --- |
| Talento Andino | Cajero de supermercado y mercaderista (Retail Express), operario de producción (Agroindustrial del Tolima), asesor de servicio al cliente |
| Logística Caribe | Auxiliar de bodega, montacarguista y coordinador de despachos |
| Clínica del Valle | Auxiliar de farmacia, enfermera jefe de urgencias y camillero |

- **Vinculación:** 21 procesos nuevos (7 con la oferta por aprobar, 7 con documentos por revisar y 7 contratados); los contratados llegan al expediente.
- **Expediente:** planta de 36, 18 y 18 empleados «del archivo de planta», con novedades (aumentos y traslados), carpeta con cursos vigentes, por vencer y vencidos,
  afiliaciones incompletas, retiros, campaña de actualización pendiente, cambios propuestos desde el portal y una lectura de la IA por decidir.
- **Portal de la persona:** unas 130 cuentas con la contraseña `CONTRASENA_PRUEBAS`: los candidatos desde preselección en adelante (ven sus procesos y pendientes),
  los contratados y un tercio de la planta (ven «Mi empleo»), con «Mis documentos» ya cargados. Ejemplos: `natalia.gomez@ejemplo.co` (oferta por aceptar),
  `yeison.bermudez@ejemplo.co` (documentos de ingreso pendientes), y cualquier correo `…@ejemplo.co` de la ficha de un empleado.

**Desempeño y clima (29/10/2026).** Cada empresa tiene un ciclo 2025 cerrado y calibrado (con su 9-box) y un ciclo 2026 abierto de 180° con evaluaciones en curso, calificadas, autoevaluadas y algunas con borrador del copiloto; una encuesta de clima abierta con respuestas de casi toda la planta (un área con el clima bajo) y su análisis de comentarios, y pulsos de ingreso con alertas en la bandeja. El enlace de la encuesta se genera en Clima → la encuesta → «Generar enlace nuevo».

**Registro y prueba gratuita (30/10/2026).** Cualquier empresa se registra en `/registro` y arranca 30 días de prueba con el recorrido guiado. Para una prueba de carga en este ambiente se puede arrancar la API una vez con `Carga__Postulaciones=1000` (crea la vacante «Operario de producción · selección masiva» en Talento Andino).

**Mi equipo (30/10/2026).** Cada área tiene su líder: en Talento Andino `andres.gomez@talentoandino.co` (Comercial y Tiendas), `carolina.mejia@talentoandino.co` (Producción y Logística) y el dueño del ambiente (`EJEMPLO_CORREO`, Gestión humana); en Logística Caribe `jorge.castro@logisticacaribe.co` (Bodega) y `mateo.herrera@` (Transporte); en Clínica del Valle `diana.ruiz@clinicadelvalle.co` (Urgencias y Hospitalización) y `juliana.pardo@` (Admisiones y Farmacia). Los líderes entran con rol Líder y ven solo su equipo en «Mi equipo».

Los usuarios ficticios usan la contraseña `CONTRASENA_PRUEBAS`. Además sigue la empresa de desarrollo «Servicios Andinos EST»
(`laura.moreno@`, `admin@` y `camilo.duarte@serviciosandinos.co`, misma contraseña). Solo datos ficticios: nada real de candidatos.

Para volver a sembrar desde cero hay que borrar los datos de esas empresas (o recrear la base) y reiniciar la API.
