import { type CSSProperties, useState } from 'react'
import { Paginacion } from '../../shared/ui/Paginacion'
import { TAMANOS_BANDEJA, usePaginacion } from '../../shared/ui/paginado'
import { Link } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoCheck } from '../../shared/ui/Iconos'
import { useSesion } from '../sesion/api'
import { type Propuesta, useActividadAgentes, useBandeja, useDecidir, useDescartar, useResumenBandeja } from './api'

const relativo = new Intl.RelativeTimeFormat('es-CO', { numeric: 'auto' })
const fechaCorta = new Intl.DateTimeFormat('es-CO', { day: 'numeric', month: 'short', timeZone: 'UTC' })
const hora = new Intl.DateTimeFormat('es-CO', { hour: '2-digit', minute: '2-digit' })

function haceCuanto(iso: string) {
  const min = Math.round((Date.now() - new Date(iso).getTime()) / 60_000)
  if (min < 60) return relativo.format(-Math.max(1, min), 'minute')
  if (min < 60 * 24) return relativo.format(-Math.round(min / 60), 'hour')
  return relativo.format(-Math.round(min / 1440), 'day')
}

function saludo(nombre: string) {
  const h = new Date().getHours()
  return `${h < 12 ? 'Buenos días' : h < 19 ? 'Buenas tardes' : 'Buenas noches'}, ${nombre.split(' ')[0]}`
}

/** Etiqueta y color de cada decisión. El violeta solo para lo que propuso la IA y nadie ha decidido. */
function etiqueta(p: Propuesta): { texto: string; clase: string } {
  if (p.estado === 'Resuelta') return { texto: 'Decidida', clase: 'ok' }
  if (p.estado === 'Descartada') return { texto: 'Descartada', clase: 'neutra' }
  if (p.urgente) return { texto: 'Urgente', clase: 'alerta' }
  if (p.afectaPersona) return { texto: 'Afecta personas', clase: 'error' }
  if (p.agente) return { texto: 'Sugerencia IA', clase: 'ia' }
  return { texto: 'Por decidir', clase: 'neutra' }
}

