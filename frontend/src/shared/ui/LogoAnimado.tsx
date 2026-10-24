import type { CSSProperties } from 'react'

interface Props {
  /** Alto del isotipo en px. */
  tamano?: number
  /** Muestra la palabra «selvana» a la derecha, letra por letra. */
  palabra?: boolean
  /** Versión sobre fondo selva: hojas claras. */
  claro?: boolean
  className?: string
}

/**
 * Isotipo animado: las hojas brotan desde el tallo, la semilla de la IA cae a su lugar y después todo se mece.
 * Solo para la página comercial, el inicio de sesión y esperas. Con prefers-reduced-motion queda quieto.
 */
export function LogoAnimado({ tamano = 40, palabra = false, claro = false, className = '' }: Props) {
  return (
    <span className={`logo-animado ${claro ? 'logo-claro' : ''} ${className}`} style={{ '--logo-tamano': `${tamano}px` } as CSSProperties}>
      <svg viewBox="0 0 48 48" width={tamano} height={tamano} aria-hidden="true">
        <g className="logo-hoja logo-hoja-arriba">
          <path d="M24 24C24 12 31 5 43 5C43 17 36 24 24 24Z" />
          <path className="logo-nervio" d="M24 24L39 9" />
        </g>
        <g className="logo-hoja logo-hoja-abajo">
          <path d="M24 24C24 36 17 43 5 43C5 31 12 24 24 24Z" />
          <path className="logo-nervio" d="M24 24L9 39" />
        </g>
        <circle className="logo-semilla-halo" cx="11" cy="12" r="5.5" />
        <circle className="logo-semilla" cx="11" cy="12" r="5.5" />
      </svg>
      {palabra && (
        <span className="logo-palabra" aria-label="selvana">
          {[...'selvana'].map((letra, i) => (
            <span key={i} aria-hidden="true" style={{ '--i': i } as CSSProperties}>{letra}</span>
          ))}
        </span>
      )}
    </span>
  )
}
