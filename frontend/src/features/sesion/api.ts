import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, apiGet, apiSend } from '../../shared/api/client'

export interface Sesion {
  usuarioId: string
  nombre: string
  correo: string
  rol: string
  empresaId: string
  empresaNombre: string
  empresaTemporal: boolean
  todasLasEmpresasUsuarias: boolean
  empresasUsuarias: { id: string; nombre: string }[]
  /** Acciones de la matriz de permisos que su rol puede hacer (con cualquier alcance). */
  acciones: string[]
}

export const nombreRol: Record<string, string> = {
  Administrador: 'Administrador',
  GerenteRrhh: 'Gerente RRHH',
  Analista: 'Analista de selección',
  Lider: 'Líder',
  Empleado: 'Empleado',
}

/** Iniciales para el avatar: «Servicios Andinos EST» → «SA». */
export const iniciales = (texto: string) =>
  texto.split(/\s+/).filter((p) => p.length > 2 || /^[A-ZÁÉÍÓÚÑ]/.test(p)).slice(0, 2).map((p) => p[0]!.toUpperCase()).join('')

const claveSesion = ['sesion'] as const

/** ¿El rol de la sesión permite esta acción? (AprobarBandeja, CrearVacantes, GestionarUsuarios…) */
export function usePuede(accion: string) {
  const { data } = useSesion()
  return Boolean(data?.acciones.includes(accion))
}

/** La sesión actual; null si no hay (401). */
export function useSesion() {
  return useQuery({
    queryKey: claveSesion,
    queryFn: async ({ signal }) => {
      try {
        return await apiGet<Sesion>('/api/v1/sesion', signal)
      } catch (e) {
        if (e instanceof ApiError && e.status === 401) return null
        throw e
      }
    },
    staleTime: 5 * 60_000,
    retry: false,
  })
}

export function useIngresar() {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: (datos: { correo: string; contrasena: string }) => apiSend<Sesion>('POST', '/api/v1/sesion/ingresar', datos),
    onSuccess: (sesion) => {
      // Lo de la sesión anterior (otra empresa, otro usuario) no se reutiliza
      cliente.clear()
      cliente.setQueryData(claveSesion, sesion)
    },
  })
}

export function useSalir() {
  const cliente = useQueryClient()
  return useMutation({
    mutationFn: () => apiSend('POST', '/api/v1/sesion/salir'),
    onSettled: () => {
      cliente.clear()
      cliente.setQueryData(claveSesion, null)
    },
  })
}