/** Pantalla 1 · Bandeja de aprobaciones (historia 203). Todo lo que la IA dejó listo, con sus razones; la persona decide. */
export function Bandeja() {
  const { data: sesion } = useSesion()
  const [estado, setEstado] = useState<'Pendiente' | 'Resuelta'>('Pendiente')
  const { pagina, setPagina, tamano, setTamano } = usePaginacion(TAMANOS_BANDEJA[0])
  const [elegida, setElegida] = useState<string>()
  const lista = useBandeja(estado, pagina, tamano)
  const resumen = useResumenBandeja()
  const items = lista.data?.items ?? []
  const actual = items.find((p) => p.id === elegida) ?? items[0]

  return (
    <>
      <header className="encabezado bandeja-encabezado">
        <div>
          <h1>{sesion ? saludo(sesion.nombre) : <Esqueleto ancho={260} alto={32} />}</h1>
          <p>
            {resumen.data
              ? resumen.data.pendientes === 0
                ? 'No hay decisiones pendientes. Cuando un agente termine algo, aparece aquí con sus razones.'
                : `Los agentes dejaron ${resumen.data.pendientes} ${resumen.data.pendientes === 1 ? 'decisión lista' : 'decisiones listas'}${resumen.data.urgentes > 0 ? `. ${resumen.data.urgentes} ${resumen.data.urgentes === 1 ? 'es urgente' : 'son urgentes'}` : ''}.`
              : <Esqueleto ancho={380} />}
          </p>
        </div>
      </header>

      <Agentes />

      <div className="bandeja">
        <section className="tarjeta bandeja-lista" aria-label="Decisiones">
          <div className="bandeja-lista-cabeza">
            <b>Bandeja de aprobaciones</b>
            <div className="segmentado" role="group" aria-label="Estado">
              <button type="button" aria-pressed={estado === 'Pendiente'} onClick={() => { setEstado('Pendiente'); setPagina(1); setElegida(undefined) }}>
                Pendientes{resumen.data ? ` · ${resumen.data.pendientes}` : ''}
              </button>
              <button type="button" aria-pressed={estado === 'Resuelta'} onClick={() => { setEstado('Resuelta'); setPagina(1); setElegida(undefined) }}>Decididas</button>
            </div>
          </div>
          {lista.isPending ? (
            <ul className="bandeja-items">{Array.from({ length: 4 }, (_, i) => <li key={i} className="bandeja-item"><Esqueleto ancho="70%" /><Esqueleto ancho="50%" alto={11} /></li>)}</ul>
          ) : items.length === 0 ? (
            <div className="bandeja-vacia">
              <span className="bandeja-vacia-icono"><IconoCheck /></span>
              <b>{estado === 'Pendiente' ? 'Todo al día' : 'Aún no hay decisiones tomadas'}</b>
              <span>{estado === 'Pendiente' ? 'Pida una vacante en una frase o suba la nómina: los agentes dejarán aquí lo que preparen.' : 'Lo que apruebe o descarte queda aquí y en la bitácora.'}</span>
              {estado === 'Pendiente' && <Link to="/consola/vacantes" className="boton boton-secundario boton-pequeno">Ir a vacantes</Link>}
            </div>
          ) : (
            <ul className="bandeja-items">
              {items.map((p, i) => {
                const e = etiqueta(p)
                return (
                  <li key={p.id} style={{ '--i': i } as CSSProperties}>
                    <button type="button" className={`bandeja-item franja-${e.clase}`} aria-current={actual?.id === p.id || undefined} onClick={() => setElegida(p.id)}>
                      <b>{p.titulo}</b>
                      <span className="bandeja-item-desc">{p.descripcion}</span>
                      <span className="bandeja-item-pie">
                        <span className={`pill pill-${e.clase}`}>{e.clase === 'ia' && <Chispa />}{e.texto}</span>
                        <span className="mono">{p.venceEl && p.estado === 'Pendiente' ? `vence ${fechaCorta.format(new Date(`${p.venceEl}T00:00:00Z`))}` : haceCuanto(p.creadaEn)}</span>
                      </span>
                    </button>
                  </li>
                )
              })}
            </ul>
          )}
          {lista.data && lista.data.total > TAMANOS_BANDEJA[0] && (
            <Paginacion pagina={lista.data} cargando={lista.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} tamanos={TAMANOS_BANDEJA} />
          )}
        </section>

        {actual ? <Detalle key={actual.id} propuesta={actual} /> : lista.isPending ? <div className="tarjeta panel-decision bandeja-detalle"><Esqueleto alto={28} ancho="60%" /><Esqueleto alto={160} /></div> : null}
      </div>
    </>
  )
}

function Agentes() {
  const actividad = useActividadAgentes()
  if (actividad.isPending) {
    return <div className="agentes-hoy">{[0, 1, 2].map((i) => <div key={i} className="tarjeta agente-hoy"><Esqueleto ancho="60%" /><Esqueleto ancho="80%" alto={11} /></div>)}</div>
  }
  if (!actividad.data || actividad.data.length === 0) {
    return <p className="agentes-quietos"><Chispa />Los agentes aún no han trabajado hoy.</p>
  }
  return (
    <div className="agentes-hoy" aria-label="Lo que hicieron hoy los agentes">
      {actividad.data.slice(0, 4).map((a, i) => (
        <div key={a.agente} className="tarjeta agente-hoy" style={{ '--i': i } as CSSProperties}>
          <span className="agente-hoy-cabeza">
            <span className={`orbe ${actividad.dataUpdatedAt - new Date(a.ultimaVez).getTime() < 120_000 ? 'trabajando' : ''}`} />
            <b>{a.agente}</b>
          </span>
          <span className="agente-hoy-ultima">{a.ultimaAccion}</span>
          <span className="agente-hoy-pie mono">
            {a.accionesHoy} {a.accionesHoy === 1 ? 'acción' : 'acciones'} hoy · {hora.format(new Date(a.ultimaVez))}
            {a.esperanAprobacion > 0 && <span className="pill pill-ia">{a.esperanAprobacion} por aprobar</span>}
          </span>
        </div>
      ))}
    </div>
  )
}

