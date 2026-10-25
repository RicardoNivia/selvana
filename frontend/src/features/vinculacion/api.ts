import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiArchivo, apiGet, apiSend, apiUpload } from '../../shared/api/client'
import type { Pagina } from '../../shared/ui/Paginacion'

export type TipoContrato = 'TerminoFijo' | 'Indefinido' | 'ObraOLabor' | 'Aprendizaje' | 'MisionTemporal'
export type EtapaVinculacion = 'Oferta' | 'Documentos' | 'Afiliaciones' | 'Onboarding' | 'Vinculado' | 'Cancelado'
export type EstadoOferta = 'Redactando' | 'PorAprobar' | 'FallaIa' | 'Enviada' | 'Aceptada' | 'Rechazada'

export const tiposContrato: [TipoContrato, string][] = [
  ['Indefinido', 'Término indefinido'],
  ['TerminoFijo', 'Término fijo'],
  ['ObraOLabor', 'Obra o labor'],
  ['MisionTemporal', 'Misión en empresa usuaria'],
  ['Aprendizaje', 'Aprendizaje'],
]

export const etapas: Record<EtapaVinculacion, string> = {
  Oferta: 'Oferta', Documentos: 'Documentos', Afiliaciones: 'Afiliaciones', Onboarding: 'Onboarding', Vinculado: 'Vinculado', Cancelado: 'Cancelado',
}

export const estadosOferta: Record<EstadoOferta, { texto: string; clase: string }> = {
  Redactando: { texto: 'La IA redacta', clase: 'pill-ia' },
  PorAprobar: { texto: 'Por aprobar', clase: 'pill-alerta' },
  FallaIa: { texto: 'La IA no respondió', clase: 'pill-error' },
  Enviada: { texto: 'Esperando al candidato', clase: 'pill-neutra' },
  Aceptada: { texto: 'Aceptada', clase: 'pill-ok' },
  Rechazada: { texto: 'No aceptada', clase: 'pill-error' },
}

export const pesos = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 })

/** Fechas de la API (aaaa-mm-dd) sin correrse un día por la zona horaria. */
export function fechaCorta(iso: string | null) {
  if (!iso) return '—'
  const [a, m, d] = iso.slice(0, 10).split('-')
  return `${d}/${m}/${a}`
}

export interface Condiciones {
  tipo: TipoContrato
  tipoNombre: string
  salarioCop: number
  fechaIngreso: string
  fechaFin: string | null
  periodoPruebaDias: number
  labor: string | null
  jornada: string
  ciudad: string
  auxilioTransporte: boolean
  auxilioTransporteCop: number
}

export interface CondicionesRequest {
  tipo: TipoContrato
  salarioCop: number
  fechaIngreso: string
  fechaFin: string | null
  periodoPruebaDias: number
  labor: string | null
}

export interface Alerta { nivel: 'Bloquea' | 'Revisar' | 'Info'; texto: string; fuente: string }
export interface Razon { signo: string; texto: string; fuente: string }
export interface Clausula { titulo: string; texto: string }

export interface ContratacionSugerida {
  postulacionId: string
  nombre: string
  documento: string
  vacante: string
  codigoVacante: string
  cargo: string
  empresaUsuaria: string | null
  salarioMinimo: number
  salarioMaximo: number
  aspiracionSalarialCop: number | null
  condiciones: Condiciones
  razones: Razon[]
  alertas: Alerta[]
  procesoId: string | null
}

