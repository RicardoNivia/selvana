import { useState } from 'react'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa, IconoCandado, IconoCheck } from '../../shared/ui/Iconos'
import { type PruebaEnBateria, useSuite } from './api'

/**
 * Elegir qué pruebas aplica un cargo o una vacante, con su peso y su puntaje de corte. Si la batería es sugerida
 * se muestra en violeta hasta que una persona la guarda.
 */
export function EditorBateria({ inicial, sugerida, razon, guardando, error, onGuardar, etiquetaGuardar = 'Guardar pruebas' }: {
  inicial: PruebaEnBateria[]
  sugerida?: boolean
  razon?: string | null
  guardando: boolean
  error?: string
  onGuardar: (pruebas: PruebaEnBateria[]) => void
  etiquetaGuardar?: string
}) {
  const suite = useSuite()
  const [pruebas, setPruebas] = useState<PruebaEnBateria[]>(inicial)
  const [tocada, setTocada] = useState(false)
  const ia = sugerida && !tocada

  const cambiar = (codigo: string, cambio: Partial<PruebaEnBateria> | null) => {
    setTocada(true)
    setPruebas((ps) => {
      if (cambio === null) return ps.filter((p) => p.codigo !== codigo)
      const existe = ps.some((p) => p.codigo === codigo)
      return existe ? ps.map((p) => (p.codigo === codigo ? { ...p, ...cambio } : p)) : [...ps, { codigo, peso: 1, corte: null, ...cambio }]
    })
  }

  if (suite.isPending) return <div className="bateria"><Esqueleto alto={44} /><Esqueleto alto={44} /><Esqueleto alto={44} /></div>

  return (
    <div className={`bateria ${ia ? 'bateria-ia' : ''}`}>
      {ia && razon && <p className="bateria-sugerencia"><Chispa />Sugerida por Selvana: {razon} Ajústela y guárdela para confirmarla.</p>}
      <ul className="bateria-lista">
        {suite.data?.map((s) => {
          const p = pruebas.find((x) => x.codigo === s.codigo)
          const personalidad = s.tipo === 'Personalidad'
          return (
            <li key={s.codigo} className={p ? 'elegida' : ''}>
              <label className="bateria-prueba">
                <input type="checkbox" checked={Boolean(p)} onChange={(e) => cambiar(s.codigo, e.target.checked ? {} : null)} />
                <span>
                  <b>{s.nombre}</b>
                  <span className="sub-celda">{s.mide} · {s.preguntas} preguntas · {s.minutos} min</span>
                  <span className="bateria-etiquetas">
                    {!s.validada && <span className="pill pill-alerta">En validación</span>}
                    {s.sensible && <span className="pill pill-neutra"><IconoCandado />Dato sensible</span>}
                  </span>
                </span>
              </label>
              {p && (
                <span className="bateria-ajustes">
                  <label>Peso
                    <input className="entrada mono" type="number" min={1} max={10} value={p.peso} onChange={(e) => cambiar(s.codigo, { peso: Number(e.target.value) || 1 })} />
                  </label>
                  {personalidad ? <span className="sub-celda">Sin corte: no hay rasgos buenos o malos</span> : (
                    <label>Corte
                      <input className="entrada mono" type="number" min={0} max={100} placeholder="—" value={p.corte ?? ''}
                        onChange={(e) => cambiar(s.codigo, { corte: e.target.value === '' ? null : Math.min(100, Math.max(0, Number(e.target.value))) })} />
                    </label>
                  )}
                </span>
              )}
            </li>
          )
        })}
      </ul>
      <p className="ayuda-suave">Las pruebas en validación se aplican y se muestran, pero su puntaje es provisional hasta que el psicólogo las valide (ADR 0011). Nadie se descarta solo por una prueba.</p>
      {error && <span role="alert" className="error-formulario">{error}</span>}
      <span className="enlace-acciones">
        <Boton variante="primario" pequeno icono={<IconoCheck />} cargando={guardando} textoCargando="Guardando…" disabled={pruebas.length === 0}
          onClick={() => onGuardar(pruebas)}>
          {etiquetaGuardar}
        </Boton>
      </span>
    </div>
  )
}
