import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiArchivo, apiGet, apiSend, apiUpload } from '../../shared/api/client'
import type { Pagina } from '../../shared/ui/Paginacion'

const base = '/api/v1/expediente/empleados'

export type EstadoEmpleado = 'Activo' | 'Retirado'
export type TipoNovedad = 'Ingreso' | 'CambioCargo' | 'CambioSalario' | 'Traslado' | 'CambioContrato' | 'ActualizacionDatos' | 'Retiro'

export const nombreNovedad: Record<TipoNovedad, string> = {
  Ingreso: 'Ingreso',
  CambioCargo: 'Cambio de cargo',
  CambioSalario: 'Cambio de salario',
  Traslado: 'Traslado',
  CambioContrato: 'Cambio de contrato',
  ActualizacionDatos: 'Actualización de datos',
  Retiro: 'Retiro',
}

export const novedadesRegistrables: TipoNovedad[] = ['CambioCargo', 'CambioSalario', 'Traslado', 'CambioContrato']

export const tiposDocumento = ['Cédula', 'Contrato', 'Hoja de vida', 'Certificado de estudios', 'Curso o certificación', 'Licencia', 'Afiliación', 'Certificado bancario', 'Otro']

export interface Situacion {
  cargo: string
  area: string
  centroCosto: string
  sede: string
  empresaUsuariaId: string | null
  empresaUsuaria: string | null
  tipoContrato: string
  salarioCop: number
  fechaFinContrato: string | null
}

export interface Afiliaciones { eps: string; fondoPension: string; arl: string; claseRiesgoArl: number | null; cajaCompensacion: string }

export interface FilaEmpleado {
  id: string
  codigo: string
  nombre: string
  documento: string
  cargo: string
  area: string
  sede: string
  empresaUsuaria: string | null
  tipoContrato: string
  salarioCop: number
  fechaIngreso: string
  estado: EstadoEmpleado
  alertas: number
}

export interface Alerta { nivel: 'Vencido' | 'PorVencer' | 'Falta'; texto: string; fuente: string }
export interface Novedad { id: string; tipo: TipoNovedad; fechaEfectiva: string; cambios: string[]; motivo: string; por: string; en: string }
export type EstadoLectura = 'Leyendo' | 'Fallida' | 'PorRevisar' | 'SinCambios' | 'Aplicada' | 'Descartada'
export interface CambioPropuesto { clave: string; etiqueta: string; actual: string; propuesto: string; evidencia: string }
export interface Lectura {
  estado: EstadoLectura
  tipoDetectado: string
  legible: boolean
  tipoCorrecto: boolean
  observaciones: string[]
  razones: { signo: string; texto: string; fuente: string }[]
  cambios: CambioPropuesto[]
  modelo: string | null
  error: string | null
  resueltaPor: string | null
  resueltaEn: string | null
}
export interface Version { archivoId: string; nombreArchivo: string; venceEn: string | null; nota: string | null; subidoPor: string; subidoEn: string; lectura: Lectura | null }
export interface Documento { id: string; tipo: string; nombre: string; venceEn: string | null; estadoVigencia: 'SinVencimiento' | 'Vigente' | 'PorVencer' | 'Vencido'; versiones: Version[] }

export interface Empleado {
  id: string
  codigo: string
  documento: string
  nombre: string
  correo: string
  celular: string
  ciudad: string
  origen: string
  procesoVinculacionId: string | null
  situacion: Situacion
  fechaIngreso: string
  afiliaciones: Afiliaciones
  estado: EstadoEmpleado
  fechaRetiro: string | null
  motivoRetiro: string | null
  alertas: Alerta[]
  novedades: Novedad[]
  documentos: Documento[]
  creadoEn: string
  actualizadoEn: string
  /** Lo que el empleado propuso desde su portal (P5) y espera la decisión de RRHH. */
  datosPropuestos: { cambios: string[]; en: string } | null
  actualizacionPedidaEn: string | null
  datosConfirmadosEn: string | null
}

export interface NuevoEmpleado {
  documento: string
  nombre: string
  correo: string
  celular: string
  ciudad: string
  situacion: Situacion
  fechaIngreso: string
  afiliaciones: Afiliaciones
}

export const pesos = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 })

/** Fechas de la API (aaaa-mm-dd) sin correrse un día por la zona horaria. */
export function fechaCorta(iso: string | null) {
  if (!iso) return '—'
  const [a, m, d] = iso.slice(0, 10).split('-')
  return `${d}/${m}/${a}`
}

export function useEmpleados(f: { pagina: number; tamano: number; buscar: string; estado: string; alertas: boolean }) {
  const q = new URLSearchParams({ pagina: String(f.pagina), tamano: String(f.tamano) })
  if (f.buscar) q.set('buscar', f.buscar)
  if (f.estado) q.set('estado', f.estado)
  if (f.alertas) q.set('alertas', 'true')
  return useQuery({
    queryKey: ['empleados', 'lista', f],
    queryFn: ({ signal }) =>
      apiGet<{ empleados: Pagina<FilaEmpleado>; conteo: { activos: number; retirados: number; conAlertas: number } }>(`${base}?${q}`, signal),
    placeholderData: keepPreviousData,
  })
}

export function useEmpleado(id: string) {
  return useQuery({
    queryKey: ['empleados', 'ficha', id],
    queryFn: ({ signal }) => apiGet<Empleado>(`${base}/${id}`, signal),
    // Mientras la IA lee un documento, se consulta seguido
    refetchInterval: (q) => (q.state.data?.documentos.some((d) => d.versiones[0]?.lectura?.estado === 'Leyendo') ? 2500 : false),
  })
}

