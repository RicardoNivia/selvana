import type { MiCuenta } from './api'

export type SeccionHv = 'datos' | 'experiencia' | 'estudios' | 'cursos'

/** Lo que falta para que el perfil quede completo (reglas, sin IA): se usa en el anillo del inicio y en la lista «Completa tu perfil». */
export function faltantesPerfil(c: MiCuenta): { texto: string; seccion: SeccionHv | 'archivo' }[] {
  const d = c.datos
  return [
    !c.nombreArchivoHojaDeVida && { texto: 'Sube tu hoja de vida', seccion: 'archivo' as const },
    !d.celular && { texto: 'Agrega tu celular', seccion: 'datos' as const },
    !d.ciudad && { texto: 'Cuéntanos en qué ciudad vives', seccion: 'datos' as const },
    d.experiencia.length === 0 && { texto: 'Agrega tu experiencia', seccion: 'experiencia' as const },
    d.formacion.length === 0 && { texto: 'Agrega tus estudios', seccion: 'estudios' as const },
    d.habilidades.filter(Boolean).length === 0 && { texto: 'Escribe tus habilidades', seccion: 'cursos' as const },
  ].filter((x): x is { texto: string; seccion: SeccionHv | 'archivo' } => Boolean(x))
}
export const totalPasosPerfil = 6
