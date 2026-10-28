import { Link } from 'react-router-dom'
import { IconoAlerta, IconoCheck, IconoDocumento, IconoEscudo } from '../../shared/ui/Iconos'
import { type Alerta, type CondicionesRequest, pesos, type TipoContrato, tiposContrato, useTiposContrato } from './api'

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
  const maestro = useTiposContrato(true)
  const tipos = maestro.data ?? []
  // El tipo del maestro elegido; si las condiciones solo traen la modalidad, el primero activo de esa modalidad
  const elegido = tipos.find((t) => t.id === valor.tipoContratoId) ?? tipos.find((t) => t.modalidad === valor.tipo)
  const cambiarTipo = (tipo: TipoContrato, tipoContratoId: string | null) =>
    poner({ tipo, tipoContratoId, fechaFin: tipo === 'Indefinido' ? null : valor.fechaFin, labor: conLabor.includes(tipo) ? valor.labor : null })

  return (
    <div className="vin-condiciones">
      <label>
        Tipo de contrato
        {tipos.length > 0 ? (
          <select className="selector" value={elegido?.id ?? ''} onChange={(e) => {
            const t = tipos.find((x) => x.id === e.target.value)
            if (t) cambiarTipo(t.modalidad, t.id)
          }}>
            {!elegido && <option value="">Elija un tipo</option>}
            {tipos.map((t) => <option key={t.id} value={t.id}>{t.nombre}</option>)}
          </select>
        ) : (
          <select className="selector" value={valor.tipo} onChange={(e) => cambiarTipo(e.target.value as TipoContrato, null)}>
            {tiposContrato.map(([v, t]) => <option key={v} value={v}>{t}</option>)}
          </select>
        )}
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
      {elegido && (
        <div className="vin-ancho vin-tipo-como">
          <span className="vin-tipo-como-titulo">
            Cómo se trabaja este contrato
            {elegido.plantillaNombre && <span className="pill pill-neutra"><IconoDocumento />Con plantilla</span>}
            <Link to="/consola/empresa/tipos-contrato">Editar</Link>
          </span>
          <p>{elegido.descripcion}</p>
          <span className="ayuda">La IA sigue esta descripción{elegido.plantillaNombre ? ' y la plantilla' : ''} al redactar la oferta y el contrato.</span>
        </div>
      )}
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