function useAccion<V, R = unknown>(fn: (v: V) => Promise<R>) {
  const cliente = useQueryClient()
  return useMutation({ mutationFn: fn, onSuccess: () => cliente.invalidateQueries({ queryKey: ['empleados'] }) })
}

/* P5 · Lo que propone el empleado desde su portal y la campaña de actualización de datos */
export const useAplicarDatosPropuestos = (id: string) => useAccion(() => apiSend('POST', `${base}/${id}/datos-propuestos/aplicar`))
export const useDescartarDatosPropuestos = (id: string) => useAccion(() => apiSend('POST', `${base}/${id}/datos-propuestos/descartar`))
export const usePedirActualizacion = (id: string) => useAccion(() => apiSend<{ pedidos: number }>('POST', `${base}/${id}/pedir-actualizacion`))
export const useCampanaActualizacion = () => useAccion(() => apiSend<{ pedidos: number }>('POST', `${base}/campana-actualizacion`))

export const useCrearEmpleado = () => useAccion((v: NuevoEmpleado) => apiSend<{ id: string }>('POST', base, v))
export const useActualizarDatos = (id: string) =>
  useAccion((v: { nombre: string; correo: string; celular: string; ciudad: string; afiliaciones: Afiliaciones }) => apiSend('PUT', `${base}/${id}/datos`, v))
export const useRegistrarNovedad = (id: string) =>
  useAccion((v: { tipo: TipoNovedad; situacion: Situacion; fechaEfectiva: string; motivo: string }) => apiSend('POST', `${base}/${id}/novedades`, v))
export const useRetirar = (id: string) => useAccion((v: { fecha: string; motivo: string }) => apiSend('POST', `${base}/${id}/retiro`, v))

export const useGuardarDocumento = (id: string) =>
  useAccion((v: { archivo: File; documentoId?: string; tipo?: string; nombre?: string; venceEn?: string | null; nota?: string; leerConIa?: boolean }) => {
    const f = new FormData()
    f.append('archivo', v.archivo)
    const q = new URLSearchParams()
    if (v.documentoId) q.set('documentoId', v.documentoId)
    if (v.tipo) q.set('tipo', v.tipo)
    if (v.nombre) q.set('nombre', v.nombre)
    if (v.venceEn) q.set('venceEn', v.venceEn)
    if (v.nota) q.set('nota', v.nota)
    if (v.leerConIa) q.set('leerConIa', 'true')
    return apiUpload<{ id: string }>(`${base}/${id}/documentos?${q}`, f)
  })

export async function abrirArchivo(empleadoId: string, archivoId: string) {
  const blob = await apiArchivo(`${base}/${empleadoId}/archivos/${archivoId}`, {})
  window.open(URL.createObjectURL(blob), '_blank', 'noopener')
}

/* ---------- Carga de la planta que ya existía (CSV) ---------- */

export interface PlantaPrevisualizada {
  leidas: number
  porCrear: number
  yaExisten: number
  repetidas: number
  errores: string[]
  muestra: { fila: number; documento: string; nombre: string; cargo: string; sede: string; tipoContrato: string; salarioCop: number; fechaIngreso: string; resultado: string }[]
}

const conArchivo = (archivo: File) => {
  const f = new FormData()
  f.append('archivo', archivo)
  return f
}

/** Revisa el archivo sin guardar nada. */
export const usePrevisualizarPlanta = () =>
  useMutation({ mutationFn: (archivo: File) => apiUpload<PlantaPrevisualizada>('/api/v1/expediente/planta/previsualizar', conArchivo(archivo)) })

export const useCargarPlanta = () =>
  useAccion((archivo: File) => apiUpload<{ creados: number; omitidos: number; conErrores: number }>('/api/v1/expediente/planta', conArchivo(archivo)))

/** Plantilla CSV con punto y coma (Excel en español la abre directo) y una fila de ejemplo. */
export function descargarPlantillaPlanta() {
  const csv = '﻿' + [
    'documento;nombre;cargo;salario;fecha_ingreso;area;centro_costo;sede;tipo_contrato;fecha_fin;correo;celular;ciudad;eps;pension;arl;clase_riesgo;caja',
    '1016554209;Diana Gómez;Montacarguista;1900000;02/11/2026;Operaciones;CC-201;Funza;Término fijo;01/11/2027;diana@correo.co;3001234567;Funza;Sura;Porvenir;Positiva;3;Compensar',
  ].join('\r\n')
  const enlace = document.createElement('a')
  enlace.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }))
  enlace.download = 'plantilla-planta-selvana.csv'
  enlace.click()
}

/* ---------- Actualización inteligente (E2): la IA lee, las reglas proponen, RRHH decide ---------- */

const rutaDoc = (id: string, documentoId: string) => `${base}/${id}/documentos/${documentoId}`
export const usePedirLectura = (id: string) => useAccion((documentoId: string) => apiSend('POST', `${rutaDoc(id, documentoId)}/leer`))
export const useAplicarLectura = (id: string) =>
  useAccion((v: { documentoId: string; cambios: { clave: string; valor: string }[] }) => apiSend('POST', `${rutaDoc(id, v.documentoId)}/aplicar`, { cambios: v.cambios }))
export const useDescartarLectura = (id: string) => useAccion((documentoId: string) => apiSend('POST', `${rutaDoc(id, documentoId)}/descartar`))

/** La IA lee PDF y fotos; los Word se revisan a mano. */
export const legiblePorIa = (nombre: string) => /\.(pdf|jpe?g|png|webp)$/i.test(nombre)
