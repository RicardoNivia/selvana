import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiSend } from '../../shared/api/client'
import type { Pagina } from '../../shared/ui/Paginacion'

export type FiltroRapido =
  | 'Todos' | 'ConAlertas' | 'DocumentosVencidos' | 'DocumentosPorVencer' | 'ContratoPorTerminar' | 'Afiliaciones' | 'PorEvaluar' | 'RiesgoRotacion' | 'Nuevos'

export interface Alerta { nivel: 'Vencido' | 'PorVencer' | 'Falta'; texto: string; fuente: string }

export interface Miembro {
  id: string
  codigo: string
  nombre: string
  cargo: string
  area: string
  sede: string
  empresaUsuaria: string | null
  fechaIngreso: string
  diasEnLaEmpresa: number
  fechaFinContrato: string | null
  alertas: Alerta[]
  evaluacion: { id: string; ciclo: string; hasta: string; estado: 'Pendiente' | 'EnCurso' | 'Calificada'; puntaje: number | null } | null
  riesgo: { nivel: 'Alto' | 'Medio' | 'Bajo'; puntos: number; senales: { texto: string; evidencia: string }[]; acciones: string[] } | null
}

export interface Prioridad { empleadoId: string; nombre: string; nivel: 'Urgente' | 'EstaSemana' | 'Acompanar'; texto: string; evidencia: string }

export interface Lider { id: string; nombre: string; personas: number }

export interface MiEquipo {
  lider: Lider
  puedeElegirLider: boolean
  lideres: Lider[]
  resumen: { personas: number; conAlertas: number; documentosVencidos: number; porEvaluar: number; riesgoAlto: number; contratosPorTerminar: number; nuevos: number }
  prioridades: Prioridad[]
  filtro: FiltroRapido
  entendido: string[]
  miembros: Pagina<Miembro>
}

export interface OpcionesEquipo {
  usuarios: { id: string; nombre: string; rol: string; cargo: string | null; personas: number }[]
  areas: { nombre: string; personas: number; sinLider: number }[]
}

export function useMiEquipo(f: { lider: string | null; pregunta: string; filtro: FiltroRapido; pagina: number; tamano: number }) {
  const q = new URLSearchParams({ pagina: String(f.pagina), tamano: String(f.tamano), filtro: f.filtro })
  if (f.lider) q.set('lider', f.lider)
  if (f.pregunta) q.set('pregunta', f.pregunta)
  return useQuery({
    queryKey: ['empleados', 'equipo', f],
    queryFn: ({ signal }) => apiGet<MiEquipo>(`/api/v1/expediente/equipo?${q}`, signal),
    placeholderData: keepPreviousData,
  })
}

export function useOpcionesEquipo(activo: boolean) {
  return useQuery({
    queryKey: ['empleados', 'equipo', 'opciones'],
    queryFn: ({ signal }) => apiGet<OpcionesEquipo>('/api/v1/expediente/equipo/opciones', signal),
    enabled: activo,
  })
}

/** RRHH asigna (o quita, con liderId null) el líder directo a unas personas o a toda un área. */
export function useAsignarLider() {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (v: { liderId: string | null; empleadoIds?: string[]; area?: string }) =>
      apiSend<{ personas: number }>('PUT', '/api/v1/expediente/equipo/lider', v),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['empleados'] }),
  })
}
