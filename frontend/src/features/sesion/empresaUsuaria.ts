import { useSyncExternalStore } from 'react'
import { useSesion } from './api'

const clave = 'selvana.empresa-usuaria'
const oyentes = new Set<() => void>()

function leer(): string {
  try {
    return localStorage.getItem(clave) ?? ''
  } catch {
    return ''
  }
}

function suscribir(oyente: () => void) {
  oyentes.add(oyente)
  return () => oyentes.delete(oyente)
}

/** Cambia la empresa usuaria en la que se trabaja ('' = todas las que el usuario ve). Se recuerda en este navegador. */
export function elegirEmpresaUsuaria(id: string) {
  try {
    if (id) localStorage.setItem(clave, id)
    else localStorage.removeItem(clave)
  } catch {
    // Sin almacenamiento (modo privado): la elección dura hasta recargar
  }
  memoria = id
  oyentes.forEach((o) => o())
}

let memoria = leer()

/**
 * Empresa usuaria elegida en la barra lateral. Si la sesión ya no la incluye (le quitaron el acceso), vuelve a «todas».
 * Solo aplica a empresas de servicios temporales.
 */
export function useEmpresaUsuariaActual() {
  const { data: sesion } = useSesion()
  const elegida = useSyncExternalStore(suscribir, () => memoria, () => '')
  const valida = sesion?.empresasUsuarias.find((e) => e.id === elegida)
  return { id: valida?.id ?? '', nombre: valida?.nombre ?? '' }
}
