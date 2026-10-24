import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { apiGet } from '../../shared/api/client'
import type { Pagina } from '../../shared/ui/Paginacion'

export interface CandidatoResumen {
  id: string
  nombre: string
  documento: string
  ciudad: string
  ultimoCargo: string
  mesesExperiencia: number
  habilidades: string[]
  postulaciones: number
  ultimaPostulacion: string | null
  alertas: number
  tieneLinkedIn: boolean
}

export interface BancoTalento {
  resultados: Pagina<CandidatoResumen>
  entendido: string[]
}

export interface Alerta {
  tipo: 'FechasSuperpuestas' | 'Hueco' | 'FechaImposible' | 'SinFechas' | 'SinExperiencia'
  texto: string
  evidencia: string
}

export interface PostulacionCandidato {
  postulacionId: string
  numero: string
  vacanteId: string
  vacanteCodigo: string
  vacanteTitulo: string
  estadoVacante: string
  canal: string
  recibidaEn: string
  respuestasNegativas: number
}

export type GrupoHabilidad = 'Oficio' | 'Herramientas' | 'Certificaciones' | 'Idiomas' | 'Blandas'

/** Habilidades agrupadas; cada una trae lo que escribió el candidato (la evidencia). */
export interface ConjuntoHabilidades {
  grupo: GrupoHabilidad
  habilidades: { nombre: string; menciona: string }[]
}

export interface CandidatoDetalle {
  id: string
  nombre: string
  documento: string
  celular: string
  correo: string
  ciudad: string
  linkedIn: string
  experiencia: { cargo: string; cargoNormalizado: string; empresa: string; desde: string; hasta: string; meses: number | null; actual: boolean }[]
  formacion: { titulo: string; institucion: string; anio: string }[]
  cursos: string[]
  habilidades: string[]
  conjuntosHabilidades: ConjuntoHabilidades[]
  mesesExperiencia: number
  alertas: Alerta[]
  postulaciones: PostulacionCandidato[]
  posiblesDuplicados: { id: string; nombre: string; documento: string; coincide: string }[]
  suprimido: boolean
  autorizacion: string | null
  creadoEn: string
  actualizadoEn: string
}

/** Banco de talento: la búsqueda se escribe en lenguaje natural («montacarguistas con licencia C1 en Mosquera»). */
export function useBancoTalento(filtro: { buscar: string; pagina: number; tamano: number }) {
  const q = new URLSearchParams({ pagina: String(filtro.pagina), tamano: String(filtro.tamano) })
  if (filtro.buscar) q.set('buscar', filtro.buscar)
  return useQuery({
    queryKey: ['candidatos', 'banco', filtro],
    queryFn: ({ signal }) => apiGet<BancoTalento>(`/api/v1/reclutamiento/candidatos?${q.toString()}`, signal),
    placeholderData: keepPreviousData,
  })
}

export function useCandidato(id: string) {
  return useQuery({ queryKey: ['candidatos', id], queryFn: ({ signal }) => apiGet<CandidatoDetalle>(`/api/v1/reclutamiento/candidatos/${id}`, signal) })
}

/** «68 meses» → «5 años y 8 meses». */
export function duracion(meses: number) {
  if (meses <= 0) return 'Sin experiencia con fechas'
  const anios = Math.floor(meses / 12)
  const resto = meses % 12
  const a = anios > 0 ? `${anios} ${anios === 1 ? 'año' : 'años'}` : ''
  const m = resto > 0 ? `${resto} ${resto === 1 ? 'mes' : 'meses'}` : ''
  return [a, m].filter(Boolean).join(' y ')
}
