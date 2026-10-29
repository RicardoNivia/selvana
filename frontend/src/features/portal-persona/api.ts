import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiSend, apiUpload, urlApi } from '../../shared/api/client'
import type { DatosCandidato } from '../portal/api'

/** Portal de la persona (ADR 0013): su propia sesión, distinta de la consola. */
const base = '/api/v1/portal/cuenta'

export interface HojaDeVidaPerfil {
  id: string
  estado: 'Leyendo' | 'Leida' | 'Fallida'
  mensaje: string | null
  nombreArchivo: string
  datos: DatosCandidato | null
  versionAutorizacion: string
  textoAutorizacion: string
}

export interface MiCuenta {
  id: string
  correo: string
  tipoDocumento: TipoDocumento
  datos: DatosCandidato
  nombreArchivoHojaDeVida: string | null
  hojaDeVidaActualizadaEn: string | null
  versionAutorizacion: string
  autorizadaEn: string
  creadaEn: string
}

export interface MiPostulacion {
  postulacionId: string
  numero: string
  vacanteCodigo: string
  vacante: string
  empresa: string
  ciudad: string
  recibidaEn: string
  estado: string
  explicacion: string
}

/** Algo que la persona debe hacer: responder el chat, presentar pruebas, aceptar la oferta y firmar, subir documentos. */
export interface Pendiente {
  tipo: 'Chat' | 'Pruebas' | 'Oferta'
  id: string
  titulo: string
  vacante: string
  empresa: string
  venceEn: string | null
}

export interface MiPortal {
  pendientes: Pendiente[]
  postulaciones: MiPostulacion[]
}

export interface OfertaParaTi {
  codigo: string
  titulo: string
  empresa: string
  ciudad: string
  jornada: string
  salarioMinimo: number
  salarioMaximo: number
  porque: string[]
  publicadaEn: string
}

export interface DatosEditables {
  nombre: string
  celular: string
  ciudad: string
  linkedIn?: string
}

/** Registro con lo mínimo; la hoja de vida se sube después en el portal. */
export interface Registro {
  correo: string
  contrasena: string
  nombre: string
  tipoDocumento: TipoDocumento
  numeroDocumento: string
  aceptaAutorizacion: boolean
}

export type TipoDocumento = 'CC' | 'CE' | 'TI' | 'PA' | 'PPT'

export const tiposDocumento: [TipoDocumento, string][] = [
  ['CC', 'Cédula de ciudadanía'],
  ['CE', 'Cédula de extranjería'],
  ['TI', 'Tarjeta de identidad'],
  ['PA', 'Pasaporte'],
  ['PPT', 'Permiso por protección temporal'],
]

const claves = { cuenta: ['portal-persona', 'cuenta'] as const, portal: ['portal-persona', 'portal'] as const }

/** Sin sesión responde 401: la pantalla manda a ingresar. */
export function useMiCuenta() {
  return useQuery({ queryKey: claves.cuenta, queryFn: ({ signal }) => apiGet<MiCuenta>(`${base}/`, signal), retry: false })
}

export function useMiPortal(activo = true) {
  return useQuery({ queryKey: claves.portal, queryFn: ({ signal }) => apiGet<MiPortal>(`${base}/portal`, signal), retry: false, refetchInterval: 30_000, enabled: activo })
}

export function useOfertasParaTi(activo: boolean) {
  return useQuery({ queryKey: ['portal-persona', 'ofertas'], queryFn: ({ signal }) => apiGet<OfertaParaTi[]>(`${base}/ofertas`, signal), enabled: activo, retry: false })
}

export function useSubirHojaDeVidaPerfil() {
  return useMutation({
    mutationFn: (archivo: File) => {
      const form = new FormData()
      form.append('hojaDeVida', archivo)
      return apiUpload<{ id: string }>(`${base}/hoja-de-vida`, form)
    },
  })
}

