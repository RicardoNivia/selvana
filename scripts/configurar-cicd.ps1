<#
.SYNOPSIS
  Configura el despliegue automático (una sola vez): crea la identidad de servicio del CI/CD en Azure
  y guarda AZURE_CREDENTIALS en GitHub sin mostrar el secreto en pantalla.

.DESCRIPTION
  Requiere sesión en Azure (az login) y en GitHub (gh auth login).
  La identidad queda limitada al grupo rg-selvana-pruebas. Al final lanza el workflow sobre main.

.EXAMPLE
  .\scripts\configurar-cicd.ps1
#>
$ErrorActionPreference = 'Stop'
$repo = 'RicardoNivia/selvana'

gh auth status *> $null
if ($LASTEXITCODE -ne 0) { throw 'Falta la sesión de GitHub: ejecute gh auth login.' }
$suscripcion = az account show --query id -o tsv
if (-not $suscripcion) { throw 'Falta la sesión de Azure: ejecute az login.' }

Write-Host 'Creando la identidad sp-selvana-github (Contributor en rg-selvana-pruebas)...' -ForegroundColor Cyan
$credenciales = az ad sp create-for-rbac --name sp-selvana-github --role Contributor `
    --scopes "/subscriptions/$suscripcion/resourceGroups/rg-selvana-pruebas" --json-auth --only-show-errors
if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear la identidad de servicio.' }

# El JSON va directo a GitHub por la entrada estándar: no se escribe en disco ni en la consola
$credenciales | gh secret set AZURE_CREDENTIALS --repo $repo
if ($LASTEXITCODE -ne 0) { throw 'No se pudo guardar el secreto en GitHub.' }
Write-Host 'Secreto AZURE_CREDENTIALS guardado en GitHub.' -ForegroundColor Green

gh workflow run ci-cd.yml --repo $repo --ref main
Write-Host "Workflow lanzado: gh run watch --repo $repo" -ForegroundColor Green
