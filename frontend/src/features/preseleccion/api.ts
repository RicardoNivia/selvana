import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiSend } from '../../shared/api/client'

export type EstadoConversacion = 'Invitada' | 'Abierta' | 'Completada' | 'Escalada'

export interface Mensaje {
  autor: 'Agente' | 'Candidato' | 'Persona'
  texto: string
  por: string | null
  en: string
}

export interface ConversacionPublica {
  vacante: string
  empresa: string
  ciudad: string
  estado: EstadoConversacion
  pensando: boolean
  mensajes: Mensaje[]
}

export interface ResumenPreseleccion {
  interesado: boolean | null
  aspiracionSalarialCop: number | null
  disponibilidad: string
  ubicacion: string
  respuestas: { pregunta: string; respuesta: string }[]
  dudas: string[]
  texto: string
}

export interface Conversacion {
  id: string
  estado: EstadoConversacion
  pensando: boolean
  mensajes: Mensaje[]
  resumen: ResumenPreseleccion
  motivoEscalamiento: string | null
  modelo: string | null
  invitadaPor: string
  creadaEn: string
  actualizadaEn: string
}

/** El candidato ve su conversación; mientras el agente responde, se consulta seguido (dentro del límite del portal público). */
export function useConversacionPublica(token: string) {
  return useQuery({
    queryKey: ['preseleccion', 'publica', token],
    queryFn: ({ signal }) => apiGet<ConversacionPublica>(`/api/v1/portal/preseleccion/${token}`, signal),
    refetchInterval: (q) => (q.state.data?.pensando ? 2_000 : q.state.data?.estado === 'Escalada' ? 10_000 : false),
    retry: false,
  })
}

export function useEscribir(token: string) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (texto: string) => apiSend('POST', `/api/v1/portal/preseleccion/${token}/mensajes`, { texto }),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['preseleccion', 'publica', token] }),
  })
}

/** En la consola: la conversación de una postulación (404 si aún no la invitaron). */
export function useConversacion(postulacionId: string) {
  return useQuery({
    queryKey: ['preseleccion', postulacionId],
    queryFn: ({ signal }) => apiGet<Conversacion>(`/api/v1/reclutamiento/postulaciones/${postulacionId}/preseleccion`, signal),
    retry: false,
    refetchInterval: (q) => (q.state.data?.pensando ? 3_000 : q.state.data && q.state.data.estado !== 'Completada' ? 15_000 : false),
  })
}

export function useInvitar(postulacionId: string) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: () => apiSend<{ conversacionId: string; token: string }>('POST', `/api/v1/reclutamiento/postulaciones/${postulacionId}/preseleccion`),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['preseleccion', postulacionId] }),
  })
}

export function useResponder(postulacionId: string) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (v: { conversacionId: string; texto: string; cerrar: boolean }) =>
      apiSend('POST', `/api/v1/reclutamiento/preseleccion/${v.conversacionId}/mensajes`, { texto: v.texto, cerrar: v.cerrar }),
    onSuccess: () => {
      cliente.invalidateQueries({ queryKey: ['preseleccion', postulacionId] })
      cliente.invalidateQueries({ queryKey: ['bandeja'] })
    },
  })
}
