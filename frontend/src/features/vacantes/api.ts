import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiSend } from '../../shared/api/client'
import type { Pagina } from '../../shared/ui/Paginacion'

export type EstadoVacante = 'Redactando' | 'Borrador' | 'EnAprobacion' | 'Publicada' | 'Cerrada'

export interface VacanteResumen {
  id: string
  codigo: string
  titulo: string
  empresa: string
  ciudad: string
  cupos: number
  estado: EstadoVacante
  postulantes: number
  creadaEn: string
  publicadaEn: string | null
}

export interface Razon {
  signo: string
  texto: string
  fuente: string
}

export interface Conteo {
  nombre: string
  total: number
}

export interface DecisionRequisicion {
  etapa: string
  decision: 'Aprobada' | 'Devuelta'
  por: string
  rol: string
  motivo: string | null
  automatica: boolean
  en: string
}

/** Requisición líder → RRHH → gerencia. */
export interface AprobacionRequisicion {
  etapas: string[]
  etapaPendiente: string | null
  quienesAprueban: string[]
  pedidaPor: string | null
  motivoDevolucion: string | null
  decisiones: DecisionRequisicion[]
  puedeDecidir: boolean
}

/** Cargo del catálogo. vinculado: la vacante parte de él; si no, existe con el mismo nombre pero se redactó desde cero. */
export interface CargoCatalogo {
  id: string
  nombre: string
  area: string
  empleados: number
  estadoPerfil: 'SinPerfil' | 'BorradorIa' | 'Aprobado'
  vinculado: boolean
}

/** Pregunta de la IA antes de redactar, con respuestas cortas para elegir. */
export interface Aclaracion {
  pregunta: string
  opciones: string[]
}

export interface Vacante {
  aprobacion: AprobacionRequisicion
  cargoId: string | null
  cargoCatalogo: CargoCatalogo | null
  pruebas: { codigo: string; peso: number; corte: number | null }[]
  id: string
  codigo: string
  frase: string
  estado: EstadoVacante
  empresa: string
  empresaUsuariaId: string | null
  titulo: string
  cargo: string
  ciudad: string
  cupos: number
  jornada: string
  descripcion: string
  salarioMinimo: number
  salarioMaximo: number
  justificacionSalario: string
  requisitos: string[]
  competencias: string[]
  preguntasFiltro: string[]
  supuestos: string[]
  razones: Razon[]
  modeloIa: string | null
  mensajeIa: string | null
  editadaPorPersona: boolean
  creadaEn: string
  creadaPor: string
  publicadaEn: string | null
  postulantes: number
  canales: Conteo[]
  /** Código DANE del municipio; null si la ciudad aún no se eligió del catálogo. */
  codigoMunicipio: string | null
  /** Preguntas de la IA antes de redactar (por ejemplo, si la frase pide dos cargos). */
  aclaraciones: Aclaracion[] | null
}

/** Afinidad con la vacante: sugerencia del agente de ranking y, si existe, el ajuste de una persona. */
export interface Afinidad {
  estado: 'SinCalcular' | 'Calculando' | 'Lista' | 'Fallo'
  puntaje: number | null
  puntajeIa: number | null
  razones: Razon[]
  cumplidos: string[]
  faltantes: string[]
  modelo: string | null
  mensaje: string | null
  calculadaEn: string | null
  ajustado: boolean
  motivoAjuste: string | null
  ajustadoPor: string | null
  razonesRetiradas: number
}

export interface Postulante {
  afinidad: Afinidad | null
  retroalimentacion: 'Bueno' | 'NoEncaja' | null
  notaRetroalimentacion: string | null
  postulacionId: string
  candidatoId: string
  numero: string
  nombre: string
  documento: string
  celular: string
  ciudad: string
  canal: string
  respuestas: string
  respuestasNegativas: number
  autorizacion: string
  recibidaEn: string
  otrasPostulaciones: number
  camposCorregidos: string[]
  linkedIn: string
}

export interface EmpresaActual {
  id: string
  nombre: string
  esTemporal: boolean
  empresasUsuarias: { id: string; nombre: string; ciudad: string; activa: boolean }[]
}