export interface Proceso {
  id: string
  codigo: string
  etapa: EtapaVinculacion
  persona: { nombre: string; documento: string; correo: string; celular: string; ciudad: string }
  vacanteId: string
  codigoVacante: string
  vacante: string
  cargo: string
  postulacionId: string
  candidatoId: string
  empresa: string
  empresaUsuaria: string | null
  salarioMinimoVacante: number
  salarioMaximoVacante: number
  condiciones: Condiciones
  alertas: Alerta[]
  oferta: {
    estado: EstadoOferta
    carta: string
    clausulas: Clausula[]
    razones: Razon[]
    alertasIa: string[]
    modelo: string | null
    costoUsd: number
    errorIa: string | null
    redactadaEn: string | null
    editadaPor: string | null
    aprobadaPor: string | null
    aprobadaEn: string | null
    enviadaEn: string | null
    respuesta: { acepta: boolean; nombreEscrito: string; motivo: string | null; en: string; ip: string; navegador: string; huella: string; integra: boolean } | null
  }
  aprobadaPor: string
  motivoCancelacion: string | null
  creadoEn: string
  actualizadoEn: string
  documentos: Documento[]
  afiliaciones: Afiliacion[]
  alta: DatosAlta
  induccion: PlanInduccion | null
}

export type EstadoPlanInduccion = 'Redactando' | 'PorAprobar' | 'FallaIa' | 'Aprobado'
export type EstadoTarea = 'Pendiente' | 'Hecha' | 'NoAplica'

export const estadosPlan: Record<EstadoPlanInduccion, { texto: string; clase: string }> = {
  Redactando: { texto: 'La IA propone tareas', clase: 'pill-ia' },
  PorAprobar: { texto: 'Por aprobar', clase: 'pill-alerta' },
  FallaIa: { texto: 'La IA no respondió', clase: 'pill-error' },
  Aprobado: { texto: 'En seguimiento', clase: 'pill-ok' },
}

export interface TareaInduccion {
  id: string
  titulo: string
  descripcion: string
  responsable: string
  asignado: string | null
  dia: number
  fecha: string
  obligatoria: boolean
  fuente: string
  deIa: boolean
  estado: EstadoTarea
  nota: string | null
  cerradaPor: string | null
  cerradaEn: string | null
  vencida: boolean
}

export interface PlanInduccion {
  estado: EstadoPlanInduccion
  tareas: TareaInduccion[]
  razones: Razon[]
  alertasIa: string[]
  modelo: string | null
  costoUsd: number
  errorIa: string | null
  redactadoEn: string | null
  editadoPor: string | null
  aprobadoPor: string | null
  aprobadoEn: string | null
  hechas: number
  vencidas: number
  completo: boolean
  cerradoPor: string | null
  cerradoEn: string | null
  responsables: string[]
}

export interface TareaEditable { id: string | null; titulo: string; descripcion: string; responsable: string; asignado: string | null; dia: number }

export interface TareaPublica { titulo: string; descripcion: string; responsable: string; fecha: string; estado: EstadoTarea }

export type EstadoAfiliacion = 'Pendiente' | 'Solicitada' | 'Confirmada' | 'NoAplica'

export interface Afiliacion {
  tipo: 'Eps' | 'Pension' | 'Arl' | 'CajaCompensacion'
  nombre: string
  estado: EstadoAfiliacion
  entidad: string
  sugeridaDe: string | null
  radicado: string | null
  fechaEfectiva: string | null
  claseRiesgo: number | null
  por: string | null
  en: string | null
}

export interface DatosAlta {
  area: string
  centroCosto: string
  sede: string
  banco: string
  tipoCuenta: string
  numeroCuenta: string
  sugeridosPorIa: string[]
  editadoPor: string | null
  alertas: Alerta[]
  listo: boolean
  requiereConfirmarAlertas: boolean
  claseRiesgoRazon: string
  empleadoCreadoPor: string | null
  empleadoCreadoEn: string | null
}

export type EstadoDocumento = 'Pendiente' | 'Leyendo' | 'PorRevisar' | 'Aprobado' | 'Rechazado'

export const estadosDocumento: Record<EstadoDocumento, { texto: string; clase: string }> = {
  Pendiente: { texto: 'Pendiente', clase: 'pill-neutra' },
  Leyendo: { texto: 'La IA lo lee', clase: 'pill-ia' },
  PorRevisar: { texto: 'Por revisar', clase: 'pill-alerta' },
  Aprobado: { texto: 'Aprobado', clase: 'pill-ok' },
  Rechazado: { texto: 'Rechazado', clase: 'pill-error' },
}

