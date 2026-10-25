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

```powershell
az ad sp create-for-rbac --name sp-selvana-github --role Contributor `
  --scopes /subscriptions/<id-suscripcion>/resourceGroups/rg-selvana-pruebas --json-auth
```

Copie el JSON completo en GitHub → Settings → Secrets and variables → Actions → New repository secret, con el nombre
`AZURE_CREDENTIALS`.

## Usuarios de prueba

`laura.moreno@serviciosandinos.co` (Gerente RRHH), `admin@serviciosandinos.co` y `camilo.duarte@serviciosandinos.co`; la
contraseña es `CONTRASENA_PRUEBAS` del `.env.pruebas`. Solo datos de prueba: nada real de candidatos.
