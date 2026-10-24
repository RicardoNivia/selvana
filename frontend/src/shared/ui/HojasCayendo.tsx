import type { CSSProperties } from 'react'

// Posiciones fijas (no aleatorias) para que la escena sea igual en cada visita y no cambie al volver a renderizar
const hojas = [
  { x: 6, tam: 18, dur: 14, retraso: 0, giro: 220 },
  { x: 18, tam: 12, dur: 18, retraso: 5, giro: -180 },
  { x: 31, tam: 22, dur: 16, retraso: 9, giro: 260 },
  { x: 44, tam: 14, dur: 20, retraso: 2, giro: -240 },
  { x: 57, tam: 26, dur: 15, retraso: 11, giro: 200 },
  { x: 68, tam: 12, dur: 19, retraso: 7, giro: -200 },
  { x: 79, tam: 20, dur: 17, retraso: 3, giro: 240 },
  { x: 90, tam: 15, dur: 21, retraso: 13, giro: -260 },
  { x: 12, tam: 10, dur: 22, retraso: 16, giro: 180 },
  { x: 50, tam: 11, dur: 23, retraso: 18, giro: -220 },
  { x: 74, tam: 9, dur: 24, retraso: 20, giro: 200 },
  { x: 96, tam: 13, dur: 18, retraso: 8, giro: -180 },
]

/** Hojas que caen meciéndose detrás del contenido. Decorativo: oculto para lectores de pantalla y quieto con movimiento reducido. */
export function HojasCayendo({ cantidad = hojas.length, className = '' }: { cantidad?: number; className?: string }) {
  return (
    <div className={`hojas-cayendo ${className}`} aria-hidden="true">
      {hojas.slice(0, cantidad).map((h, i) => (
        <span
          key={i}
          className={`hoja-cae ${i % 3 === 2 ? 'hoja-cae-clara' : ''}`}
          style={{ '--x': `${h.x}%`, '--tam': `${h.tam}px`, '--dur': `${h.dur}s`, '--retraso': `-${h.retraso}s`, '--giro': `${h.giro}deg` } as CSSProperties}
        >
          <svg viewBox="0 0 24 24"><path d="M2 22C2 10 9 2 22 2C22 14 14 22 2 22Z" /><path d="M2 22L16 8" className="hoja-cae-nervio" /></svg>
        </span>
      ))}
    </div>
  )
}
