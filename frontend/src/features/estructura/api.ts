import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiSend, apiUpload } from '../../shared/api/client'
import type { Pagina } from '../../shared/ui/Paginacion'

export type EstadoImportacion = 'OrganizandoConIa' | 'ListaParaRevisar' | 'Aplicada' | 'Descartada'

export interface Propuesta {
  id: string
  tipo: 'UnirCargos' | 'AsignarSede'
  origen: 'Ia' | 'Regla'
  titulo: string
  porque: string
  evidencia: string
  incluida: boolean
}

export interface Importacion {
  id: string
  fuente: string
  leidaEn: string
  estado: EstadoImportacion
  conteos: { sedes: number; areas: number; centrosCosto: number; cargos: number; cargosTrasUnir: number; empleados: number; nombresSedes: string[] }
  propuestas: Propuesta[]
  errores: string[]
  mensajeIa: string | null
  modeloIa: string | null
  aplicadaEn: string | null
  aplicadaPor: string | null
}

export interface Razon {
  signo: string
  texto: string
  fuente: string
}

export interface Perfil {
  mision: string
  funciones: string[]
  requisitos: string[]
  riesgoArl: string
}

export interface Cargo {
  id: string
  nombre: string
  area: string
  empleados: number
  codigosNomina: string[]
  nombresNomina: string[]
  estadoPerfil: 'SinPerfil' | 'BorradorIa' | 'Aprobado'
  perfil: Perfil | null
  razones: Razon[]
  modeloIa: string | null
  aprobadoPor: string | null
  aprobadoEn: string | null
  activo: boolean
  editadoManualmente: boolean
  redactandoPerfil: boolean
}

export interface Maestro {
  id: string
  nombre: string
  nombreNomina: string
  empleados: number
  activo: boolean
  editadoManualmente: boolean
}

export interface CentroCosto {
  id: string
  codigo: string
  nombre: string
  sede: string
  empleados: number
  activo: boolean
  editadoManualmente: boolean
}

export interface ResumenEstructura {
  sedes: number
  areas: number
  centrosCosto: number
  cargos: number
  perfilesPorRevisar: number
  perfilesRedactando: number
  perfilesSinRedactar: number
  actualizadaEn: string | null
  fuente: string | null
  nombresSedes: string[]
  nombresAreas: string[]
}

export type TipoMaestro = 'sedes' | 'areas' | 'centros-costo'

const claves = {
  importacion: ['estructura', 'importacion-actual'] as const,
  resumen: ['estructura', 'resumen'] as const,
}

function query(params: Record<string, string | number | undefined>) {
  const q = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== '') q.set(k, String(v))
  return q.toString()
}

export function useImportacionActual() {
  return useQuery({
    queryKey: claves.importacion,
    queryFn: ({ signal }) => apiGet<Importacion | null>('/api/v1/core/nomina/importaciones/actual', signal),
    // Mientras la IA organiza, se consulta seguido para mostrar el resultado apenas llegue
    refetchInterval: (q) => (q.state.data?.estado === 'OrganizandoConIa' ? 2000 : false),
  })
}

export function useImportarNomina() {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (archivo: File) => {
      const form = new FormData()
      form.append('archivo', archivo)
      form.append('fuente', 'Carga manual')
      return apiUpload<{ id: string }>('/api/v1/core/nomina/importaciones', form)
    },
    onSuccess: () => cliente.invalidateQueries({ queryKey: claves.importacion }),
  })
}

export function useCambiarPropuesta(importacionId: string) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: ({ propuestaId, incluida }: { propuestaId: string; incluida: boolean }) =>
      apiSend('PATCH', `/api/v1/core/nomina/importaciones/${importacionId}/propuestas/${propuestaId}`, { incluida }),
    onSuccess: () => cliente.invalidateQueries({ queryKey: claves.importacion }),
  })
}

export function useAplicarImportacion(importacionId: string) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: () => apiSend('POST', `/api/v1/core/nomina/importaciones/${importacionId}/aplicar`),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['estructura'] }),
  })
}