function Detalle({ propuesta: p }: { propuesta: Propuesta }) {
  const decidir = useDecidir()
  const descartar = useDescartar()
  const [descartando, setDescartando] = useState(false)
  const [motivo, setMotivo] = useState('')
  const [hecho, setHecho] = useState<string>()
  const e = etiqueta(p)
  const pendiente = p.estado === 'Pendiente' && !hecho

  return (
    <article className="tarjeta panel-decision bandeja-detalle" aria-label={p.titulo}>
      <div className="bandeja-detalle-cabeza">
        <span className={`pill pill-${e.clase}`}>{e.clase === 'ia' && <Chispa />}{hecho ? 'Decidida' : e.texto}</span>
        <h2>{p.titulo}</h2>
        <p>{p.descripcion}</p>
        {p.agente && <span className="sub-celda"><Chispa /> Preparado por {p.agente}{p.modelo ? ` · ${p.modelo}` : ''}</span>}
      </div>

      {p.campos.length > 0 && (
        <dl className="bandeja-campos">
          {p.campos.filter((c) => c.valor).map((c) => (
            <div key={c.etiqueta} className={p.agente && pendiente ? 'ia' : ''}>
              <dt>{c.etiqueta}</dt>
              <dd>{c.valor}</dd>
            </div>
          ))}
        </dl>
      )}

      {p.razones.length > 0 && (
        <div className={`${p.agente && pendiente ? 'bloque-ia' : 'bloque-razones'} bandeja-razones`}>
          <div className="bloque-ia-cabeza">{p.agente && <Chispa />}{p.agente ? 'Por qué la IA lo propone' : 'Por qué está aquí'}</div>
          {p.razones.map((r, i) => (
            <div key={i} className="razon">
              <span className={`signo ${r.signo === '−' || r.signo === '-' ? 'menos' : 'mas'}`}>{r.signo === '-' ? '−' : r.signo}</span>
              <span>{r.texto}</span>
              <span className="cita">{r.fuente}</span>
            </div>
          ))}
        </div>
      )}

      {p.afectaPersona && pendiente && (
        <p className="aviso aviso-alerta"><IconoAlerta />Afecta a una persona: siempre requiere su aprobación, en cualquier nivel de autonomía.</p>
      )}

      {hecho && <p className="aviso aviso-ok" role="status"><IconoCheck />{hecho}</p>}
      {!pendiente && !hecho && (
        <p className="aviso aviso-ok" role="status"><IconoCheck />{p.decision}{p.decididaPor ? ` · ${p.decididaPor}` : ''}</p>
      )}

      {pendiente && descartando && (
        <form className="formulario bandeja-descartar" onSubmit={(ev) => {
          ev.preventDefault()
          descartar.mutate({ id: p.id, motivo }, { onSuccess: () => setHecho('Sugerencia descartada. El motivo queda registrado para mejorar al agente.') })
        }}>
          <label>Por qué la descarta<input className="entrada" value={motivo} onChange={(ev) => setMotivo(ev.target.value)} autoFocus placeholder="Ej.: el salario está por debajo del mercado" /></label>
          {descartar.error && <span role="alert" className="error-formulario">{descartar.error.message}</span>}
          <span className="enlace-acciones">
            <Boton type="submit" cargando={descartar.isPending} textoCargando="Descartando…" disabled={!motivo.trim()}>Descartar sugerencia</Boton>
            <Boton onClick={() => setDescartando(false)}>Cancelar</Boton>
          </span>
        </form>
      )}

      {decidir.error && <span role="alert" className="error-formulario">{decidir.error.message}</span>}
      {pendiente && !descartando && (
        <div className="decision">
          {p.accion && (
            <Boton variante="primario" icono={<IconoCheck />} cargando={decidir.isPending} textoCargando="Aplicando…"
              onClick={() => decidir.mutate(p, { onSuccess: () => setHecho(`${p.accion!.etiqueta}: hecho. Queda registrado con su nombre.`) })}>
              {p.accion.etiqueta}
            </Boton>
          )}
          <Link to={p.revisar} className={`boton ${p.accion ? 'boton-secundario' : 'boton-primario'}`}>{p.accion ? 'Ajustar' : 'Revisar y decidir'}</Link>
          {p.agente && <Boton onClick={() => setDescartando(true)}>Descartar</Boton>}
          <span className="registro">Se registran la sugerencia y su decisión</span>
        </div>
      )}
    </article>
  )
}