/** Mientras la IA lee la hoja de vida, se consulta seguido. */
export function useHojaDeVidaPerfil(id: string | undefined) {
  return useQuery({
    queryKey: ['portal-persona', 'hoja-de-vida', id],
    queryFn: ({ signal }) => apiGet<HojaDeVidaPerfil>(`${base}/hoja-de-vida/${id}`, signal),
    enabled: Boolean(id),
    refetchInterval: (q) => (q.state.data?.estado === 'Leyendo' ? 1500 : false),
  })
}

function useMutacionCuenta<V, R = unknown>(fn: (v: V) => Promise<R>) {
  const cliente = useQueryClient()
  return useMutation({ mutationFn: fn, onSuccess: () => cliente.invalidateQueries({ queryKey: ['portal-persona'] }) })
}

export const useRegistrarme = () => useMutacionCuenta((v: Registro) => apiSend<{ cuentaId: string; nombre: string }>('POST', `${base}/registro`, v))

export const useIngresarPortal = () => useMutacionCuenta((v: { correo: string; contrasena: string }) => apiSend<{ cuentaId: string; nombre: string }>('POST', `${base}/ingresar`, v))

export function useSalirPortal() {
  const cliente = useQueryClient()
  return useMutation({ mutationFn: () => apiSend('POST', `${base}/salir`), onSettled: () => cliente.resetQueries({ queryKey: ['portal-persona'] }) })
}

export const useActualizarMisDatos = () => useMutacionCuenta((v: DatosEditables) => apiSend('PUT', `${base}/datos`, v))

export interface MensajeChat {
  autor: string
  esMio: boolean
  texto: string
  en: string
}

/** Historial del chat de preselección de una de sus postulaciones. */
export function useChatDePostulacion(postulacionId: string, activo: boolean) {
  return useQuery({
    queryKey: ['portal-persona', 'chat', postulacionId],
    queryFn: ({ signal }) => apiGet<{ abierto: boolean; mensajes: MensajeChat[] }>(`${base}/postulaciones/${postulacionId}/chat`, signal),
    enabled: activo,
  })
}

/** Su hoja de vida escrita o corregida a mano (reemplaza experiencia, estudios, cursos y habilidades). */
export interface PerfilEditable extends DatosEditables {
  experiencia: { cargo: string; empresa: string; desde: string; hasta: string }[]
  formacion: { titulo: string; institucion: string; anio: string }[]
  cursos: string[]
  habilidades: string[]
}

export const useActualizarMiPerfil = () => useMutacionCuenta((v: PerfilEditable) => apiSend('PUT', `${base}/perfil`, v))

export const useActualizarMiHojaDeVida = () => useMutacionCuenta((v: DatosEditables & { borradorId: string }) => apiSend('PUT', `${base}/hoja-de-vida`, v))

/** Renueva el enlace del pendiente (solo si es de la persona) y devuelve la pantalla donde se responde. */
export const useAbrirPendiente = () =>
  useMutation({ mutationFn: (p: Pendiente) => apiSend<{ ruta: string }>('POST', `${base}/pendientes/${p.tipo}/${p.id}/abrir`) })

/** Deja listo el borrador de la vacante con la hoja de vida del perfil; la persona responde las preguntas en la postulación. */
export const usePostularmeConMiPerfil = () => useMutation({ mutationFn: (codigo: string) => apiSend<{ id: string }>('POST', `${base}/postular/${codigo}`) })

/* ---------- P4 · Mis documentos ---------- */

export type Vigencia = 'Vigente' | 'PorVencer' | 'Vencido' | 'SinVencimiento'

export interface MiDocumento {
  id: string
  tipo: string
  nombre: string
  nombreArchivo: string
  venceEn: string | null
  vigencia: Vigencia
  subidoEn: string
  envios: { empresa: string; en: string }[]
}

/** Lo que le pide un proceso de contratación; `puedeEnviar` si ya lo tiene vigente en su portal. */
export interface DocumentoPedido {
  procesoId: string
  empresa: string
  cargo: string
  tipo: string
  nombre: string
  obligatorio: boolean
  estado: 'Pendiente' | 'Rechazado' | 'Leyendo' | 'PorRevisar' | 'Aprobado'
  motivoRechazo: string | null
  puedeEnviar: boolean
}

