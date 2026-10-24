import { useEffect, useState } from 'react'
import { TAMANOS_PAGINA } from './paginado'

export interface Pagina<T> {
  items: T[]
  numero: number
  tamano: number
  total: number
}

const numero = new Intl.NumberFormat('es-CO')

/** Norma: toda tabla que pueda crecer pagina en el servidor y muestra el rango ("1–10 de 954"). */
export function Paginacion({ pagina, cargando, onCambiar, onCambiarTamano, tamanos = TAMANOS_PAGINA }: {
  pagina: Pagina<unknown>
  cargando?: boolean
  onCambiar: (n: number) => void
  onCambiarTamano?: (tamano: number) => void
  tamanos?: readonly number[]
}) {
  const totalPaginas = Math.max(1, Math.ceil(pagina.total / pagina.tamano))
  const desde = pagina.total === 0 ? 0 : (pagina.numero - 1) * pagina.tamano + 1
  const hasta = Math.min(pagina.numero * pagina.tamano, pagina.total)

  return (
    <nav className="paginacion" aria-label="Paginación">
      <span className="mono">
        {numero.format(desde)}–{numero.format(hasta)} de {numero.format(pagina.total)}
      </span>
      {onCambiarTamano && (
        <label className="paginacion-tamano">
          <select className="entrada entrada-pequena" value={pagina.tamano} disabled={cargando} onChange={(e) => onCambiarTamano(Number(e.target.value))} aria-label="Registros por página">
            {tamanos.map((t) => <option key={t} value={t}>{t}</option>)}
          </select>
          por página
        </label>
      )}
      {cargando && <span className="indicador indicador-oscuro" aria-label="Cargando página" />}
      <span className="acciones">
        <button type="button" className="boton boton-secundario boton-pequeno" disabled={pagina.numero <= 1 || cargando} onClick={() => onCambiar(pagina.numero - 1)}>
          Anterior
        </button>
        <span className="mono" aria-live="polite">
          {pagina.numero} / {totalPaginas}
        </span>
        <button type="button" className="boton boton-secundario boton-pequeno" disabled={pagina.numero >= totalPaginas || cargando} onClick={() => onCambiar(pagina.numero + 1)}>
          Siguiente
        </button>
      </span>
    </nav>
  )
}

/** Buscador con espera de 300 ms para no llamar a la API en cada tecla. */
export function Buscador({ etiqueta, onBuscar }: { etiqueta: string; onBuscar: (texto: string) => void }) {
  const [texto, setTexto] = useState('')
  useEffect(() => {
    const t = setTimeout(() => onBuscar(texto.trim()), 300)
    return () => clearTimeout(t)
  }, [texto, onBuscar])

  return (
    <input
      type="search"
      className="entrada buscador"
      placeholder={etiqueta}
      aria-label={etiqueta}
      value={texto}
      onChange={(e) => setTexto(e.target.value)}
    />
  )
}
