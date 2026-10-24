import type { CSSProperties } from 'react'

/** Bloque de carga con la forma del contenido que viene. Norma: nunca una pantalla en blanco ni un "Cargando…" suelto. */
export function Esqueleto({ alto = 14, ancho = '100%', redondo, style }: { alto?: number; ancho?: number | string; redondo?: boolean; style?: CSSProperties }) {
  return <span className="esqueleto" aria-hidden="true" style={{ height: alto, width: ancho, borderRadius: redondo ? 999 : undefined, ...style }} />
}

/** Fila de cifras (tarjetas con número grande), como en Conectar nómina. */
export function EsqueletoCifras({ cantidad = 5 }: { cantidad?: number }) {
  return (
    <div className="rejilla" aria-busy="true" aria-label="Cargando cifras">
      {Array.from({ length: cantidad }, (_, i) => (
        <div key={i} className="cifra">
          <Esqueleto alto={12} ancho="50%" />
          <Esqueleto alto={26} ancho="40%" style={{ margin: '8px 0' }} />
          <Esqueleto alto={11} ancho="70%" />
        </div>
      ))}
    </div>
  )
}

/** Filas de una lista o tabla. */
export function EsqueletoFilas({ filas = 6, conPill = true }: { filas?: number; conPill?: boolean }) {
  return (
    <div aria-busy="true" aria-label="Cargando lista">
      {Array.from({ length: filas }, (_, i) => (
        <div key={i} className="esqueleto-fila">
          <span style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 6 }}>
            <Esqueleto alto={14} ancho={`${60 + ((i * 17) % 30)}%`} />
            <Esqueleto alto={11} ancho="35%" />
          </span>
          <Esqueleto alto={14} ancho={36} />
          {conPill && <Esqueleto alto={20} ancho={84} redondo />}
        </div>
      ))}
    </div>
  )
}

/** Panel de detalle (título, campos y barra de decisión). */
export function EsqueletoPanel() {
  return (
    <div className="esqueleto-panel" aria-busy="true" aria-label="Cargando detalle">
      <Esqueleto alto={24} ancho="55%" />
      <Esqueleto alto={12} ancho="40%" />
      <Esqueleto alto={64} />
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 12 }}>
        <Esqueleto alto={120} />
        <Esqueleto alto={120} />
      </div>
      <Esqueleto alto={44} ancho={160} />
    </div>
  )
}
