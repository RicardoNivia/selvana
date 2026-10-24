import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiSend } from '../../shared/api/client'
import type { Pagina } from '../../shared/ui/Paginacion'

export interface Propuesta {
  id: string
  modulo: string
  tipo: string
  entidadId: string
  empresaUsuariaId: string | null
  titulo: string
  descripcion: string
  agente: string | null
  modelo: string | null
  razones: { signo: string; texto: string; fuente: string }[]
  campos: { etiqueta: string; valor: string }[]
  accion: { etiqueta: string; metodo: 'POST' | 'PUT' | 'PATCH' | 'DELETE'; ruta: string } | null
  revisar: string
  afectaPersona: boolean
  venceEl: string | null
  urgente: boolean
  creadaEn: string
  estado: 'Pendiente' | 'Resuelta' | 'Descartada'
  decision: string | null
  decididaPor: string | null
  decididaEn: string | null
}

export interface ResumenBandeja {
  pendientes: number
  urgentes: number
  afectanPersonas: number
}

export interface ActividadAgente {
  agente: string
  accionesHoy: number
  ultimaAccion: string
  ultimaVez: string
  esperanAprobacion: number
}

export function useBandeja(estado: string, pagina: number, tamano: number) {
  return useQuery({
    queryKey: ['bandeja', 'lista', estado, pagina, tamano],
    queryFn: ({ signal }) => apiGet<Pagina<Propuesta>>(`/api/v1/core/bandeja?estado=${estado}&pagina=${pagina}&tamano=${tamano}`, signal),
    placeholderData: keepPreviousData,
    // Los agentes dejan propuestas en cualquier momento
    refetchInterval: 15_000,
  })
}

export function useResumenBandeja(activo = true) {
  return useQuery({
    queryKey: ['bandeja', 'resumen'],
    queryFn: ({ signal }) => apiGet<ResumenBandeja>('/api/v1/core/bandeja/resumen', signal),
    refetchInterval: 20_000,
    enabled: activo,
  })
}

export function useActividadAgentes() {
  return useQuery({ queryKey: ['bandeja', 'actividad'], queryFn: ({ signal }) => apiGet<ActividadAgente[]>('/api/v1/core/bandeja/actividad', signal), refetchInterval: 20_000 })
}

/** Ejecuta la acción principal en el módulo dueño (publicar, aprobar…). El módulo resuelve la propuesta. */
export function useDecidir() {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (p: Propuesta) => apiSend(p.accion!.metodo, p.accion!.ruta),
    onSuccess: () => {
      void cliente.invalidateQueries({ queryKey: ['bandeja'] })
      void cliente.invalidateQueries({ queryKey: ['vacantes'] })
      void cliente.invalidateQueries({ queryKey: ['estructura'] })
    },
  })
}

export function useDescartar() {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: ({ id, motivo }: { id: string; motivo: string }) => apiSend('POST', `/api/v1/core/bandeja/${id}/descartar`, { motivo }),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['bandeja'] }),
  })
}