export interface Documento {
  tipo: string
  nombre: string
  obligatorio: boolean
  loCargaEmpresa: boolean
  estado: EstadoDocumento
  nombreArchivo: string | null
  subidoPor: string | null
  subidoEn: string | null
  versiones: number
  lectura: {
    tipoDetectado: string
    legible: boolean
    tipoCorrecto: boolean
    coincideTitular: boolean | null
    campos: { campo: string; valor: string }[]
    observaciones: string[]
    razones: Razon[]
    modelo: string | null
    error: string | null
    leidoEn: string
  } | null
  revisadoPor: string | null
  revisadoEn: string | null
  motivoRechazo: string | null
  antecedentes: 'SinAnotaciones' | 'ConAnotaciones' | null
  conceptoRequiereRevision: boolean | null
}

export interface DocumentoPublico { tipo: string; nombre: string; obligatorio: boolean; estado: EstadoDocumento; nombreArchivo: string | null; motivoRechazo: string | null }

export interface FilaProceso {
  id: string
  codigo: string
  nombre: string
  documento: string
  cargo: string
  vacante: string
  empresaUsuaria: string | null
  tipoContrato: string
  salarioCop: number
  fechaIngreso: string
  etapa: EtapaVinculacion
  estadoOferta: EstadoOferta
  actualizadoEn: string
}

export interface OfertaPublica {
  empresa: string
  empresaUsuaria: string | null
  nombre: string
  cargo: string
  estado: EstadoOferta
  carta: string
  clausulas: Clausula[]
  condiciones: Condiciones
  respondidaEn: string | null
  acepto: boolean | null
  huella: string | null
  etapa: EtapaVinculacion
  documentos: DocumentoPublico[]
  induccion: TareaPublica[]
}

export const aRequest = (c: Condiciones): CondicionesRequest => ({
  tipo: c.tipo, salarioCop: c.salarioCop, fechaIngreso: c.fechaIngreso, fechaFin: c.fechaFin, periodoPruebaDias: c.periodoPruebaDias, labor: c.labor,
})

export function useContratacionSugerida(postulacionId: string | null) {
  return useQuery({
    queryKey: ['vinculacion', 'sugerida', postulacionId],
    queryFn: ({ signal }) => apiGet<ContratacionSugerida>(`/api/v1/vinculacion/contratacion/sugerida?postulacion=${postulacionId}`, signal),
    enabled: Boolean(postulacionId),
  })
}

/** Alertas por reglas de unas condiciones mientras RRHH las ajusta. */
export function useRevisarCondiciones(postulacionId: string, condiciones: CondicionesRequest | null) {
  return useQuery({
    queryKey: ['vinculacion', 'revisar', postulacionId, condiciones],
    queryFn: () => apiSend<Alerta[]>('POST', '/api/v1/vinculacion/contratacion/revisar', { postulacionId, condiciones }),
    enabled: Boolean(condiciones && condiciones.salarioCop > 0 && condiciones.fechaIngreso),
    placeholderData: keepPreviousData,
    retry: false,
  })
}

export function useAprobarContratacion() {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (v: { postulacionId: string; condiciones: CondicionesRequest }) => apiSend<{ id: string }>('POST', '/api/v1/vinculacion/procesos', v),
    onSuccess: () => {
      cliente.invalidateQueries({ queryKey: ['vinculacion'] })
      cliente.invalidateQueries({ queryKey: ['vacantes'] })
    },
  })
}

export function useProcesos(filtro: { pagina: number; tamano: number; buscar: string; etapa: string }) {
  const q = new URLSearchParams({ pagina: String(filtro.pagina), tamano: String(filtro.tamano) })
  if (filtro.buscar) q.set('buscar', filtro.buscar)
  if (filtro.etapa) q.set('etapa', filtro.etapa)
  return useQuery({
    queryKey: ['vinculacion', 'procesos', filtro],
    queryFn: ({ signal }) => apiGet<{ procesos: Pagina<FilaProceso>; porEtapa: Record<string, number> }>(`/api/v1/vinculacion/procesos?${q}`, signal),
    placeholderData: keepPreviousData,
    refetchInterval: (x) => (x.state.data?.procesos.items.some((p) => p.estadoOferta === 'Redactando') ? 3000 : false),
  })
}

