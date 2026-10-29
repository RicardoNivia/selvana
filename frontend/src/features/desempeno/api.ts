import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiSend } from '../../shared/api/client'
import type { Pagina } from '../../shared/ui/Paginacion'

const base = '/api/v1/desempeno'

export interface Razon { signo: string; texto: string; fuente: string }

export interface Competencia { nombre: string; descripcion: string; peso: number }

export interface ObjetivoBase { descripcion: string; meta: string; peso: number }

export interface Formulario {
  cargo: string
  personas: number
  competencias: Competencia[]
  objetivos: ObjetivoBase[]
  origen: string
  razones: Razon[]
  modelo: string | null
  preparando: boolean
  error: string | null
  aprobadoPor: string | null
  aprobadoEn: string | null
}

export interface PersonaMatriz { evaluacionId: string; nombre: string; cargo: string; puntaje: number; calibrada: boolean }

export interface Ciclo {
  id: string
  nombre: string
  tipo: TipoCiclo
  alcance: Alcance
  desde: string
  hasta: string
  pesoObjetivos: number
  estado: EstadoCiclo
  formularios: Formulario[]
  conteo: Record<string, number>
  sesgos: { tipo: string; texto: string; evidencia: string }[]
  matriz: { caja: number; nombre: string; personas: PersonaMatriz[] }[]
  promedio: number | null
  creadoPor: string
  creadoEn: string
}

export type TipoCiclo = 'Anual' | 'Semestral' | 'Trimestral' | 'PeriodoPrueba' | 'FinMision'
export type Alcance = 'Grados90' | 'Grados180' | 'Grados360'
export type EstadoCiclo = 'Borrador' | 'Abierto' | 'Calibracion' | 'Cerrado'

export const nombreTipo: Record<TipoCiclo, string> = {
  Anual: 'Anual', Semestral: 'Semestral', Trimestral: 'Trimestral', PeriodoPrueba: 'Periodo de prueba', FinMision: 'Fin de misión',
}
export const nombreAlcance: Record<Alcance, string> = { Grados90: '90° · líder', Grados180: '180° · líder y autoevaluación', Grados360: '360° · con pares' }
export const nombreEstadoCiclo: Record<EstadoCiclo, string> = { Borrador: 'Preparando', Abierto: 'Evaluando', Calibracion: 'Calibración', Cerrado: 'Cerrado' }

export interface FilaCiclo {
  id: string
  nombre: string
  tipo: TipoCiclo
  alcance: Alcance
  desde: string
  hasta: string
  estado: EstadoCiclo
  formularios: number
  formulariosAprobados: number
  evaluaciones: number
  calificadas: number
}

export interface FilaEvaluacion {
  id: string
  empleadoId: string
  codigo: string
  nombre: string
  cargo: string
  area: string
  sede: string
  estado: 'Pendiente' | 'EnCurso' | 'Calificada'
  autoevaluada: boolean
  puntaje: number | null
  potencial: Potencial | null
  caja: number | null
  calibrada: boolean
  calificadaPor: string | null
}

export type Potencial = 'Bajo' | 'Medio' | 'Alto'

export interface Objetivo { id: string; descripcion: string; meta: string; peso: number; avance: number; evidencias: { texto: string; fecha: string; por: string }[] }

export interface Calificacion { nombre: string; descripcion: string; peso: number; lider: number | null; auto: number | null; promedioPares: number | null; pares: number }

export interface Borrador { estado: 'Preparando' | 'Listo' | 'Fallido' | 'Usado'; fortalezas: string[]; oportunidades: string[]; comentario: string; razones: Razon[]; modelo: string | null; error: string | null }

export interface AccionPlan { id: string; competencia: string; accion: string; fecha: string; cumplida: boolean; origen: string }

export interface Evaluacion {
  id: string
  cicloId: string
  ciclo: string
  estadoCiclo: EstadoCiclo
  alcance: Alcance
  pesoObjetivos: number
  empleadoId: string
  codigo: string
  nombre: string
  cargo: string
  area: string
  sede: string
  estado: FilaEvaluacion['estado']
  objetivos: Objetivo[]
  competencias: Calificacion[]
  comentarioLider: string
  comentarioAuto: string
  autoevaluadaEn: string | null
  calificadaPor: string | null
  calificadaEn: string | null
  borrador: Borrador | null
  potencial: Potencial | null
  puntaje: number | null
  puntajeEnCurso: number
  caja: number | null
  nombreCaja: string | null
  ajuste: { caja: number; motivo: string; por: string; en: string } | null
  plan: AccionPlan[]
}

export const nombresCaja = [
  'Bajo potencial · bajo desempeño', 'Bajo potencial · desempeño medio', 'Bajo potencial · alto desempeño',
  'Potencial medio · bajo desempeño', 'Núcleo del equipo', 'Potencial medio · alto desempeño',
  'Alto potencial · bajo desempeño', 'Alto potencial · desempeño medio', 'Estrella',
]

export const fecha = (iso: string | null | undefined) => {
  if (!iso) return '—'
  const [a, m, d] = iso.slice(0, 10).split('-')
  return `${d}/${m}/${a}`
}

export const decimal = (n: number | null | undefined) => (n == null ? '—' : n.toLocaleString('es-CO', { minimumFractionDigits: 1, maximumFractionDigits: 2 }))

