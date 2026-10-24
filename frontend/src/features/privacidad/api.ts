import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiSend } from '../../shared/api/client'
import type { Pagina } from '../../shared/ui/Paginacion'

export interface EntradaBitacora {
  id: string
  fecha: string
  tipoActor: 'Persona' | 'Ia' | 'Sistema' | 'Titular'
  actor: string
  accion: string
  detalle: string
  nota: string | null
  modulo: string
  datoSensible: boolean
  huella: string
}

export interface Verificacion {
  integra: boolean
  revisadas: number
  fechaAlterada: string | null
  mensaje: string
}

export interface ResumenPrivacidad {
  solicitudesAbiertas: number
  vencenEnTresDias: number
  vencidas: number
  accesosSensiblesSieteDias: number
  entradasHoy: number
}

export interface ResumenAutorizaciones {
  vigentes: number
  revocadasEsteAnio: number
  versionVigente: string
}

export interface Autorizacion {
  postulacionId: string
  titular: string
  documento: string
  canal: string
  version: string
  aceptadaEn: string
  estado: 'Vigente' | 'Revocada'
  huella: string
  vacante: string
}

export type TipoSolicitud = 'Consulta' | 'Actualizacion' | 'Supresion' | 'Revocatoria'

export interface Solicitud {
  id: string
  tipo: TipoSolicitud
  titular: string
  documento: string
  contacto: string
  canal: string
  descripcion: string
  recibidaEn: string
  vence: string
  diasHabilesRestantes: number
  plazoDiasHabiles: number
  borradorRespuesta: string
  estado: 'Abierta' | 'Respondida'
  respuesta: string | null
  respondidaPor: string | null
  respondidaEn: string | null
  esSupresion: boolean
  supresionAprobada: boolean
}

export const nombreTipo: Record<TipoSolicitud, string> = {
  Consulta: 'Consulta',
  Actualizacion: 'Actualización',
  Supresion: 'Supresión',
  Revocatoria: 'Revocatoria',
}

const q = (v: Record<string, string | number | boolean | undefined>) =>
  new URLSearchParams(Object.entries(v).filter(([, x]) => x !== undefined && x !== '' && x !== false).map(([k, x]) => [k, String(x)])).toString()

export function useResumenPrivacidad() {
  return useQuery({ queryKey: ['privacidad', 'resumen'], queryFn: ({ signal }) => apiGet<ResumenPrivacidad>('/api/v1/core/privacidad/resumen', signal) })
}

export function useResumenAutorizaciones() {
  return useQuery({ queryKey: ['privacidad', 'autorizaciones', 'resumen'], queryFn: ({ signal }) => apiGet<ResumenAutorizaciones>('/api/v1/reclutamiento/autorizaciones/resumen', signal) })
}

export function useBitacora(filtro: { pagina: number; tamano: number; tipo: string; buscar: string; sensibles: boolean }) {
  return useQuery({
    queryKey: ['privacidad', 'bitacora', filtro],
    queryFn: ({ signal }) => apiGet<Pagina<EntradaBitacora>>(`/api/v1/core/privacidad/bitacora?${q(filtro)}`, signal),
    placeholderData: keepPreviousData,
    // La bitácora recibe acciones de otros módulos por el outbox: se refresca sola
    refetchInterval: 15_000,
  })
}

export function useVerificarBitacora() {
  return useMutation({ mutationFn: () => apiGet<Verificacion>('/api/v1/core/privacidad/bitacora/verificar') })
}

export function useAutorizaciones(filtro: { pagina: number; tamano: number; buscar: string; estado: string }) {
  return useQuery({
    queryKey: ['privacidad', 'autorizaciones', filtro],
    queryFn: ({ signal }) => apiGet<Pagina<Autorizacion>>(`/api/v1/reclutamiento/autorizaciones?${q(filtro)}`, signal),
    placeholderData: keepPreviousData,
  })
}

export function useSolicitudes(filtro: { pagina: number; tamano: number; estado: string }) {
  return useQuery({
    queryKey: ['privacidad', 'solicitudes', filtro],
    queryFn: ({ signal }) => apiGet<Pagina<Solicitud>>(`/api/v1/core/privacidad/solicitudes?${q(filtro)}`, signal),
    placeholderData: keepPreviousData,
  })
}

export function useRegistrarSolicitud() {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (d: { tipo: TipoSolicitud; titular: string; documento: string; contacto: string; canal: string; descripcion: string }) =>
      apiSend<{ id: string }>('POST', '/api/v1/core/privacidad/solicitudes', d),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['privacidad'] }),
  })
}

export function useResponderSolicitud(id: string) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (d: { respuesta: string; aprobarSupresion: boolean }) => apiSend('POST', `/api/v1/core/privacidad/solicitudes/${id}/responder`, d),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['privacidad'] }),
  })
}

export function useSolicitudPortal() {
  return useMutation({
    mutationFn: (d: { tipo: TipoSolicitud; titular: string; documento: string; contacto: string; descripcion: string }) =>
      apiSend('POST', '/api/v1/portal/solicitudes-titular', d),
  })
}
