import { IconoAlerta, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { type Alerta, type CondicionesRequest, pesos, type TipoContrato, tiposContrato } from './api'

const conFin: TipoContrato[] = ['TerminoFijo', 'MisionTemporal', 'Aprendizaje', 'ObraOLabor']
const conLabor: TipoContrato[] = ['ObraOLabor', 'MisionTemporal']

/** Alertas por reglas: en rojo lo que impide aprobar, en ámbar lo que conviene revisar. */
export function AlertasContrato({ alertas, cargando }: { alertas: Alerta[]; cargando?: boolean }) {
  const orden = { Bloquea: 0, Revisar: 1, Info: 2 }
  return (
    <ul className="vin-alertas" aria-live="polite" aria-busy={cargando || undefined}>
      {[...alertas].sort((a, b) => orden[a.nivel] - orden[b.nivel]).map((a) => (
        <li key={a.texto} className={`vin-alerta ${a.nivel.toLowerCase()}`}>
          {a.nivel === 'Info' ? <IconoEscudo /> : <IconoAlerta />}
          <span>{a.texto}</span>
          <span className="cita">{a.fuente}</span>
        </li>
      ))}
      {alertas.every((a) => a.nivel === 'Info') && <li className="vin-alerta ok"><IconoCheck /><span>Las condiciones cumplen las reglas que revisa Selvana.</span></li>}
    </ul>
  )
}

/** Editor de las condiciones de la contratación. Cambiar el tipo ajusta lo que aplica (fin, labor). */
export function CondicionesFormulario({ valor, onCambiar, empresaUsuaria }: {
  valor: CondicionesRequest
  onCambiar: (c: CondicionesRequest) => void
  empresaUsuaria: string | null
}) {
  const poner = (c: Partial<CondicionesRequest>) => onCambiar({ ...valor, ...c })
  return (
    <div className="vin-condiciones">
      <label>
        Tipo de contrato
        <select className="selector" value={valor.tipo} onChange={(e) => {
          const tipo = e.target.value as TipoContrato
          poner({ tipo, fechaFin: tipo === 'Indefinido' ? null : valor.fechaFin, labor: conLabor.includes(tipo) ? valor.labor : null })
        }}>
          {tiposContrato.map(([v, t]) => <option key={v} value={v}>{t}</option>)}
        </select>
      </label>
      <label>
        Salario mensual
        <input className="entrada mono" inputMode="numeric" value={valor.salarioCop ? pesos.format(valor.salarioCop) : ''}
          onChange={(e) => poner({ salarioCop: Number(e.target.value.replace(/\D/g, '')) || 0 })} />
      </label>
      <label>
        Fecha de ingreso
        <input className="entrada" type="date" value={valor.fechaIngreso} onChange={(e) => poner({ fechaIngreso: e.target.value })} />
      </label>
      {conFin.includes(valor.tipo) && (
        <label>
          Fecha de terminación {valor.tipo === 'ObraOLabor' && <span className="ayuda">(opcional: termina con la labor)</span>}
          <input className="entrada" type="date" value={valor.fechaFin ?? ''} onChange={(e) => poner({ fechaFin: e.target.value || null })} />
        </label>
      )}
      <label>
        Periodo de prueba (días)
        <input className="entrada mono" type="number" min={0} max={60} value={valor.periodoPruebaDias}
          onChange={(e) => poner({ periodoPruebaDias: Math.max(0, Number(e.target.value) || 0) })} />
      </label>
      {conLabor.includes(valor.tipo) && (
        <label className="vin-ancho">
          Obra o labor contratada{valor.tipo === 'MisionTemporal' && empresaUsuaria ? ` en ${empresaUsuaria}` : ''}
          <textarea className="area-texto" rows={2} value={valor.labor ?? ''} onChange={(e) => poner({ labor: e.target.value })}
            placeholder="Operar montacargas en el centro de distribución durante la temporada" />
        </label>
      )}
    </div>
  )
}
