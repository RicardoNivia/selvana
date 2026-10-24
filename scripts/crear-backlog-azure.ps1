<#
.SYNOPSIS
  Crea en Azure Boards las épicas, features e historias definidas en docs/backlog/azure-backlog.json
  (derivado de docs/Selvana-Plan-de-trabajo.md).

  Épica        = Etapa (D, 0 a 5)
  Feature      = Sprint o bloque de sprints
  Historia     = Cada entregable (Product Backlog Item en Scrum, User Story en Agile)

  Es idempotente: si ya existe un work item del mismo tipo con el mismo título, lo reutiliza
  y solo crea lo que falta. Para agregar funcionalidades, edite el JSON y vuelva a ejecutarlo.

  El PAT se toma de -Pat, de la variable AZURE_DEVOPS_PAT o de la línea AZURE_DEVOPS_PAT=<token>
  del archivo .env en la raíz del repo (ignorado por git).

.EXAMPLE
  .\scripts\crear-backlog-azure.ps1 -Etapas D -WhatIf   # vista previa de la Etapa D
  .\scripts\crear-backlog-azure.ps1 -Etapas D
  .\scripts\crear-backlog-azure.ps1                     # todo el backlog
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Organizacion = "ricardonivia0145",
    [string]$Proyecto = "Selvana",
    [string]$Pat = $env:AZURE_DEVOPS_PAT,
    # Solo estas etapas (D, 0, 1...5); vacío = todas
    [string[]]$Etapas,
    [string]$Backlog
)

$ErrorActionPreference = "Stop"
if (-not $Backlog) { $Backlog = Join-Path $PSScriptRoot "..\docs\backlog\azure-backlog.json" }
foreach ($archivoPat in @((Join-Path $PSScriptRoot "..\.env"), (Join-Path $PSScriptRoot "..\.env.azure-devops"))) {
    if (-not $Pat -and (Test-Path $archivoPat)) {
        $Pat = ((Get-Content $archivoPat) -match '^AZURE_DEVOPS_PAT=' | Select-Object -First 1) -replace '^AZURE_DEVOPS_PAT=', ''
    }
}
if (-not $Pat) { throw "Falta el PAT: use -Pat, la variable AZURE_DEVOPS_PAT o AZURE_DEVOPS_PAT=<token> en el archivo .env de la raíz." }

$base = "https://dev.azure.com/$Organizacion/$Proyecto/_apis"
$headers = @{ Authorization = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$($Pat.Trim())")) }
$plan = ([IO.File]::ReadAllText((Resolve-Path $Backlog), [Text.Encoding]::UTF8) | ConvertFrom-Json).etapas

# --- API --------------------------------------------------------------------------------------

function Invoke-Ado([string]$Method, [string]$Uri, $Body, [string]$ContentType = "application/json") {
    $bytes = $null
    if ($Body) { $bytes = [Text.Encoding]::UTF8.GetBytes((ConvertTo-Json $Body -Depth 10)) }
    $r = Invoke-WebRequest -UseBasicParsing -Method $Method -Uri $Uri -Headers $headers -Body $bytes -ContentType "$ContentType; charset=utf-8"
    [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())
}

# ConvertFrom-Json de PowerShell 5.1 falla con algunas respuestas (claves duplicadas), se leen con regex
$tipos = [regex]::Matches((Invoke-Ado GET "$base/wit/workitemtypes?api-version=7.1"), '"name":"([^"]+)","referenceName"') | ForEach-Object { $_.Groups[1].Value }
$tipoHistoria = if ($tipos -contains "User Story") { "User Story" } elseif ($tipos -contains "Product Backlog Item") { "Product Backlog Item" } else { "Requirement" }
Write-Host "Tipo de historia: $tipoHistoria"

function Find-WorkItem([string]$Tipo, [string]$Titulo) {
    $t = $Titulo.Replace("'", "''")
    $wiql = @{ query = "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project AND [System.WorkItemType] = '$Tipo' AND [System.Title] = '$t'" }
    $m = [regex]::Match((Invoke-Ado POST "$base/wit/wiql?api-version=7.1" $wiql), '"workItems":\[\{"id":(\d+)')
    if ($m.Success) { [int]$m.Groups[1].Value } else { $null }
}

function New-WorkItem([string]$Tipo, [string]$Titulo, [string]$Descripcion, [string]$Tags, $PadreId) {
    $existente = Find-WorkItem $Tipo $Titulo
    if ($existente) { Write-Host "  = $Tipo #$existente $Titulo"; $script:existentes++; return $existente }
    if (-not $PSCmdlet.ShouldProcess("$Tipo '$Titulo'", "Crear")) { return -1 }

    $ops = [System.Collections.ArrayList]@(
        @{ op = "add"; path = "/fields/System.Title"; value = $Titulo }
        @{ op = "add"; path = "/fields/System.Description"; value = $Descripcion }
        @{ op = "add"; path = "/fields/System.Tags"; value = $Tags }
    )
    if ($PadreId -and $PadreId -gt 0) {
        [void]$ops.Add(@{ op = "add"; path = "/relations/-"; value = @{
            rel = "System.LinkTypes.Hierarchy-Reverse"; url = "https://dev.azure.com/$Organizacion/_apis/wit/workItems/$PadreId" } })
    }
    $tipoUrl = [Uri]::EscapeDataString($Tipo)
    $resp = Invoke-Ado POST "$base/wit/workitems/`$$($tipoUrl)?api-version=7.1" $ops.ToArray() "application/json-patch+json"
    $id = [int][regex]::Match($resp, '^\{"id":(\d+)').Groups[1].Value
    Write-Host "  + $Tipo #$id $Titulo"
    $script:creados++
    $id
}

# --- Creación ---------------------------------------------------------------------------------

$creados = 0; $existentes = 0
foreach ($e in $plan) {
    $num = ($e.etapa -split ' ')[1]
    if ($Etapas -and $num -notin $Etapas) { continue }
    $desc = "<p><b>Objetivo:</b> $($e.objetivo)</p><p><b>Sprints:</b> $($e.sprints)</p><p><b>Gate de salida:</b> $($e.gate)</p>"
    $epicId = New-WorkItem "Epic" $e.etapa $desc "Selvana; Etapa $num" $null

    foreach ($f in $e.features) {
        $fDesc = "<p>$($e.etapa) · Sprint $($f.sprint)</p>"
        $featId = New-WorkItem "Feature" $f.titulo $fDesc "Selvana; Etapa $num; Sprint $($f.sprint)" $epicId

        foreach ($h in $f.historias) {
            $hDesc = "<p>$($f.titulo) ($($e.etapa)).</p>"
            if ($num -ne "D") { $hDesc += "<p><b>Definición de terminado (funciones con IA):</b> funciona en el flujo completo con datos anonimizados; supera la meta de calidad; muestra explicación y permite corregir; registra sugerencia y decisión; mide costo de IA; pasa revisión de datos personales y sesgo.</p>" }
            [void](New-WorkItem $tipoHistoria $h $hDesc "Selvana; Etapa $num; Sprint $($f.sprint)" $featId)
        }
    }
}
Write-Host "`nListo: $creados creados, $existentes ya existían."