export interface ContenidoVacante {
  titulo: string
  cargo: string
  ciudad: string
  cupos: number
  jornada: string
  descripcion: string
  salarioMinimo: number
  salarioMaximo: number
  requisitos: string[]
  preguntasFiltro: string[]
  codigoMunicipio?: string | null
}

function query(params: Record<string, string | number | undefined>) {
  const q = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== '') q.set(k, String(v))
  return q.toString()
}

export function useEmpresaActual() {
  return useQuery({ queryKey: ['core', 'empresa'], queryFn: ({ signal }) => apiGet<EmpresaActual>('/api/v1/core/empresa', signal), staleTime: 5 * 60_000 })
}

export function useVacantes(filtro: { pagina: number; tamano?: number; buscar: string; estado: string; empresaUsuariaId: string }) {
  return useQuery({
    queryKey: ['vacantes', 'lista', filtro],
    queryFn: ({ signal }) =>
      apiGet<Pagina<VacanteResumen>>(`/api/v1/reclutamiento/vacantes?${query({ pagina: filtro.pagina, tamano: filtro.tamano, buscar: filtro.buscar, estado: filtro.estado, empresaUsuariaId: filtro.empresaUsuariaId })}`, signal),
    placeholderData: keepPreviousData,
    refetchInterval: (q) => (q.state.data?.items.some((v) => v.estado === 'Redactando') ? 3000 : false),
  })
}

export function useVacante(id: string, activo = true) {
  return useQuery({
    enabled: activo && Boolean(id),
    queryKey: ['vacantes', 'detalle', id],
    queryFn: ({ signal }) => apiGet<Vacante>(`/api/v1/reclutamiento/vacantes/${id}`, signal),
    // Mientras el agente redacta, se consulta seguido para mostrar el borrador apenas llegue
    refetchInterval: (q) => (q.state.data?.estado === 'Redactando' ? 2000 : 30_000),
  })
}

export function usePostulantes(vacanteId: string, filtro: { pagina: number; tamano?: number; buscar: string; canal: string }) {
  return useQuery({
    queryKey: ['vacantes', 'postulantes', vacanteId, filtro],
    queryFn: ({ signal }) =>
      apiGet<Pagina<Postulante>>(`/api/v1/reclutamiento/vacantes/${vacanteId}/postulantes?${query({ pagina: filtro.pagina, tamano: filtro.tamano, buscar: filtro.buscar, canal: filtro.canal })}`, signal),
    placeholderData: keepPreviousData,
    // Mientras la IA califica, se refresca seguido para mostrar los puntajes apenas llegan
    refetchInterval: (q) => (q.state.data?.items.some((p) => p.afinidad?.estado === 'Calculando') ? 4_000 : 30_000),
  })
}

export type Etapa = 'Recibida' | 'Preseleccion' | 'Pruebas' | 'Terna' | 'Descartada'

export interface Tarjeta {
  postulacionId: string
  candidatoId: string
  numero: string
  nombre: string
  ciudad: string
  puntaje: number | null
  estadoAfinidad: string
  ajustado: boolean
  razonPrincipal: string | null
  respuestasNegativas: number
  recibidaEn: string
  motivoDescarte: string | null
  procesoVinculacionId: string | null
  resultadoFiltros: 'SinFiltros' | 'Cumple' | 'PorRevisar' | 'NoCumple' | 'Rescatada'
  motivoFiltro: string | null
}

export interface Tablero {
  columnas: { etapa: Etapa; total: number; tarjetas: Tarjeta[] }[]
}

/** Tablero de la vacante: cada columna trae su total y hasta «porColumna» tarjetas, ordenadas por afinidad. */
export function useTablero(vacanteId: string, porColumna: number) {
  return useQuery({
    queryKey: ['vacantes', 'tablero', vacanteId, porColumna],
    queryFn: ({ signal }) => apiGet<Tablero>(`/api/v1/reclutamiento/vacantes/${vacanteId}/tablero?porColumna=${porColumna}`, signal),
    placeholderData: keepPreviousData,
    refetchInterval: (q) => (q.state.data?.columnas.some((c) => c.tarjetas.some((t) => t.estadoAfinidad === 'Calculando')) ? 4_000 : 30_000),
  })
}

