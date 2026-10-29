import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiSend } from '../../shared/api/client'
import type { Pagina } from '../../shared/ui/Paginacion'
import type { Etapa } from './api'

export type Accion = 'Avanzar' | 'Descartar' | 'Invitar' | 'EnviarPruebas' | 'Esperar' | 'Revisar' | 'Contratar' | 'Ninguna'

export interface Criterio { texto: string; cumple: boolean | null; evidencia: string }

export interface Sugerencia { accion: Accion; destino: Etapa | null; titulo: string; criterios: Criterio[]; automatica: boolean }

export interface CambioEtapa { de: Etapa; a: Etapa; por: string; motivo: string | null; en: string }

export interface PersonaProceso {
  postulacionId: string
  candidatoId: string
  numero: string
  nombre: string
  ciudad: string
  etapa: Etapa
  puntaje: number | null
  estadoAfinidad: string
  ajustado: boolean
  razonPrincipal: string | null
  resultadoFiltros: 'SinFiltros' | 'Cumple' | 'NoCumple' | 'SinEvidencia' | 'Rescatada'
  motivoFiltro: string | null
  preseleccion: { estado: string; interesado: boolean | null; aspiracion: number | null; disponibilidad: string; texto: string } | null
  pruebas: { estado: string; promedio: number | null; cumpleCortes: boolean | null; terminadas: number; total: number } | null
  diasEnEtapa: number
  recibidaEn: string
  motivoDescarte: string | null
  procesoVinculacionId: string | null
  historial: CambioEtapa[]
  sugerencia: Sugerencia
}

export interface EtapaProceso { etapa: Etapa; total: number; alcanzaron: number; porDecidir: number; esperando: number }

export type ModoAvance = 'Sugerir' | 'Automatico'

export interface ReglasAvance { modo: ModoAvance; umbralAfinidad: number; toleranciaSalarial: number }

export interface Proceso {
  etapas: EtapaProceso[]
  reglas: ReglasAvance
  tieneBateria: boolean
  contratados: number
  sugerenciasListas: number
  personas: Pagina<PersonaProceso>
}

export interface Avance { etapa: Etapa; tipoEnlace: 'Preseleccion' | 'Pruebas' | null; token: string | null }

export const nombreEtapa: Record<Etapa, string> = {
  Recibida: 'Recibidas', Preseleccion: 'Preselección', Pruebas: 'Pruebas', Terna: 'Terna', Descartada: 'Descartadas',
}

/** Qué hace cada etapa, en una línea (se muestra bajo el nombre en el embudo). */
export const queHace: Record<Etapa, string> = {
  Recibida: 'La IA lee la hoja de vida y la califica',
  Preseleccion: 'Chat corto con el agente: interés, salario, disponibilidad',
  Pruebas: 'Pruebas psicotécnicas del cargo',
  Terna: 'Finalistas para la decisión de contratar',
  Descartada: 'Con motivo registrado y respeto',
}

export const enlaceDe = (a: Avance) =>
  a.token ? `${window.location.origin}/${a.tipoEnlace === 'Pruebas' ? 'pruebas' : 'preseleccion'}/${a.token}` : null

export function useProceso(vacanteId: string, filtro: { etapa: Etapa | null; porDecidir: boolean; buscar: string; pagina: number; tamano: number }) {
  const q = new URLSearchParams({ pagina: String(filtro.pagina), tamano: String(filtro.tamano), porDecidir: String(filtro.porDecidir) })
  if (filtro.etapa) q.set('etapa', filtro.etapa)
  if (filtro.buscar) q.set('buscar', filtro.buscar)
  return useQuery({
    queryKey: ['vacantes', 'proceso', vacanteId, filtro],
    queryFn: ({ signal }) => apiGet<Proceso>(`/api/v1/reclutamiento/vacantes/${vacanteId}/proceso?${q}`, signal),
    placeholderData: keepPreviousData,
    refetchInterval: 30_000,
  })
}

function useMutacionProceso<V, R = unknown>(fn: (v: V) => Promise<R>) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      cliente.invalidateQueries({ queryKey: ['vacantes'] })
      cliente.invalidateQueries({ queryKey: ['preseleccion'] })
      cliente.invalidateQueries({ queryKey: ['pruebas'] })
    },
  })
}

/** Pasa a la persona de etapa y la etapa hace su trabajo: preselección invita al chat; pruebas envía el enlace. Misma etapa = reenviar. */
export const useAvanzar = () =>
  useMutacionProceso((v: { postulacionId: string; destino: Etapa; motivo: string | null }) =>
    apiSend<Avance>('POST', `/api/v1/reclutamiento/postulaciones/${v.postulacionId}/avanzar`, { destino: v.destino, motivo: v.motivo }))

export const useAplicarSugerencias = (vacanteId: string) =>
  useMutacionProceso((postulacionIds: string[]) =>
    apiSend<{ aplicadas: number }>('POST', `/api/v1/reclutamiento/vacantes/${vacanteId}/proceso/sugerencias`, { postulacionIds }))

export const useGuardarReglas = (vacanteId: string) =>
  useMutacionProceso((r: ReglasAvance) => apiSend('PUT', `/api/v1/reclutamiento/vacantes/${vacanteId}/proceso/reglas`, r))
