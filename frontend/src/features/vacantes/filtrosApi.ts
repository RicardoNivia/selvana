import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiSend } from '../../shared/api/client'
import type { Pagina } from '../../shared/ui/Paginacion'

export type ResultadoFiltros = 'SinFiltros' | 'Cumple' | 'PorRevisar' | 'NoCumple' | 'Rescatada' | 'Descartada'

export const resultados: Record<ResultadoFiltros, { texto: string; clase: string }> = {
  SinFiltros: { texto: 'Sin filtros', clase: 'pill-neutra' },
  Cumple: { texto: 'Cumple', clase: 'pill-ok' },
  PorRevisar: { texto: 'Sin evidencia', clase: 'pill-alerta' },
  NoCumple: { texto: 'No cumple', clase: 'pill-error' },
  Rescatada: { texto: 'Rescatado', clase: 'pill-neutra' },
  Descartada: { texto: 'Ya descartado', clase: 'pill-neutra' },
}

export interface OpcionFiltro { texto: string; excluyente: boolean; sugerencia: string | null }

export interface Filtros {
  configurados: boolean
  configuradoPor: string | null
  configuradoEn: string | null
  requisitos: OpcionFiltro[]
  preguntas: OpcionFiltro[]
  ciudades: string[]
  ciudadVacante: string
  experienciaMinimaMeses: number | null
  porResultado: Partial<Record<ResultadoFiltros, number>>
  sugeridos: number
}

export interface Chequeo { tipo: string; filtro: string; estado: 'Cumple' | 'NoCumple' | 'SinEvidencia'; detalle: string; evidencia: string }

export interface EvaluacionPostulante {
  postulacionId: string
  candidatoId: string
  numero: string
  nombre: string
  ciudad: string
  etapa: string
  resultado: ResultadoFiltros
  chequeos: Chequeo[]
  rescatadaPor: string | null
  descarteSugerido: boolean
}

export interface GuardarFiltros { requisitos: string[]; preguntas: string[]; ciudades: string[]; experienciaMinimaMeses: number | null }

export function useFiltros(vacanteId: string) {
  return useQuery({ queryKey: ['vacantes', 'filtros', vacanteId], queryFn: ({ signal }) => apiGet<Filtros>(`/api/v1/reclutamiento/vacantes/${vacanteId}/filtros`, signal) })
}

export function useEvaluacionFiltros(vacanteId: string, filtro: { resultado: string; pagina: number; tamano: number }) {
  const q = new URLSearchParams({ pagina: String(filtro.pagina), tamano: String(filtro.tamano) })
  if (filtro.resultado) q.set('resultado', filtro.resultado)
  return useQuery({
    queryKey: ['vacantes', 'filtros', vacanteId, 'postulantes', filtro],
    queryFn: ({ signal }) => apiGet<Pagina<EvaluacionPostulante>>(`/api/v1/reclutamiento/vacantes/${vacanteId}/filtros/postulantes?${q}`, signal),
    placeholderData: keepPreviousData,
  })
}

function useAccion<TVars, TRes>(fn: (v: TVars) => Promise<TRes>) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      cliente.invalidateQueries({ queryKey: ['vacantes'] })
      cliente.invalidateQueries({ queryKey: ['bandeja'] })
    },
  })
}

export const useGuardarFiltros = (vacanteId: string) =>
  useAccion((f: GuardarFiltros) => apiSend('PUT', `/api/v1/reclutamiento/vacantes/${vacanteId}/filtros`, f))
export const useProponerDescartes = (vacanteId: string) =>
  useAccion(() => apiSend<{ postulaciones: number }>('POST', `/api/v1/reclutamiento/vacantes/${vacanteId}/filtros/descartes`))
export const useAprobarDescartes = (vacanteId: string) =>
  useAccion((postulacionIds: string[] | null) =>
    apiSend<{ postulaciones: number }>('POST', `/api/v1/reclutamiento/vacantes/${vacanteId}/filtros/descartes/aprobar`, { postulacionIds }))
export const useRescatar = (vacanteId: string) =>
  useAccion((postulacionId: string) => apiSend('POST', `/api/v1/reclutamiento/vacantes/${vacanteId}/filtros/rescatar/${postulacionId}`))