export const useMoverPostulacion = () =>
  useMutacionVacantes((v: { postulacionId: string; etapa: Etapa; motivo: string | null }) =>
    apiSend('PUT', `/api/v1/reclutamiento/postulaciones/${v.postulacionId}/etapa`, { etapa: v.etapa, motivo: v.motivo }))

/** El agente arma la terna con los mejores puntajes y la deja en la bandeja para aprobación. */
export const useProponerTerna = (vacanteId: string) =>
  useMutacionVacantes(() => apiSend<{ candidatos: number }>('POST', `/api/v1/reclutamiento/vacantes/${vacanteId}/terna/proponer`))

export interface Finalista {
  postulacionId: string
  candidatoId: string
  nombre: string
  ciudad: string
  etapa: string
  puntaje: number | null
  puntajeIa: number | null
  ajustado: boolean
  aFavor: Razon[]
  enContra: Razon[]
  cumplidos: string[]
  faltantes: string[]
  retroalimentacion: 'Bueno' | 'NoEncaja' | null
  notaRetroalimentacion: string | null
  hojaDeVida: { ultimoCargo: string; mesesExperiencia: number; formacion: string; habilidades: string[]; alertas: string[]; posibleDuplicado: boolean }
  preseleccion: { estado: string; interesado: boolean | null; aspiracionSalarialCop: number | null; aspiracionEnRango: boolean | null; disponibilidad: string; ubicacion: string; dudas: number; resumen: string } | null
  pruebas: { estado: string; total: number | null; provisional: boolean; pruebas: { nombre: string; estado: string; puntaje: number | null; corte: number | null; pasaCorte: boolean | null; sensible: boolean; validada: boolean }[] } | null
}

export interface ComparativoFinalistas {
  vacante: string
  codigo: string
  cupos: number
  salarioMinimo: number
  salarioMaximo: number
  finalistas: Finalista[]
  disponibles: { postulacionId: string; nombre: string; puntaje: number | null; etapa: string }[]
}

/** Comparativo de finalistas; sin elegidos, la terna, luego pruebas y luego los de mayor afinidad (hasta 4). */
export function useComparativoFinalistas(vacanteId: string, elegidos: string[] | null) {
  return useQuery({
    queryKey: ['vacantes', 'finalistas', vacanteId, elegidos],
    queryFn: ({ signal }) => apiGet<ComparativoFinalistas>(`/api/v1/reclutamiento/vacantes/${vacanteId}/finalistas${elegidos ? `?postulaciones=${elegidos.join(',')}` : ''}`, signal),
    placeholderData: keepPreviousData,
  })
}

/** «Buen candidato» o «No encaja»: calibra el ranking de la empresa. */
export const useRetroalimentar = () =>
  useMutacionVacantes((v: { postulacionId: string; valor: 'Bueno' | 'NoEncaja'; nota: string | null }) =>
    apiSend('PUT', `/api/v1/reclutamiento/postulaciones/${v.postulacionId}/retroalimentacion`, { valor: v.valor, nota: v.nota }))

export interface GrupoSesgo {
  grupo: string
  calificados: number
  promedio: number
}

export interface ReporteSesgo {
  postulaciones: number
  calificadas: number
  promedio: number | null
  noVeElAgente: string[]
  razonesRetiradas: number
  porCiudad: GrupoSesgo[]
  brechaCiudad: number | null
  porCanal: GrupoSesgo[]
  brechaCanal: number | null
  ajustes: number
  diferenciaPromedioAjuste: number | null
  buenos: number
  noEncajan: number
  descartadas: number
  enTerna: number
  aceptacionTerna: number | null
  observaciones: string[]
  descartesPorFiltro: GrupoSesgo[] | null
}

export function useReporteSesgo(vacanteId: string) {
  return useQuery({ queryKey: ['vacantes', 'sesgo', vacanteId], queryFn: ({ signal }) => apiGet<ReporteSesgo>(`/api/v1/reclutamiento/vacantes/${vacanteId}/sesgo`, signal) })
}

