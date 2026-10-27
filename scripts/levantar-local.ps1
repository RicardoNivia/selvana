<#
.SYNOPSIS
  Levanta Selvana en local: PostgreSQL, API (5000), worker de IA (5090) y web (5173).

.DESCRIPTION
  1. Arranca PostgreSQL si no responde en localhost:5432 (por defecto la versión portátil).
  2. Crea la base `selvana` si no existe (las migraciones las aplican la API y el worker al arrancar).
  3. Instala las dependencias del frontend si faltan.
  4. Abre una ventana por servicio. Con -Detener cierra los servicios y PostgreSQL.

  La contraseña de PostgreSQL se lee de los user-secrets de Selvana.Api (ConnectionStrings:Selvana).

.EXAMPLE
  .\scripts\levantar-local.ps1
  .\scripts\levantar-local.ps1 -SinWorker
  .\scripts\levantar-local.ps1 -Detener
#>
param(
    [switch]$SinWorker,
    [switch]$Detener,
    [string]$PgHome
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent

# Variables del .env de la raíz (PG*, ConnectionStrings__Selvana, SELVANA_PG_HOME); las ventanas las heredan
if (Test-Path "$raiz\.env") {
    Get-Content "$raiz\.env" | Where-Object { $_ -match '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$' } | ForEach-Object {
        if ($Matches[2] -ne '') { Set-Item "Env:$($Matches[1])" $Matches[2] }
    }
}
if (-not $PgHome) { $PgHome = if ($env:SELVANA_PG_HOME) { $env:SELVANA_PG_HOME } else { "$env:USERPROFILE\pgsql-portable" } }
$pgBin = Join-Path $PgHome 'pgsql\bin'
$pgData = Join-Path $PgHome 'data17'
$puertos = @{ 'API' = 5000; 'Worker IA' = 5090; 'Web' = 5173 }

function Detener-Puerto([int]$puerto) {
    Get-NetTCPConnection -LocalPort $puerto -State Listen -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
}

if ($Detener) {
    foreach ($p in $puertos.GetEnumerator()) { Detener-Puerto $p.Value; Write-Host "Detenido: $($p.Key)" }
    if (Test-Path $pgBin) { & "$pgBin\pg_ctl.exe" -D $pgData stop -m fast }
    return
}

# 1. PostgreSQL
if (-not (Test-Path "$pgBin\pg_isready.exe")) { throw "No se encontró PostgreSQL en $pgBin. Use -PgHome o SELVANA_PG_HOME." }
& "$pgBin\pg_isready.exe" -h localhost -p 5432 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Arrancando PostgreSQL...' -ForegroundColor Cyan
    # Proceso aparte: si postgres hereda la salida de esta consola, el script se queda esperando
    Start-Process "$pgBin\pg_ctl.exe" -ArgumentList '-D', "`"$pgData`"", '-l', "`"$(Join-Path $PgHome 'postgres.log')`"", 'start' -WindowStyle Hidden
    $limite = (Get-Date).AddSeconds(30)
    do { Start-Sleep 1; & "$pgBin\pg_isready.exe" -h localhost -p 5432 | Out-Null } until ($LASTEXITCODE -eq 0 -or (Get-Date) -gt $limite)
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL no arrancó; revise postgres.log.' }
}
Write-Host 'PostgreSQL listo en localhost:5432' -ForegroundColor Green

# 2. Base de datos
if (-not $env:PGPASSWORD) {
    $cadena = (dotnet user-secrets list --project "$raiz\backend\src\Host\Selvana.Api" | Select-String 'ConnectionStrings:Selvana').Line
    if (-not $cadena) { throw 'Falta PGPASSWORD en el .env o ConnectionStrings:Selvana en los user-secrets de Selvana.Api.' }
    $env:PGPASSWORD = [regex]::Match($cadena, 'Password=([^;]*)').Groups[1].Value
    $env:PGUSER = [regex]::Match($cadena, 'Username=([^;]*)').Groups[1].Value
}
$usuario = if ($env:PGUSER) { $env:PGUSER } else { 'postgres' }
$base = if ($env:PGDATABASE) { $env:PGDATABASE } else { 'selvana' }
$existe = & "$pgBin\psql.exe" -h localhost -U $usuario -d postgres -tAc "select 1 from pg_database where datname = '$base'"
if ($existe -ne '1') {
    Write-Host "Creando la base $base..." -ForegroundColor Cyan
    & "$pgBin\psql.exe" -h localhost -U $usuario -d postgres -c "create database $base" | Out-Null
}

# 3. Dependencias del frontend
if (-not (Test-Path "$raiz\frontend\node_modules")) {
    Write-Host 'Instalando dependencias del frontend...' -ForegroundColor Cyan
    Push-Location "$raiz\frontend"; npm install; Pop-Location
}

# 4. Una sola compilación (la API y el worker comparten proyectos y chocan si compilan a la vez)
foreach ($p in $puertos.GetEnumerator()) { Detener-Puerto $p.Value }
Write-Host 'Compilando el backend...' -ForegroundColor Cyan
dotnet build "$raiz\backend\Selvana.slnx" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'El backend no compila.' }

# 5. Servicios, cada uno en su ventana
function Abrir([string]$titulo, [string]$carpeta, [string]$comando) {
    $script = "`$Host.UI.RawUI.WindowTitle = 'Selvana - $titulo'; Set-Location '$carpeta'; $comando"
    Start-Process powershell.exe -ArgumentList '-NoExit', '-NoProfile', '-Command', $script
}

Abrir 'API' "$raiz\backend" "`$env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run --project src/Host/Selvana.Api --no-build --no-launch-profile --urls http://localhost:5000"
if (-not $SinWorker) {
    Abrir 'Worker IA' "$raiz\backend" "`$env:ASPNETCORE_ENVIRONMENT='Development'; `$env:DOTNET_ENVIRONMENT='Development'; dotnet run --project src/Workers/Selvana.AI.Worker --no-build --no-launch-profile --urls http://localhost:5090"
}
Abrir 'Web' "$raiz\frontend" 'npm run dev -- --port 5173 --strictPort'

Write-Host ''
Write-Host 'Levantando (la primera compilación tarda un poco):' -ForegroundColor Green
Write-Host '  Web      http://localhost:5173'
Write-Host '  API      http://localhost:5000/swagger'
if (-not $SinWorker) { Write-Host '  Worker   http://localhost:5090' }
Write-Host 'Para detener todo: .\scripts\levantar-local.ps1 -Detener'
