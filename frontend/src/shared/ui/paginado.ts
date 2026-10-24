import { useState } from 'react'

/** Norma: 10 registros por página por defecto; la persona elige 10, 25, 50 o 100. */
export const TAMANOS_PAGINA = [10, 25, 50, 100] as const
export const TAMANO_PAGINA = 10

/** La bandeja de aprobaciones muestra pocas decisiones a la vez: 5 por defecto. */
export const TAMANOS_BANDEJA = [5, 10, 25, 50] as const

/** Página y tamaño de una lista; cambiar el tamaño vuelve a la primera página. */
export function usePaginacion(tamanoInicial: number = TAMANO_PAGINA) {
  const [pagina, setPagina] = useState(1)
  const [tamano, setTamanoActual] = useState(tamanoInicial)
  const setTamano = (t: number) => {
    setTamanoActual(t)
    setPagina(1)
  }
  return { pagina, setPagina, tamano, setTamano }
}