export function useResumenEstructura() {
  return useQuery({
    queryKey: claves.resumen,
    queryFn: ({ signal }) => apiGet<ResumenEstructura>('/api/v1/core/estructura/resumen', signal),
    // Mientras la IA redacta perfiles, se refresca para ver el avance real
    refetchInterval: (q) => ((q.state.data?.perfilesRedactando ?? 0) > 0 ? 3000 : false),
  })
}

export function useCargos(filtro: { pagina: number; tamano?: number; buscar: string; estado: string }) {
  return useQuery({
    queryKey: ['estructura', 'cargos', filtro],
    queryFn: ({ signal }) =>
      apiGet<Pagina<Cargo>>(`/api/v1/core/estructura/cargos?${query({ pagina: filtro.pagina, tamano: filtro.tamano, buscar: filtro.buscar, estadoPerfil: filtro.estado })}`, signal),
    placeholderData: keepPreviousData,
    refetchInterval: (q) => (q.state.data?.items.some((c) => c.redactandoPerfil) ? 3000 : false),
  })
}

export function useCargo(id: string, activo = true) {
  return useQuery({
    queryKey: ['estructura', 'cargo', id],
    queryFn: ({ signal }) => apiGet<Cargo>(`/api/v1/core/estructura/cargos/${id}`, signal),
    enabled: activo && Boolean(id),
    // Mientras la IA redacta el perfil, se consulta seguido para mostrarlo apenas llegue
    refetchInterval: (q) => (q.state.data?.redactandoPerfil ? 3000 : false),
  })
}

export function useMaestros<T extends Maestro | CentroCosto>(tipo: TipoMaestro, filtro: { pagina: number; tamano?: number; buscar: string }) {
  return useQuery({
    queryKey: ['estructura', tipo, filtro],
    queryFn: ({ signal }) => apiGet<Pagina<T>>(`/api/v1/core/estructura/${tipo}?${query({ pagina: filtro.pagina, tamano: filtro.tamano, buscar: filtro.buscar })}`, signal),
    placeholderData: keepPreviousData,
  })
}

function useMutacionEstructura<V, R = unknown>(fn: (v: V) => Promise<R>) {
  const cliente = useQueryClient()
  return useMutation({ mutationFn: fn, onSuccess: () => cliente.invalidateQueries({ queryKey: ['estructura'] }) })
}

export const useCrearCargo = () =>
  useMutacionEstructura((v: { nombre: string; area: string; descripcion?: string; perfil?: Perfil }) =>
    apiSend<{ id: string }>('POST', '/api/v1/core/estructura/cargos', v))

export const useEditarCargo = () =>
  useMutacionEstructura((v: { id: string; nombre: string; area: string; activo: boolean; perfil: Perfil | null }) =>
    apiSend('PUT', `/api/v1/core/estructura/cargos/${v.id}`, { nombre: v.nombre, area: v.area, activo: v.activo, perfil: v.perfil }))

export const useAprobarPerfil = () =>
  useMutacionEstructura((cargoId: string) => apiSend('POST', `/api/v1/core/estructura/cargos/${cargoId}/aprobar-perfil`))

export const useSolicitarPerfil = () =>
  useMutacionEstructura((cargoId: string) => apiSend('POST', `/api/v1/core/estructura/cargos/${cargoId}/solicitar-perfil`))

export const useCrearMaestro = (tipo: TipoMaestro) =>
  useMutacionEstructura((v: { nombre: string; codigo?: string; sede?: string }) =>
    apiSend<{ id: string }>('POST', `/api/v1/core/estructura/${tipo}`, tipo === 'centros-costo' ? { codigo: v.codigo ?? '', nombre: v.nombre, sede: v.sede ?? '' } : { nombre: v.nombre }))

export const useEditarMaestro = (tipo: TipoMaestro) =>
  useMutacionEstructura((v: { id: string; nombre: string; activo: boolean; sede?: string }) =>
    apiSend('PUT', `/api/v1/core/estructura/${tipo}/${v.id}`, tipo === 'centros-costo' ? { nombre: v.nombre, sede: v.sede ?? '', activo: v.activo } : { nombre: v.nombre, activo: v.activo }))
