import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiSend } from '../../shared/api/client'
import type { Pagina } from '../../shared/ui/Paginacion'

const base = '/api/v1/clima'

export interface Pregunta { id: string; dimension: string; texto: string }

export interface Dimension { dimension: string; promedio: number; favorables: number }

export interface Grupo { tipo: string; nombre: string; respuestas: number; promedio: number | null; enps: number | null; dimensiones: Dimension[] }

export interface Resultados {
  respuestas: number
  promedio: number | null
  enps: number | null
  promotores: number | null
  detractores: number | null
  dimensiones: Dimension[]
  grupos: Grupo[]
  gruposOcultos: number
}

export interface Tema { nombre: string; sentimiento: 'Positivo' | 'Negativo' | 'Mixto'; cantidad: number; citas: string[]; accion: string }

export interface Analisis { estado: 'Preparando' | 'Listo' | 'Fallido'; origen: 'IA' | 'Reglas'; temas: Tema[]; resumen: string; modelo: string | null; error: string | null; comentarios: number; revisadoPor: string | null; en: string }

export interface Encuesta {
  id: string
  titulo: string
  estado: 'Borrador' | 'Abierta' | 'Cerrada'
  preguntas: Pregunta[]
  incluyeEnps: boolean
  preguntaAbierta: string
  areas: string[]
  sedes: string[]
  abiertaEn: string | null
  cerradaEn: string | null
  resultados: Resultados
  comentarios: number
  analisis: Analisis | null
  creadaPor: string
  creadaEn: string
}

export interface FilaEncuesta { id: string; titulo: string; estado: Encuesta['estado']; preguntas: number; respuestas: number; abiertaEn: string | null; cerradaEn: string | null }

export interface FilaPulso {
  id: string
  empleadoId: string
  nombre: string
  cargo: string
  area: string
  hito: number
  pendienteDesde: string
  respondido: boolean
  promedio: number | null
  recomendaria: number | null
  comentario: string | null
  alerta: boolean
  seguimiento: string | null
  seguimientoPor: string | null
}

export interface Riesgo {
  empleadoId: string
  nombre: string
  cargo: string
  area: string
  sede: string
  nivel: 'Alto' | 'Medio' | 'Bajo'
  puntos: number
  senales: { texto: string; evidencia: string; puntos: number }[]
  acciones: string[]
}

export interface EncuestaPublica { empresa: string; titulo: string; preguntas: Pregunta[]; incluyeEnps: boolean; preguntaAbierta: string; areas: string[]; sedes: string[] }

export const decimal = (n: number | null | undefined) => (n == null ? '—' : n.toLocaleString('es-CO', { minimumFractionDigits: 1, maximumFractionDigits: 2 }))
export const fecha = (iso: string | null | undefined) => {
  if (!iso) return '—'
  const [a, m, d] = iso.slice(0, 10).split('-')
  return `${d}/${m}/${a}`
}

function useAccion<V, R = unknown>(fn: (v: V) => Promise<R>) {
  const cliente = useQueryClient()
  return useMutation({ mutationFn: fn, onSuccess: () => cliente.invalidateQueries({ queryKey: ['clima'] }) })
}

export function useEncuestas(pagina: number, tamano: number) {
  return useQuery({
    queryKey: ['clima', 'encuestas', pagina, tamano],
    queryFn: ({ signal }) => apiGet<Pagina<FilaEncuesta>>(`${base}/encuestas?pagina=${pagina}&tamano=${tamano}`, signal),
    placeholderData: keepPreviousData,
  })
}

export function useEncuesta(id: string) {
  return useQuery({
    queryKey: ['clima', 'encuesta', id],
    queryFn: ({ signal }) => apiGet<Encuesta>(`${base}/encuestas/${id}`, signal),
    refetchInterval: (q) => (q.state.data?.analisis?.estado === 'Preparando' ? 3000 : false),
  })
}

export function usePulsos(f: { pagina: number; tamano: number; filtro: string }) {
  return useQuery({
    queryKey: ['clima', 'pulsos', f],
    queryFn: ({ signal }) => apiGet<Pagina<FilaPulso>>(`${base}/pulsos?pagina=${f.pagina}&tamano=${f.tamano}${f.filtro ? `&filtro=${f.filtro}` : ''}`, signal),
    placeholderData: keepPreviousData,
  })
}

export function useRotacion(f: { pagina: number; tamano: number; nivel: string }) {
  return useQuery({
    queryKey: ['clima', 'rotacion', f],
    queryFn: ({ signal }) =>
      apiGet<{ personas: Pagina<Riesgo>; altos: number; medios: number; bajos: number }>(`${base}/rotacion?pagina=${f.pagina}&tamano=${f.tamano}${f.nivel ? `&nivel=${f.nivel}` : ''}`, signal),
    placeholderData: keepPreviousData,
  })
}

export const useCrearEncuesta = () => useAccion((titulo: string) => apiSend<{ id: string }>('POST', `${base}/encuestas`, { titulo }))
export const useEditarEncuesta = (id: string) =>
  useAccion((v: { titulo: string; preguntas: Pregunta[]; incluyeEnps: boolean; preguntaAbierta: string }) => apiSend('PUT', `${base}/encuestas/${id}`, v))
export const useAbrirEncuesta = (id: string) => useAccion(() => apiSend<{ ruta: string }>('POST', `${base}/encuestas/${id}/abrir`))
export const useCerrarEncuesta = (id: string) => useAccion(() => apiSend('POST', `${base}/encuestas/${id}/cerrar`))
export const useAnalizarIa = (id: string) => useAccion(() => apiSend('POST', `${base}/encuestas/${id}/analisis/ia`))
export const useAnalizarReglas = (id: string) => useAccion(() => apiSend('POST', `${base}/encuestas/${id}/analisis/reglas`))
export const useMarcarRevisado = (id: string) => useAccion(() => apiSend('POST', `${base}/encuestas/${id}/analisis/revisado`))
export const useProgramarPulsos = () => useAccion(() => apiSend<{ cantidad: number }>('POST', `${base}/pulsos/programar`))
export const useSeguimientoPulso = () => useAccion((v: { id: string; nota: string }) => apiSend('POST', `${base}/pulsos/${v.id}/seguimiento`, { nota: v.nota }))

export function useEncuestaPublica(token: string) {
  return useQuery({ queryKey: ['clima', 'publica', token], queryFn: ({ signal }) => apiGet<EncuestaPublica>(`${base}/publico/${token}`, signal), retry: false })
}

export const useResponderEncuesta = (token: string) =>
  useMutation({
    mutationFn: (v: { area: string; sede: string; puntajes: Record<string, number>; enps: number | null; comentario: string }) => apiSend('POST', `${base}/publico/${token}`, v),
  })
