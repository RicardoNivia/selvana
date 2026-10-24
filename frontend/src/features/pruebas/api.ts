import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiSend } from '../../shared/api/client'

export interface PruebaEnBateria {
  codigo: string
  peso: number
  corte: number | null
}

export interface PruebaSuite {
  codigo: string
  version: string
  nombre: string
  mide: string
  tipo: 'Aptitud' | 'JuicioSituacional' | 'Personalidad'
  minutos: number
  preguntas: number
  validada: boolean
  sensible: boolean
}

export interface Bateria {
  cargo: string
  pruebas: PruebaEnBateria[]
  esSugerida: boolean
  razon: string | null
  actualizadaPor: string | null
  actualizadaEn: string | null
}

export interface EnvioPruebas {
  postulacionId: string
  titular: string
  token: string
  venceEn: string
}

export interface ResultadoPrueba {
  codigo: string
  nombre: string
  estado: 'Pendiente' | 'EnCurso' | 'Terminada'
  puntaje: number | null
  corte: number | null
  pasaCorte: boolean | null
  peso: number
  sensible: boolean
  validada: boolean
  alertas: string[]
}

export interface ResultadoPostulante {
  aplicacionId: string
  postulacionId: string
  titular: string
  estado: 'Enviada' | 'EnCurso' | 'Terminada' | 'Rechazada'
  enviadaEn: string
  venceEn: string
  vencida: boolean
  total: number | null
  provisional: boolean
  pruebas: ResultadoPrueba[]
}

export interface ResultadoSensible {
  codigo: string
  nombre: string
  dimensiones: { nombre: string; puntaje: number }[]
  nota: string
}

export interface AplicacionPublica {
  nombre: string
  empresa: string
  vacante: string
  estado: 'Enviada' | 'EnCurso' | 'Terminada' | 'Rechazada'
  venceEn: string
  vencida: boolean
  consentimientoVersion: string
  consentimientoTexto: string
  consentimientoAceptado: boolean
  pruebas: { codigo: string; nombre: string; mide: string; instrucciones: string; minutos: number; preguntas: number; estado: 'Pendiente' | 'EnCurso' | 'Terminada' }[]
}

export interface PreguntasPrueba {
  codigo: string
  nombre: string
  instrucciones: string
  tipo: PruebaSuite['tipo']
  segundosRestantes: number
  preguntas: { id: string; texto: string; opciones: { id: string; texto: string }[] }[]
}

export const enlacePruebas = (token: string) => `${window.location.origin}/pruebas/${token}`

export function useSuite() {
  return useQuery({ queryKey: ['pruebas', 'suite'], queryFn: ({ signal }) => apiGet<PruebaSuite[]>('/api/v1/reclutamiento/pruebas/suite', signal), staleTime: Infinity })
}

export function useBateriaCargo(cargo: string) {
  return useQuery({
    queryKey: ['pruebas', 'bateria', cargo],
    queryFn: ({ signal }) => apiGet<Bateria>(`/api/v1/reclutamiento/pruebas/baterias?cargo=${encodeURIComponent(cargo)}`, signal),
    enabled: Boolean(cargo),
  })
}

export function useGuardarBateriaCargo() {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (d: { cargo: string; pruebas: PruebaEnBateria[] }) => apiSend('PUT', '/api/v1/reclutamiento/pruebas/baterias', d),
    onSuccess: (_, d) => cliente.invalidateQueries({ queryKey: ['pruebas', 'bateria', d.cargo] }),
  })
}

export function useGuardarBateriaVacante(vacanteId: string) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (pruebas: PruebaEnBateria[]) => apiSend('PUT', `/api/v1/reclutamiento/vacantes/${vacanteId}/pruebas`, { pruebas }),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['vacantes', 'detalle', vacanteId] }),
  })
}

export function useEnviarPruebas(vacanteId: string) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (postulacionIds: string[]) => apiSend<EnvioPruebas[]>('POST', `/api/v1/reclutamiento/vacantes/${vacanteId}/pruebas/enviar`, { postulacionIds }),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['pruebas', 'resultados', vacanteId] }),
  })
}

export function useResultadosPruebas(vacanteId: string, activo: boolean) {
  return useQuery({
    queryKey: ['pruebas', 'resultados', vacanteId],
    queryFn: ({ signal }) => apiGet<ResultadoPostulante[]>(`/api/v1/reclutamiento/vacantes/${vacanteId}/pruebas/resultados`, signal),
    enabled: activo,
    // Los candidatos van presentando: se refresca sola
    refetchInterval: 20_000,
  })
}

export function useVerResultadoSensible() {
  return useMutation({
    mutationFn: ({ aplicacionId, codigo }: { aplicacionId: string; codigo: string }) =>
      apiSend<ResultadoSensible>('POST', `/api/v1/reclutamiento/pruebas/aplicaciones/${aplicacionId}/sensible/${codigo}`),
  })
}

// ---------- Candidato ----------

export function useAplicacion(token: string) {
  return useQuery({ queryKey: ['pruebas', 'candidato', token], queryFn: ({ signal }) => apiGet<AplicacionPublica>(`/api/v1/portal/pruebas/${token}`, signal), retry: false })
}

export function useConsentimientoPruebas(token: string) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (acepta: boolean) => apiSend('POST', `/api/v1/portal/pruebas/${token}/consentimiento`, { acepta }),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['pruebas', 'candidato', token] }),
  })
}

export function useIniciarPrueba(token: string) {
  return useMutation({ mutationFn: (codigo: string) => apiSend<PreguntasPrueba>('POST', `/api/v1/portal/pruebas/${token}/${codigo}/iniciar`) })
}

export function useTerminarPrueba(token: string) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (d: { codigo: string; respuestas: { preguntaId: string; opcionId: string; milisegundos: number }[]; cambiosDePestana: number }) =>
      apiSend('POST', `/api/v1/portal/pruebas/${token}/${d.codigo}/terminar`, { respuestas: d.respuestas, cambiosDePestana: d.cambiosDePestana }),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['pruebas', 'candidato', token] }),
  })
}
