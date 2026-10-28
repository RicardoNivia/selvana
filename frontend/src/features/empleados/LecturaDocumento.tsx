import { useState } from 'react'
import { Boton } from '../../shared/ui/Boton'
import { Chispa, IconoAlerta, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { type Documento, type Empleado, legiblePorIa, useAplicarLectura, useDescartarLectura, usePedirLectura } from './api'

const fechaHora = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })

/**
 * Lo que leyó la IA del documento vigente: en violeta hasta que una persona decide. Los cambios los proponen las reglas
 * (EPS, pensión, ARL, caja, vencimiento); RRHH elige cuáles aplicar y puede corregir el valor. Nada cambia solo.
 */
export function LecturaDocumento({ e, d }: { e: Empleado; d: Documento }) {
  const v = d.versiones[0]
  const l = v?.lectura
  const pedir = usePedirLectura(e.id)
  const aplicar = useAplicarLectura(e.id)
  const descartar = useDescartarLectura(e.id)
  const [elegidos, setElegidos] = useState<Record<string, boolean>>({})
  const [valores, setValores] = useState<Record<string, string>>({})
  const error = pedir.error ?? aplicar.error ?? descartar.error
  if (!v || e.estado !== 'Activo') return null

  if (!l) {
    return legiblePorIa(v.nombreArchivo) ? (
      <div className="emp-lectura-pedir">
        <Boton pequeno variante="ia" icono={<Chispa />} cargando={pedir.isPending} textoCargando="Pidiendo…" onClick={() => pedir.mutate(d.id)}>Leer con IA</Boton>
        <span className="emp-sub">La IA lee el documento y propone actualizar el expediente; usted decide.</span>
        {error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{error.message}</p>}
      </div>
    ) : null
  }

  if (l.estado === 'Leyendo') {
    return <div className="estado-agente emp-lectura-estado" role="status"><span className="orbe trabajando" /><span>La IA está leyendo «{d.nombre}»…</span></div>
  }

  if (l.estado === 'Fallida') {
    return (
      <div className="emp-lectura-pedir">
        <p className="aviso aviso-alerta"><IconoAlerta />La IA no pudo leerlo: {l.error}. Revíselo a mano o pídalo de nuevo.</p>
        <Boton pequeno variante="ia" icono={<Chispa />} cargando={pedir.isPending} textoCargando="Pidiendo…" onClick={() => pedir.mutate(d.id)}>Leer de nuevo</Boton>
      </div>
    )
  }

  if (l.estado !== 'PorRevisar') {
    const texto = l.estado === 'SinCambios' ? 'Leída por la IA: el expediente ya coincide con el documento.'
      : l.estado === 'Aplicada' ? `Cambios aplicados por ${l.resueltaPor}${l.resueltaEn ? ` · ${fechaHora.format(new Date(l.resueltaEn))}` : ''}.`
      : `Propuesta descartada por ${l.resueltaPor}.`
    return <p className="emp-lectura-resuelta"><IconoCheck />{texto}</p>
  }

  const marcado = (clave: string) => elegidos[clave] ?? true
  const valor = (clave: string, propuesto: string) => valores[clave] ?? propuesto
  const confirmar = () => aplicar.mutate({
    documentoId: d.id,
    cambios: l.cambios.filter((c) => marcado(c.clave)).map((c) => ({ clave: c.clave, valor: valor(c.clave, c.propuesto) })),
  })

  return (
    <div className="bloque-ia emp-lectura">
      <div className="bloque-ia-cabeza"><Chispa />Lo que leyó la IA · {l.tipoDetectado}<span className="derecha">{l.modelo}</span></div>
      {l.razones.map((r) => (
        <div key={r.texto} className="razon"><span className={`signo ${r.signo === '+' ? 'mas' : 'menos'}`}>{r.signo}</span><span className="texto">{r.texto}</span><span className="cita">{r.fuente}</span></div>
      ))}
      {l.observaciones.map((o) => <div key={o} className="razon"><IconoAlerta className="emp-lectura-alerta" /><span className="texto">{o}</span></div>)}

      {l.cambios.length > 0 && (
        <ul className="emp-cambios" aria-label="Cambios propuestos">
          {l.cambios.map((c) => (
            <li key={c.clave} className={marcado(c.clave) ? 'activo' : ''}>
              <label className="emp-cambio-marca">
                <input type="checkbox" checked={marcado(c.clave)} onChange={(x) => setElegidos({ ...elegidos, [c.clave]: x.target.checked })} />
                <b>{c.etiqueta}</b>
              </label>
              <span className="emp-cambio-actual">{c.actual || '—'}</span>
              <span aria-hidden="true">→</span>
              <input className={`entrada ${valores[c.clave] === undefined ? 'entrada-ia' : ''}`} aria-label={`Nuevo valor de ${c.etiqueta}`} value={valor(c.clave, c.propuesto)}
                disabled={!marcado(c.clave)} onChange={(x) => setValores({ ...valores, [c.clave]: x.target.value })} />
              <span className="cita">{c.evidencia}</span>
            </li>
          ))}
        </ul>
      )}

      {error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{error.message}</p>}
      <div className="decision">
        <Boton variante="primario" cargando={aplicar.isPending} textoCargando="Aplicando…" onClick={confirmar}>
          {l.cambios.length === 0 ? 'Marcar revisado' : `Aplicar ${l.cambios.filter((c) => marcado(c.clave)).length} ${l.cambios.filter((c) => marcado(c.clave)).length === 1 ? 'cambio' : 'cambios'}`}
        </Boton>
        <Boton cargando={descartar.isPending} textoCargando="Descartando…" onClick={() => descartar.mutate(d.id)}>Descartar</Boton>
        <span className="registro"><IconoEscudo />Se registran la sugerencia y tu decisión</span>
      </div>
    </div>
  )
}
