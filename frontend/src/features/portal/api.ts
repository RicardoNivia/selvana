import { keepPreviousData, useMutation, useQuery } from '@tanstack/react-query'
import { apiGet, apiSend, apiUpload } from '../../shared/api/client'
import type { Pagina } from '../../shared/ui/Paginacion'

export interface VacantePublica {
  codigo: string
  titulo: string
  empresa: string
  ciudad: string
  jornada: string
  cupos: number
  salarioMinimo: number
  salarioMaximo: number
  resumen: string
  publicadaEn: string
}

export interface Conteo {
  nombre: string
  total: number
}

export interface Portal {
  vacantes: Pagina<VacantePublica>
  ciudades: Conteo[]
  jornadas: Conteo[]
  empresas: number
}

export interface VacantePublicaDetalle extends Omit<VacantePublica, 'resumen'> {
  descripcion: string
  requisitos: string[]
  preguntasFiltro: string[]
  recibePostulaciones: boolean
}

export interface DatosCandidato {
  nombre: string
  documento: string
  celular: string
  correo: string
  ciudad: string
  experiencia: { cargo: string; empresa: string; desde: string; hasta: string }[]
  formacion: { titulo: string; institucion: string; anio: string }[]
  cursos: string[]
  habilidades: string[]
  linkedIn: string
}

export interface BorradorPostulacion {
  id: string
  estado: 'Leyendo' | 'Leida' | 'Fallida'
  mensaje: string | null
  nombreArchivo: string
  datos: DatosCandidato | null
  vacanteCodigo: string
  vacanteTitulo: string
  empresa: string
  ciudad: string
  preguntasFiltro: string[]
  versionAutorizacion: string
  textoAutorizacion: string
}

export function usePortal(filtro: { pagina: number; tamano: number; buscar: string; ciudad: string; jornada: string }) {
  const q = new URLSearchParams()
  q.set('pagina', String(filtro.pagina))
  q.set('tamano', String(filtro.tamano))
  if (filtro.buscar) q.set('buscar', filtro.buscar)
  if (filtro.ciudad) q.set('ciudad', filtro.ciudad)
  if (filtro.jornada) q.set('jornada', filtro.jornada)
  return useQuery({
    queryKey: ['portal', filtro],
    queryFn: ({ signal }) => apiGet<Portal>(`/api/v1/portal/vacantes?${q.toString()}`, signal),
    placeholderData: keepPreviousData,
  })
}

export function useVacantePublica(codigo: string) {
  return useQuery({ queryKey: ['portal', 'vacante', codigo], queryFn: ({ signal }) => apiGet<VacantePublicaDetalle>(`/api/v1/portal/vacantes/${codigo}`, signal) })
}

export function useSubirHojaDeVida(codigo: string) {
  return useMutation({
    mutationFn: (archivo: File) => {
      const form = new FormData()
      form.append('hojaDeVida', archivo)
      return apiUpload<{ id: string }>(`/api/v1/portal/vacantes/${codigo}/postulaciones`, form)
    },
  })
}

export function useBorrador(id: string | undefined) {
  return useQuery({
    queryKey: ['portal', 'borrador', id],
    queryFn: ({ signal }) => apiGet<BorradorPostulacion>(`/api/v1/portal/postulaciones/${id}`, signal),
    enabled: Boolean(id),
    // Mientras la IA lee la hoja de vida, se consulta seguido
    refetchInterval: (q) => (q.state.data?.estado === 'Leyendo' ? 1500 : false),
  })
}

export interface EnvioPostulacion {
  nombre: string
  documento: string
  celular: string
  correo: string
  ciudad: string
  respuestas: boolean[]
  aceptaAutorizacion: boolean
  canal: string
  linkedIn?: string
}

export function useEnviarPostulacion(borradorId: string) {
  return useMutation({ mutationFn: (v: EnvioPostulacion) => apiSend<{ numero: string }>('POST', `/api/v1/portal/postulaciones/${borradorId}/enviar`, v) })
}

/** Mismo formato que valida la API: un perfil linkedin.com/in/usuario. */
export const esLinkedIn = (texto: string) => /^(https?:\/\/)?([a-z]{2,3}\.)?linkedin\.com\/in\/[A-Za-z0-9\-_%]{3,100}\/?(\?.*)?$/i.test(texto.trim())

export const pesos = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 })

/** Rango salarial para el portal; si la empresa decidió no mostrarlo, la API lo manda en cero y se lee «A convenir». */
export function rangoSalario(min: number, max: number) {
  if (min <= 0 && max <= 0) return 'A convenir'
  return max > min ? `${pesos.format(min)} – ${pesos.format(max)}` : pesos.format(min)
}
