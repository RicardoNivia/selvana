<#
  Despliega Selvana en el ambiente de pruebas de Azure desde este equipo:
  construye las imágenes, las sube al registro y las levanta en la VM con docker compose.
  Requiere: Docker Desktop encendido, `az login` hecho y deploy/azure/.env.pruebas (git lo ignora).
  Uso: ./deploy/azure/desplegar.ps1            (versión = commit actual)
       ./deploy/azure/desplegar.ps1 -Version 1a2b3c4   (volver a una versión ya subida, sin construir)
#>
param(
    [string]$Version,
    [switch]$SinConstruir
)

$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path "$PSScriptRoot/../.."
$archivoEnv = Join-Path $PSScriptRoot '.env.pruebas'
if (-not (Test-Path $archivoEnv)) { throw "Falta $archivoEnv. Cópielo de .env.example y complete los valores." }

# az.cmd pasa por cmd.exe y daña las consultas; se llama la CLI directo con su Python
function az { & "C:\Program Files\Microsoft SDKs\Azure\CLI2\python.exe" -IBm azure.cli @args }

$valores = @{}
Get-Content $archivoEnv | Where-Object { $_ -match '^\s*([A-Z_]+)=(.*)$' } | ForEach-Object { $valores[$Matches[1]] = $Matches[2] }
$registro = $valores['REGISTRO']
$servidor = "selvana@$($valores['DOMINIO'])"
$llave = Join-Path $HOME '.ssh/selvana_azure'

if (-not $Version) {
    $Version = (git -C $raiz rev-parse --short HEAD).Trim()
    if (git -C $raiz status --porcelain) { $Version = "$Version-local" }
} else {
    $SinConstruir = $true
}

if (-not $SinConstruir) {
    Write-Host "Construyendo la versión $Version..."
    docker build -t "$registro/selvana-api:$Version" -f "$raiz/backend/src/Host/Selvana.Api/Dockerfile" "$raiz/backend"
    docker build -t "$registro/selvana-ai-worker:$Version" -f "$raiz/backend/src/Workers/Selvana.AI.Worker/Dockerfile" "$raiz/backend"
    # Web y API comparten dominio: la web llama a /api con rutas relativas
    docker build -t "$registro/selvana-web:$Version" --build-arg VITE_API_URL= "$raiz/frontend"

    az acr login --name $registro.Split('.')[0] --only-show-errors
    foreach ($imagen in 'selvana-api', 'selvana-ai-worker', 'selvana-web') {
        docker push "$registro/${imagen}:$Version"
    }
}

Write-Host "Subiendo la configuración a la VM..."
$envServidor = (Get-Content $archivoEnv | Where-Object { $_ -notmatch '^VERSION=' }) + "VERSION=$Version"
$temporal = New-TemporaryFile
Set-Content -Path $temporal -Value $envServidor -NoNewline:$false
scp -i $llave -o StrictHostKeyChecking=accept-new "$PSScriptRoot/docker-compose.yml" "$PSScriptRoot/Caddyfile" "${servidor}:/opt/selvana/"
scp -i $llave $temporal "${servidor}:/opt/selvana/.env"
Remove-Item $temporal

Write-Host "Levantando la versión $Version..."
$usuario = $valores['REGISTRO_USUARIO']
$clave = $valores['REGISTRO_CLAVE']
ssh -i $llave $servidor @"
set -e
cd /opt/selvana
chmod 600 .env
echo '$clave' | docker login $registro -u '$usuario' --password-stdin >/dev/null
docker compose pull -q
docker compose up -d --remove-orphans
docker image prune -f >/dev/null
docker compose ps --format 'table {{.Service}}\t{{.Status}}'
"@

Write-Host "Listo: https://$($valores['DOMINIO'])"