/** Pide a la IA calificar las postulaciones sin puntaje (o todas, menos las ajustadas por una persona). */
export const useCalcularAfinidad = (vacanteId: string) =>
  useMutacionVacantes((todas: boolean) => apiSend<{ pedidas: number }>('POST', `/api/v1/reclutamiento/vacantes/${vacanteId}/afinidad?todas=${todas}`))

export const useAjustarAfinidad = () =>
  useMutacionVacantes((v: { postulacionId: string; puntaje: number; motivo: string }) =>
    apiSend('PUT', `/api/v1/reclutamiento/postulaciones/${v.postulacionId}/afinidad`, { puntaje: v.puntaje, motivo: v.motivo }))

function useMutacionVacantes<V, R = unknown>(fn: (v: V) => Promise<R>) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      cliente.invalidateQueries({ queryKey: ['vacantes'] })
      cliente.invalidateQueries({ queryKey: ['bandeja'] })
    },
  })
}

export const useSolicitarVacante = () =>
  useMutacionVacantes((v: { frase: string; empresaUsuariaId: string | null; cargoId: string | null }) => apiSend<{ id: string }>('POST', '/api/v1/reclutamiento/vacantes', v))

export const useCrearVacante = () =>
  useMutacionVacantes((v: ContenidoVacante & { empresaUsuariaId: string | null; cargoId: string | null }) =>
    apiSend<{ id: string }>('POST', '/api/v1/reclutamiento/vacantes/manual', v))

/** Otro borrador de la IA con una frase nueva (por ejemplo, la respuesta a lo que preguntó). */
export const useRedactarVacante = () =>
  useMutacionVacantes((v: { id: string; frase: string }) => apiSend('POST', `/api/v1/reclutamiento/vacantes/${v.id}/redactar`, { frase: v.frase }))

export const useEditarVacante = (id: string) =>
  useMutacionVacantes((v: ContenidoVacante) => apiSend('PUT', `/api/v1/reclutamiento/vacantes/${id}`, v))

/** Envía la requisición a aprobación (RRHH → gerencia); la última aprobación la publica. */
export const useSolicitarAprobacion = (id: string) => useMutacionVacantes(() => apiSend('POST', `/api/v1/reclutamiento/vacantes/${id}/solicitar-aprobacion`))

export const useAprobarRequisicion = (id: string) => useMutacionVacantes(() => apiSend('POST', `/api/v1/reclutamiento/vacantes/${id}/aprobar`))

export const useDevolverRequisicion = (id: string) =>
  useMutacionVacantes((motivo: string) => apiSend('POST', `/api/v1/reclutamiento/vacantes/${id}/devolver`, { motivo }))

/** Cargos del catálogo para elegir de cuál parte la vacante; primero los de perfil aprobado. */
export function useCargosCatalogo(buscar: string, activo: boolean) {
  return useQuery({
    queryKey: ['vacantes', 'cargos', buscar],
    queryFn: ({ signal }) => apiGet<CargoCatalogo[]>(`/api/v1/reclutamiento/cargos?buscar=${encodeURIComponent(buscar)}`, signal),
    enabled: activo,
    placeholderData: keepPreviousData,
    staleTime: 60_000,
  })
}

export const useCerrarVacante = (id: string) => useMutacionVacantes(() => apiSend('POST', `/api/v1/reclutamiento/vacantes/${id}/cerrar`))

/** Texto y estilo de cada estado de la vacante. */
export const estadoVacante: Record<EstadoVacante, { texto: string; clase: string }> = {
  Redactando: { texto: 'IA redactando', clase: 'pill-ia' },
  Borrador: { texto: 'Borrador', clase: 'pill-neutra' },
  EnAprobacion: { texto: 'En aprobación', clase: 'pill-alerta' },
  Publicada: { texto: 'Publicada', clase: 'pill-ok' },
  Cerrada: { texto: 'Cerrada', clase: 'pill-neutra' },
}
