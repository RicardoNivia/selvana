import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiSend } from '../../shared/api/client'
import type { Pagina } from '../../shared/ui/Paginacion'

export type EstadoSugerencia = 'Sugerida' | 'Invitada' | 'NoEncaja'

export interface CoincidenciaBanco { tipo: 'Cargo' | 'Ciudad' | 'Requisito'; texto: string; evidencia: string }

export interface AfinidadBanco {
  estado: 'SinCalcular' | 'Calculando' | 'Lista' | 'Fallo'
  puntaje: number | null
  razones: { signo: string; texto: string; evidencia: string }[]
  cumplidos: string[]
  faltantes: string[]
  modelo: string | null
  mensaje: string | null
}

/** Una persona del banco que encaja con la vacante. El contacto solo llega cuando una persona decidió invitarla. */
export interface SugerenciaBanco {
  id: string
  candidatoId: string
  nombre: string
  documento: string
  ciudad: string
  puntajeReglas: number
  coincidencias: CoincidenciaBanco[]
  afinidad: AfinidadBanco | null
  estado: EstadoSugerencia
  decididaPor: string | null
  decididaEn: string | null
  motivo: string | null
  yaSePostulo: boolean
  postulacionesPrevias: number
  celular: string | null
  correo: string | null
  creadaEn: string
}

export interface InvitacionBanco { ruta: string; mensaje: string; celular: string; correo: string }

export function useSugerenciasBanco(vacanteId: string, pagina: number, tamano: number) {
  return useQuery({
    queryKey: ['vacantes', 'banco', vacanteId, pagina, tamano],
    queryFn: ({ signal }) => apiGet<Pagina<SugerenciaBanco>>(`/api/v1/reclutamiento/vacantes/${vacanteId}/banco?pagina=${pagina}&tamano=${tamano}`, signal),
    placeholderData: keepPreviousData,
    // Mientras la IA califica, se refresca para mostrar el avance
    refetchInterval: (q) => (q.state.data?.items.some((s) => s.afinidad?.estado === 'Calculando') ? 4_000 : false),
  })
}

function useAccion<TVars, TRes>(fn: (v: TVars) => Promise<TRes>) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      cliente.invalidateQueries({ queryKey: ['vacantes', 'banco'] })
      cliente.invalidateQueries({ queryKey: ['bandeja'] })
    },
  })
}

export const useBuscarEnBanco = (vacanteId: string) =>
  useAccion(() => apiSend<{ sugeridos: number }>('POST', `/api/v1/reclutamiento/vacantes/${vacanteId}/banco/buscar`))
export const useInvitarDesdeBanco = () =>
  useAccion((sugerenciaId: string) => apiSend<InvitacionBanco>('POST', `/api/v1/reclutamiento/banco/sugerencias/${sugerenciaId}/invitar`))
export const useNoEncaja = () =>
  useAccion(({ sugerenciaId, motivo }: { sugerenciaId: string; motivo: string }) =>
    apiSend('POST', `/api/v1/reclutamiento/banco/sugerencias/${sugerenciaId}/no-encaja`, { motivo }))
