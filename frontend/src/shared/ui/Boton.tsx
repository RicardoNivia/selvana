import type { ButtonHTMLAttributes, ReactNode } from 'react'

interface BotonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variante?: 'primario' | 'secundario' | 'ia'
  pequeno?: boolean
  /** Mientras llama a la API: se deshabilita, muestra el indicador y el texto en gerundio. */
  cargando?: boolean
  textoCargando?: string
  icono?: ReactNode
}

/** Botón de Selvana. Norma: todo botón que llama a la API usa `cargando` (skill selvana-marca). */
export function Boton({ variante = 'secundario', pequeno, cargando, textoCargando, icono, children, disabled, className, type, ...resto }: BotonProps) {
  const clases = ['boton', `boton-${variante}`, pequeno ? 'boton-pequeno' : '', className ?? ''].filter(Boolean).join(' ')
  return (
    <button type={type ?? 'button'} className={clases} disabled={disabled || cargando} aria-busy={cargando || undefined} {...resto}>
      {cargando ? <span className="indicador" aria-hidden="true" /> : icono}
      {cargando ? (textoCargando ?? children) : children}
    </button>
  )
}