export function useProceso(id: string) {
  return useQuery({
    queryKey: ['vinculacion', 'proceso', id],
    queryFn: ({ signal }) => apiGet<Proceso>(`/api/v1/vinculacion/procesos/${id}`, signal),
    refetchInterval: (x) => (x.state.data?.oferta.estado === 'Redactando' || x.state.data?.induccion?.estado === 'Redactando' || x.state.data?.documentos.some((d) => d.estado === 'Leyendo') ? 2500
      : x.state.data?.oferta.estado === 'Enviada' ? 15_000 : false),
  })
}

function useAccionProceso<TVars, TRes = void>(fn: (v: TVars) => Promise<TRes>) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      cliente.invalidateQueries({ queryKey: ['vinculacion'] })
      cliente.invalidateQueries({ queryKey: ['bandeja'] })
    },
  })
}

export const useCambiarCondiciones = (id: string) =>
  useAccionProceso((c: CondicionesRequest) => apiSend('PUT', `/api/v1/vinculacion/procesos/${id}/condiciones`, c))
export const useRedactarOferta = (id: string) => useAccionProceso(() => apiSend('POST', `/api/v1/vinculacion/procesos/${id}/oferta/redactar`))
export const useEditarOferta = (id: string) =>
  useAccionProceso((v: { carta: string; clausulas: Clausula[] }) => apiSend('PUT', `/api/v1/vinculacion/procesos/${id}/oferta`, v))
export const useAprobarOferta = (id: string) =>
  useAccionProceso(() => apiSend<{ procesoId: string; token: string }>('POST', `/api/v1/vinculacion/procesos/${id}/oferta/aprobar`))
export const useRenovarEnlace = (id: string) =>
  useAccionProceso(() => apiSend<{ procesoId: string; token: string }>('POST', `/api/v1/vinculacion/procesos/${id}/oferta/enlace`))
export const useCancelarProceso = (id: string) =>
  useAccionProceso((motivo: string) => apiSend('POST', `/api/v1/vinculacion/procesos/${id}/cancelar`, { motivo }))

export function useOfertaPublica(token: string) {
  return useQuery({
    queryKey: ['vinculacion', 'publica', token],
    queryFn: ({ signal }) => apiGet<OfertaPublica>(`/api/v1/portal/vinculacion/${token}`, signal),
    retry: false,
    refetchInterval: (x) => (x.state.data?.documentos.some((d) => d.estado === 'Leyendo') ? 4000 : false),
  })
}

export function useResponderOferta(token: string) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (v: { acepta: boolean; nombre: string | null; documento: string | null; motivo: string | null }) =>
      apiSend('POST', `/api/v1/portal/vinculacion/${token}/respuesta`, v),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['vinculacion', 'publica', token] }),
  })
}

export const enlaceCandidato = (token: string) => `${window.location.origin}/vinculacion/${token}`

function formulario(archivo: File, extra: Record<string, string | null> = {}) {
  const f = new FormData()
  f.append('archivo', archivo)
  return { f, q: new URLSearchParams(Object.entries(extra).filter(([, v]) => v) as [string, string][]).toString() }
}

export function useSubirDocumentoCandidato(token: string) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (v: { tipo: string; archivo: File }) => apiUpload(`/api/v1/portal/vinculacion/${token}/documentos/${v.tipo}`, formulario(v.archivo).f),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['vinculacion', 'publica', token] }),
  })
}