export interface MisDocumentos {
  tipos: { clave: string; nombre: string; vence: boolean }[]
  documentos: MiDocumento[]
  pedidos: DocumentoPedido[]
}

export function useMisDocumentos(activo = true) {
  return useQuery({ queryKey: ['portal-persona', 'documentos'], queryFn: ({ signal }) => apiGet<MisDocumentos>(`${base}/documentos`, signal), enabled: activo, retry: false })
}

export const useSubirMiDocumento = () =>
  useMutacionCuenta((v: { tipo: string; venceEn?: string; archivo: File }) => {
    const form = new FormData()
    form.append('archivo', v.archivo)
    const q = new URLSearchParams({ tipo: v.tipo })
    if (v.venceEn) q.set('venceEn', v.venceEn)
    return apiUpload<{ id: string }>(`${base}/documentos?${q}`, form)
  })

export const useQuitarMiDocumento = () => useMutacionCuenta((id: string) => apiSend('DELETE', `${base}/documentos/${id}`))

export const useEnviarMiDocumento = () => useMutacionCuenta((v: { procesoId: string; tipo: string }) => apiSend('POST', `${base}/documentos/enviar`, v))

export const urlMiDocumento = (id: string) => urlApi(`${base}/documentos/${id}/archivo`)

/* ---------- P5 · Mi empleo ---------- */

export interface DocumentoDelEmpleo {
  id: string
  tipo: string
  nombre: string
  venceEn: string | null
  vigencia: Vigencia
  enRevision: boolean
}

export interface Empleo {
  empleadoId: string
  empresa: string
  codigo: string
  cargo: string
  area: string
  sede: string
  empresaUsuaria: string | null
  tipoContrato: string
  salarioCop: number
  fechaIngreso: string
  fechaFinContrato: string | null
  estado: 'Activo' | 'Retirado'
  celular: string
  ciudad: string
  eps: string
  fondoPension: string
  arl: string
  cajaCompensacion: string
  documentos: DocumentoDelEmpleo[]
  avisos: string[]
  actualizacionPedida: boolean
  datosConfirmadosEn: string | null
  cambioPendiente: { cambios: string[]; en: string } | null
}

export interface DatosPropuestos {
  celular: string
  ciudad: string
  eps: string
  fondoPension: string
  cajaCompensacion: string
}

export const tiposSoporte = ['Certificado de estudios', 'Curso o certificación', 'Licencia', 'Afiliación', 'Certificado bancario', 'Otro']

export function useMiEmpleo(activo = true) {
  return useQuery({ queryKey: ['portal-persona', 'empleo'], queryFn: ({ signal }) => apiGet<Empleo[]>(`${base}/empleo`, signal), enabled: activo, retry: false })
}

export const useProponerMisDatos = () =>
  useMutacionCuenta((v: DatosPropuestos & { empleadoId: string }) => apiSend('PUT', `${base}/empleo/${v.empleadoId}/datos`, v))

export const useConfirmarMisDatos = () => useMutacionCuenta((empleadoId: string) => apiSend('POST', `${base}/empleo/${empleadoId}/confirmar`))

export const useSubirSoporteEmpleo = () =>
  useMutacionCuenta((v: { empleadoId: string; tipo: string; nombre: string; venceEn?: string; archivo: File }) => {
    const form = new FormData()
    form.append('archivo', v.archivo)
    const q = new URLSearchParams({ tipo: v.tipo, nombre: v.nombre })
    if (v.venceEn) q.set('venceEn', v.venceEn)
    return apiUpload(`${base}/empleo/${v.empleadoId}/soportes?${q}`, form)
  })

export const urlDocumentoEmpleo = (empleadoId: string, documentoId: string) => urlApi(`${base}/empleo/${empleadoId}/documentos/${documentoId}`)
