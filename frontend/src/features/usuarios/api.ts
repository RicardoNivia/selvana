import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiSend } from '../../shared/api/client'
import type { Pagina } from '../../shared/ui/Paginacion'

export interface EmpresaUsuariaResumen {
  id: string
  nombre: string
}

export interface Usuario {
  id: string
  nombre: string
  correo: string
  rol: string
  estado: 'Invitado' | 'Activo' | 'Desactivado'
  todasLasEmpresasUsuarias: boolean
  empresasUsuarias: EmpresaUsuariaResumen[]
  cargoNombre: string | null
  ultimoIngreso: string | null
  rolSugeridoIa: string | null
  rolCorregido: boolean
  invitacionVigente: boolean
  invitadoEn: string
  invitadoPor: string
}

export interface SugerenciaRol {
  rol: string
  razon: string
  evidencia: string
}

export interface InvitacionEmitida {
  usuarioId: string
  token: string
  vence: string
}

export interface MatrizPermisos {
  roles: string[]
  permisos: { accion: string; texto: string; nota: string | null; sensible: boolean; celdas: { rol: string; alcance: string }[] }[]
}

export interface EmpresaUsuaria {
  id: string
  nombre: string
  nit: string
  sector: string
  ciudad: string
  activa: boolean
  usuarios: number
  creadaEn: string
}

export interface Invitacion {
  nombre: string
  correo: string
  empresa: string
  rol: string
  vigente: boolean
}

/** Roles que se pueden invitar (el candidato se registra solo en el portal). */
export const rolesInvitables = ['Administrador', 'GerenteRrhh', 'Analista', 'Lider', 'Empleado'] as const

export const textoAlcance: Record<string, string> = {
  Si: 'Sí',
  No: '—',
  SuEquipo: 'Su equipo',
  SuVacante: 'Su vacante',
  LoSuyo: 'Lo suyo',
  Pedir: 'Pedir',
}

/** Enlace que la persona abre para crear su contraseña. */
export const enlaceInvitacion = (token: string) => `${window.location.origin}/activar/${token}`

const q = (v: Record<string, string | number | undefined>) =>
  new URLSearchParams(Object.entries(v).filter(([, x]) => x !== undefined && x !== '').map(([k, x]) => [k, String(x)])).toString()

export function useUsuarios(filtro: { pagina: number; tamano?: number; buscar: string; rol: string }) {
  return useQuery({
    queryKey: ['usuarios', filtro],
    queryFn: ({ signal }) => apiGet<Pagina<Usuario>>(`/api/v1/core/usuarios?${q(filtro)}`, signal),
    placeholderData: keepPreviousData,
  })
}

export function useMatrizPermisos() {
  return useQuery({ queryKey: ['usuarios', 'permisos'], queryFn: ({ signal }) => apiGet<MatrizPermisos>('/api/v1/core/usuarios/permisos', signal), staleTime: Infinity })
}

export function useSugerenciaRol(cargoId: string | undefined) {
  return useQuery({
    queryKey: ['usuarios', 'sugerir-rol', cargoId],
    queryFn: ({ signal }) => apiGet<SugerenciaRol>(`/api/v1/core/usuarios/sugerir-rol?cargoId=${cargoId}`, signal),
    enabled: Boolean(cargoId),
    staleTime: Infinity,
  })
}

export interface DatosInvitacion {
  correo: string
  nombre: string
  rol: string
  cargoId?: string
  todasLasEmpresasUsuarias: boolean
  empresasUsuarias: string[]
  rolSugerido?: string
  razonSugerencia?: string
}

export function useInvitar() {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (d: DatosInvitacion) => apiSend<InvitacionEmitida>('POST', '/api/v1/core/usuarios', d),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['usuarios'] }),
  })
}

export function useCambiarUsuario(id: string) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (d: { nombre: string; rol: string; todasLasEmpresasUsuarias: boolean; empresasUsuarias: string[]; activo: boolean }) =>
      apiSend('PUT', `/api/v1/core/usuarios/${id}`, d),
    onSuccess: () => {
      void cliente.invalidateQueries({ queryKey: ['usuarios'] })
      void cliente.invalidateQueries({ queryKey: ['empresas-usuarias'] })
    },
  })
}

export function useRenovarInvitacion() {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => apiSend<InvitacionEmitida>('POST', `/api/v1/core/usuarios/${id}/invitacion`),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['usuarios'] }),
  })
}

export function useEmpresasUsuarias() {
  return useQuery({ queryKey: ['empresas-usuarias'], queryFn: ({ signal }) => apiGet<EmpresaUsuaria[]>('/api/v1/core/empresas-usuarias', signal) })
}

export function useAgregarEmpresaUsuaria() {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (d: { nombre: string; nit: string; sector: string; ciudad: string }) => apiSend<{ id: string }>('POST', '/api/v1/core/empresas-usuarias', d),
    onSuccess: () => {
      void cliente.invalidateQueries({ queryKey: ['empresas-usuarias'] })
      void cliente.invalidateQueries({ queryKey: ['sesion'] })
      void cliente.invalidateQueries({ queryKey: ['core', 'empresa'] })
    },
  })
}

export function useEditarEmpresaUsuaria(id: string) {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (d: { nombre: string; sector: string; ciudad: string; activa: boolean }) => apiSend('PUT', `/api/v1/core/empresas-usuarias/${id}`, d),
    onSuccess: () => {
      void cliente.invalidateQueries({ queryKey: ['empresas-usuarias'] })
      void cliente.invalidateQueries({ queryKey: ['sesion'] })
      void cliente.invalidateQueries({ queryKey: ['core', 'empresa'] })
    },
  })
}

export function useInvitacion(token: string) {
  return useQuery({ queryKey: ['invitacion', token], queryFn: ({ signal }) => apiGet<Invitacion>(`/api/v1/sesion/invitaciones/${token}`, signal), retry: false })
}

export function useActivarCuenta() {
  return useMutation({ mutationFn: (d: { token: string; contrasena: string }) => apiSend('POST', '/api/v1/sesion/activar', d) })
}

/**
 * Dígito de verificación del NIT (algoritmo de la DIAN, el mismo de la API): da la respuesta mientras se escribe.
 * Devuelve el NIT normalizado «900412887-9» o null si no es válido.
 */
export function normalizarNit(texto: string): string | null {
  const limpio = texto.replace(/[.\s]/g, '')
  const m = /^(\d{9})(?:-(\d))?$/.exec(limpio)
  if (!m) return null
  const cuerpo = m[1]!
  const pesos = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71]
  let suma = 0
  for (let i = 0; i < cuerpo.length; i++) suma += Number(cuerpo[cuerpo.length - 1 - i]) * pesos[i]!
  const residuo = suma % 11
  const dv = residuo > 1 ? 11 - residuo : residuo
  return m[2] === undefined || Number(m[2]) === dv ? `${cuerpo}-${dv}` : null
}

/** «900412887-9» → «900.412.887-9». */
export const formatoNit = (nit: string) => nit.replace(/^(\d{3})(\d{3})(\d{3})-(\d)$/, '$1.$2.$3-$4')