export const useSubirDocumento = (id: string) =>
  useAccionProceso((v: { tipo: string; archivo: File; antecedentes?: string | null; concepto?: string | null; restricciones?: string | null }) => {
    const { f, q } = formulario(v.archivo, { antecedentes: v.antecedentes ?? null, concepto: v.concepto ?? null, restricciones: v.restricciones ?? null })
    return apiUpload(`/api/v1/vinculacion/procesos/${id}/documentos/${v.tipo}${q ? `?${q}` : ''}`, f)
  })
export const useAprobarDocumento = (id: string) => useAccionProceso((tipo: string) => apiSend('POST', `/api/v1/vinculacion/procesos/${id}/documentos/${tipo}/aprobar`))
export const useRechazarDocumento = (id: string) =>
  useAccionProceso((v: { tipo: string; motivo: string }) => apiSend('POST', `/api/v1/vinculacion/procesos/${id}/documentos/${v.tipo}/rechazar`, { motivo: v.motivo }))
export const useAprobarSinObservaciones = (id: string) =>
  useAccionProceso(() => apiSend<{ documentos: number }>('POST', `/api/v1/vinculacion/procesos/${id}/documentos/aprobar-sin-observaciones`))
export const useConceptoMedico = (id: string) =>
  useMutation({ mutationFn: () => apiSend<{ concepto: string; restricciones: string | null; registradoPor: string; registradoEn: string | null }>('POST', `/api/v1/vinculacion/procesos/${id}/documentos/examen-medico/concepto`) })

/** Abre un documento en otra pestaña (queda en la bitácora). */
export async function abrirDocumento(id: string, tipo: string) {
  const ruta = tipo === 'ExamenMedico' ? `/api/v1/vinculacion/procesos/${id}/documentos/examen-medico/archivo` : `/api/v1/vinculacion/procesos/${id}/documentos/${tipo}/archivo`
  const blob = await apiArchivo(ruta, {})
  window.open(URL.createObjectURL(blob), '_blank', 'noopener')
}

export const useActualizarAfiliacion = (id: string) =>
  useAccionProceso((v: { tipo: string; estado: EstadoAfiliacion; entidad: string | null; radicado: string | null; fechaEfectiva: string | null; claseRiesgo: number | null }) =>
    apiSend('PUT', `/api/v1/vinculacion/procesos/${id}/afiliaciones/${v.tipo}`, v))
export const useEditarAlta = (id: string) =>
  useAccionProceso((v: { area: string; centroCosto: string; sede: string; banco: string; tipoCuenta: string; numeroCuenta: string }) =>
    apiSend('PUT', `/api/v1/vinculacion/procesos/${id}/alta`, v))
export const useCrearEmpleado = (id: string) =>
  useAccionProceso((confirmaAlertas: boolean) => apiSend('POST', `/api/v1/vinculacion/procesos/${id}/crear-empleado`, { confirmaAlertas }))

/** Descarga el archivo de alta (CSV canónico para cualquier ERP). */
export async function descargarAlta(id: string, codigo: string) {
  const blob = await apiArchivo(`/api/v1/vinculacion/procesos/${id}/alta/archivo`, {})
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = `alta-${codigo}.csv`
  a.click()
}

export const useRedactarPlan = (id: string) => useAccionProceso(() => apiSend('POST', `/api/v1/vinculacion/procesos/${id}/induccion/redactar`))
export const useEditarPlan = (id: string) =>
  useAccionProceso((tareas: TareaEditable[]) => apiSend('PUT', `/api/v1/vinculacion/procesos/${id}/induccion`, { tareas }))
export const useAprobarPlan = (id: string) => useAccionProceso(() => apiSend('POST', `/api/v1/vinculacion/procesos/${id}/induccion/aprobar`))
export const useActualizarTarea = (id: string) =>
  useAccionProceso((v: { tareaId: string; estado: EstadoTarea; nota: string | null }) =>
    apiSend('PUT', `/api/v1/vinculacion/procesos/${id}/induccion/tareas/${v.tareaId}`, { estado: v.estado, nota: v.nota }))
export const useCerrarOnboarding = (id: string) => useAccionProceso(() => apiSend('POST', `/api/v1/vinculacion/procesos/${id}/cerrar-onboarding`))