function useAccion<V, R = unknown>(fn: (v: V) => Promise<R>) {
  const cliente = useQueryClient()
  return useMutation({ mutationFn: fn, onSuccess: () => cliente.invalidateQueries({ queryKey: ['desempeno'] }) })
}

export function useCiclos(pagina: number, tamano: number) {
  return useQuery({
    queryKey: ['desempeno', 'ciclos', pagina, tamano],
    queryFn: ({ signal }) => apiGet<Pagina<FilaCiclo>>(`${base}/ciclos?pagina=${pagina}&tamano=${tamano}`, signal),
    placeholderData: keepPreviousData,
  })
}

export function useCiclo(id: string) {
  return useQuery({
    queryKey: ['desempeno', 'ciclo', id],
    queryFn: ({ signal }) => apiGet<Ciclo>(`${base}/ciclos/${id}`, signal),
    // Mientras la IA prepara formularios, se consulta seguido
    refetchInterval: (q) => (q.state.data?.formularios.some((f) => f.preparando) ? 3000 : false),
  })
}

export function useEvaluaciones(cicloId: string, f: { pagina: number; tamano: number; buscar: string; estado: string }) {
  const q = new URLSearchParams({ pagina: String(f.pagina), tamano: String(f.tamano) })
  if (f.buscar) q.set('buscar', f.buscar)
  if (f.estado) q.set('estado', f.estado)
  return useQuery({
    queryKey: ['desempeno', 'evaluaciones', cicloId, f],
    queryFn: ({ signal }) => apiGet<Pagina<FilaEvaluacion>>(`${base}/ciclos/${cicloId}/evaluaciones?${q}`, signal),
    placeholderData: keepPreviousData,
  })
}

export function useEvaluacion(id: string) {
  return useQuery({
    queryKey: ['desempeno', 'evaluacion', id],
    queryFn: ({ signal }) => apiGet<Evaluacion>(`${base}/evaluaciones/${id}`, signal),
    refetchInterval: (q) => (q.state.data?.borrador?.estado === 'Preparando' ? 2500 : false),
  })
}

export interface NuevoCiclo { nombre: string; tipo: TipoCiclo; alcance: Alcance; desde: string; hasta: string; pesoObjetivos: number }

export const useCrearCiclo = () => useAccion((v: NuevoCiclo) => apiSend<{ id: string }>('POST', `${base}/ciclos`, v))
export const usePedirFormularioIa = (id: string) => useAccion((cargo: string | null) => apiSend<{ cantidad: number }>('POST', `${base}/ciclos/${id}/formularios/ia`, { cargo }))
export const useUsarPlantilla = (id: string) => useAccion((cargo: string | null) => apiSend<{ cantidad: number }>('POST', `${base}/ciclos/${id}/formularios/plantilla`, { cargo }))
export const useGuardarFormulario = (id: string) =>
  useAccion((v: { cargo: string; competencias: Competencia[]; objetivos: ObjetivoBase[] }) => apiSend('PUT', `${base}/ciclos/${id}/formularios`, v))
export const useAprobarFormulario = (id: string) => useAccion((cargo: string) => apiSend('POST', `${base}/ciclos/${id}/formularios/aprobar`, { cargo }))
export const useAbrirCiclo = (id: string) => useAccion(() => apiSend<{ cantidad: number }>('POST', `${base}/ciclos/${id}/abrir`))
export const usePasarCalibracion = (id: string) => useAccion(() => apiSend('POST', `${base}/ciclos/${id}/calibracion`))
export const useCerrarCiclo = (id: string) => useAccion(() => apiSend('POST', `${base}/ciclos/${id}/cerrar`))

const ev = (id: string) => `${base}/evaluaciones/${id}`
export const useDefinirObjetivos = (id: string) =>
  useAccion((objetivos: { id: string | null; descripcion: string; meta: string; peso: number }[]) => apiSend('PUT', `${ev(id)}/objetivos`, { objetivos }))
export const useRegistrarAvance = (id: string) =>
  useAccion((v: { objetivoId: string; avance: number; evidencia?: string }) => apiSend('POST', `${ev(id)}/avance`, v))
export const useRegistrarPar = (id: string) => useAccion((calificaciones: Record<string, number>) => apiSend('POST', `${ev(id)}/pares`, { calificaciones }))
export const usePedirBorrador = (id: string) => useAccion(() => apiSend('POST', `${ev(id)}/borrador`))
export const useUsarBorrador = (id: string) => useAccion(() => apiSend('POST', `${ev(id)}/borrador/usar`))
export const useCalificar = (id: string) =>
  useAccion((v: { calificaciones: Record<string, number>; comentario: string; potencial: Potencial }) => apiSend('POST', `${ev(id)}/calificar`, v))
export const useReabrir = (id: string) => useAccion(() => apiSend('POST', `${ev(id)}/reabrir`))
export const useCalibrar = (id: string) => useAccion((v: { caja: number; motivo: string }) => apiSend('POST', `${ev(id)}/calibrar`, v))
export const useDefinirPlan = (id: string) =>
  useAccion((acciones: { id: string | null; competencia: string; accion: string; fecha: string; cumplida: boolean }[]) => apiSend('PUT', `${ev(id)}/plan`, { acciones }))
