import { type FormEvent, useCallback, useState } from 'react'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto, EsqueletoCifras } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoCandado, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { Buscador, Paginacion } from '../../shared/ui/Paginacion'
import { TAMANO_PAGINA, usePaginacion } from '../../shared/ui/paginado'
import {
  type EntradaBitacora, type Solicitud, type TipoSolicitud, nombreTipo, useAutorizaciones, useBitacora, useRegistrarSolicitud,
  useResponderSolicitud, useResumenAutorizaciones, useResumenPrivacidad, useSolicitudes, useVerificarBitacora,
} from './api'

type Pestana = 'bitacora' | 'autorizaciones' | 'solicitudes'
const hora = new Intl.DateTimeFormat('es-CO', { hour: '2-digit', minute: '2-digit' })
const dia = new Intl.DateTimeFormat('es-CO', { weekday: 'long', day: 'numeric', month: 'long' })
const fechaHora = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const fechaCorta = new Intl.DateTimeFormat('es-CO', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' })
const numero = new Intl.NumberFormat('es-CO')

/** Pantalla 9 · Privacidad y auditoría (historias 187 y 188). */
export function PrivacidadAuditoria() {
  const [pestana, setPestana] = useState<Pestana>('bitacora')
  const resumen = useResumenPrivacidad()
  const autorizaciones = useResumenAutorizaciones()
  const r = resumen.data
  const a = autorizaciones.data

  return (
    <>
      <header className="encabezado">
        <h1>Privacidad y auditoría</h1>
        <p>Todo lo que necesita para responder a un titular o a la Superintendencia de Industria y Comercio: quién autorizó, cuándo y qué hizo cada quien, incluida la IA.</p>
      </header>

      {!r || !a ? <EsqueletoCifras cantidad={4} /> : (
        <div className="rejilla">
          <div className="cifra"><span>Autorizaciones vigentes</span><b>{numero.format(a.vigentes)}</b><span>{a.revocadasEsteAnio} revocadas este año</span></div>
          <div className="cifra"><span>Política vigente</span><b className="mono">{a.versionVigente}</b><span>Se muestra antes de postularse</span></div>
          <div className={`cifra ${r.vencidas > 0 ? 'cifra-alerta' : ''}`}>
            <span>Solicitudes de titulares</span><b>{r.solicitudesAbiertas} abiertas</b>
            <span>{r.vencidas > 0 ? `${r.vencidas} fuera de plazo` : r.vencenEnTresDias > 0 ? `${r.vencenEnTresDias} vencen en 3 días hábiles o menos` : 'Todas dentro del plazo'}</span>
          </div>
          <div className="cifra"><span>Accesos a datos personales</span><b>{r.accesosSensiblesSieteDias}</b><span>Últimos 7 días</span></div>
        </div>
      )}

      <div role="tablist" aria-label="Privacidad y auditoría" className="pestanas">
        {([['bitacora', 'Bitácora'], ['autorizaciones', 'Autorizaciones Ley 1581'], ['solicitudes', `Solicitudes de titulares${r ? ` · ${r.solicitudesAbiertas}` : ''}`]] as [Pestana, string][]).map(([k, t]) => (
          <button key={k} type="button" role="tab" className="pestana" aria-selected={pestana === k} onClick={() => setPestana(k)}>{t}</button>
        ))}
      </div>

      {pestana === 'bitacora' && <Bitacora />}
      {pestana === 'autorizaciones' && <Autorizaciones />}
      {pestana === 'solicitudes' && <Solicitudes />}
    </>
  )
}

/* ---------- Bitácora ---------- */

const filtrosActor: [string, string][] = [['', 'Todo'], ['Ia', 'IA'], ['Persona', 'Personas'], ['Titular', 'Titulares'], ['Sistema', 'Sistema']]
const nombreActor: Record<EntradaBitacora['tipoActor'], string> = { Ia: 'IA', Persona: 'Persona', Sistema: 'Sistema', Titular: 'Titular' }

function Bitacora() {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const [tipo, setTipo] = useState('')
  const [buscar, setBuscar] = useState('')
  const [sensibles, setSensibles] = useState(false)
  const datos = useBitacora({ pagina, tamano, tipo, buscar, sensibles })
  const verificar = useVerificarBitacora()
  const alBuscar = useCallback((t: string) => { setBuscar(t); setPagina(1) }, [setPagina])

  // Agrupa por día (hora de Colombia en el navegador)
  const grupos = new Map<string, EntradaBitacora[]>()
  for (const e of datos.data?.items ?? []) {
    const d = dia.format(new Date(e.fecha))
    grupos.set(d, [...(grupos.get(d) ?? []), e])
  }

  return (
    <section className="tarjeta" aria-label="Bitácora">
      <div className="barra-filtros">
        <div className="segmentado" role="group" aria-label="Quién lo hizo">
          {filtrosActor.map(([v, t]) => <button key={v || 'todo'} type="button" aria-pressed={tipo === v} onClick={() => { setTipo(v); setPagina(1) }}>{t}</button>)}
        </div>
        <label className="interruptor"><input type="checkbox" checked={sensibles} onChange={(e) => { setSensibles(e.target.checked); setPagina(1) }} />Solo accesos a datos personales</label>
        <Buscador etiqueta="Buscar persona o acción" onBuscar={alBuscar} />
      </div>

      <div className="bitacora-cadena">
        <span className="ayuda-suave"><IconoEscudo />Registro inmutable: cada línea lleva la huella de la anterior.</span>
        <Boton pequeno cargando={verificar.isPending} textoCargando="Verificando…" onClick={() => verificar.mutate()}>Verificar integridad</Boton>
        {verificar.data && (
          <span className={`pill ${verificar.data.integra ? 'pill-ok' : 'pill-error'}`} role="status">
            {verificar.data.integra ? <IconoCheck /> : <IconoAlerta />}{verificar.data.mensaje}
          </span>
        )}
        {verificar.error && <span role="alert" className="error-formulario">{verificar.error.message}</span>}
      </div>

      {datos.isPending ? (
        <ul className="bitacora">{Array.from({ length: 6 }, (_, i) => (
          <li key={i} className="bitacora-linea"><Esqueleto ancho={44} /><Esqueleto ancho={90} alto={22} redondo /><div style={{ flex: 1 }}><Esqueleto ancho="60%" /><Esqueleto ancho="40%" alto={11} style={{ marginTop: 6 }} /></div></li>
        ))}</ul>
      ) : datos.data?.total === 0 ? (
        <p className="vacio-tabla">Aún no hay acciones con ese filtro. Cada cosa que hagan las personas, la IA o los titulares aparecerá aquí.</p>
      ) : (
        [...grupos].map(([d, entradas]) => (
          <div key={d} className="bitacora-dia">
            <h3>{d}</h3>
            <ul className="bitacora">
              {entradas.map((e) => (
                <li key={e.id} className={`bitacora-linea ${e.datoSensible ? 'sensible' : ''}`}>
                  <span className="mono bitacora-hora">{hora.format(new Date(e.fecha))}</span>
                  <span className={`actor actor-${e.tipoActor}`}>{e.tipoActor === 'Ia' && <Chispa />}{nombreActor[e.tipoActor]}</span>
                  <div className="bitacora-texto">
                    <b>{e.actor}</b> {e.accion}
                    {e.detalle && <span className="bitacora-detalle">{e.detalle}</span>}
                    {e.nota && <span className={`bitacora-nota ${e.datoSensible ? 'nota-sensible-texto' : ''}`}>{e.datoSensible && <IconoCandado />}{e.nota}</span>}
                  </div>
                  <span className="mono bitacora-huella" title={e.huella}>{e.huella.slice(0, 4)}…{e.huella.slice(-3)}</span>
                </li>
              ))}
            </ul>
          </div>
        ))
      )}
      {datos.data && <Paginacion pagina={datos.data} cargando={datos.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
    </section>
  )
}

/* ---------- Autorizaciones ---------- */

function Autorizaciones() {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const [buscar, setBuscar] = useState('')
  const [estado, setEstado] = useState('')
  const datos = useAutorizaciones({ pagina, tamano, buscar, estado })
  const alBuscar = useCallback((t: string) => { setBuscar(t); setPagina(1) }, [setPagina])

  return (
    <section className="tarjeta" aria-label="Autorizaciones Ley 1581">
      <div className="barra-filtros">
        <Buscador etiqueta="Buscar titular, cédula o número" onBuscar={alBuscar} />
        <div className="segmentado" role="group" aria-label="Estado">
          {[['', 'Todas'], ['Vigente', 'Vigentes'], ['Revocada', 'Revocadas']].map(([v, t]) => (
            <button key={v || 'todas'} type="button" aria-pressed={estado === v} onClick={() => { setEstado(v!); setPagina(1) }}>{t}</button>
          ))}
        </div>
      </div>
      <p className="ayuda-suave" style={{ padding: '0 var(--espacio-4)' }}>
        El texto de autorización se muestra antes de postularse, no se puede omitir y queda guardado con la versión que la persona aceptó. Las hojas de vida se guardan cifradas.
      </p>
      <div className="tabla-envoltura">
        <table className="tabla">
          <thead><tr><th>Titular</th><th>Canal</th><th>Versión</th><th>Fecha y hora</th><th>Estado</th><th>Evidencia</th></tr></thead>
          <tbody>
            {datos.isPending
              ? Array.from({ length: 6 }, (_, i) => <tr key={i}>{[60, 40, 20, 50, 30, 50].map((w, j) => <td key={j}><Esqueleto ancho={`${w}%`} /></td>)}</tr>)
              : datos.data?.items.map((x) => (
                  <tr key={x.postulacionId}>
                    <td><b>{x.titular}</b><span className="sub-celda mono">{x.documento}{x.vacante ? ` · ${x.vacante}` : ''}</span></td>
                    <td>{x.canal}</td>
                    <td className="mono">{x.version}</td>
                    <td className="mono" style={{ fontSize: 13 }}>{fechaHora.format(new Date(x.aceptadaEn))}</td>
                    <td><span className={`pill ${x.estado === 'Vigente' ? 'pill-ok' : 'pill-error'}`}>{x.estado}</span></td>
                    <td className="mono" style={{ fontSize: 12 }} title={x.huella}>{x.huella ? `sha256 ${x.huella.slice(0, 4)}…${x.huella.slice(-3)}` : 'Anterior a la huella'}</td>
                  </tr>
                ))}
          </tbody>
        </table>
      </div>
      {datos.data?.total === 0 && <p className="vacio-tabla">Aún no hay autorizaciones: llegan con cada postulación del portal.</p>}
      {datos.data && <Paginacion pagina={datos.data} cargando={datos.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
    </section>
  )
}

/* ---------- Solicitudes de titulares ---------- */

function Solicitudes() {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const [estado, setEstado] = useState('Abierta')
  const [registrando, setRegistrando] = useState(false)
  const datos = useSolicitudes({ pagina, tamano, estado })

  return (
    <section aria-label="Solicitudes de titulares" className="solicitudes">
      <div className="barra-filtros tarjeta">
        <div className="segmentado" role="group" aria-label="Estado">
          {[['Abierta', 'Abiertas'], ['Respondida', 'Respondidas'], ['', 'Todas']].map(([v, t]) => (
            <button key={v || 'todas'} type="button" aria-pressed={estado === v} onClick={() => { setEstado(v!); setPagina(1) }}>{t}</button>
          ))}
        </div>
        <span className="ayuda-suave">Consultas: 10 días hábiles. Actualización, supresión y revocatoria: 15 días hábiles (Ley 1581, arts. 14 y 15).</span>
        <Boton pequeno variante="primario" onClick={() => setRegistrando((x) => !x)}>{registrando ? 'Cerrar' : 'Registrar solicitud'}</Boton>
      </div>

      {registrando && <RegistrarSolicitud onListo={() => setRegistrando(false)} />}

      {datos.isPending
        ? Array.from({ length: 3 }, (_, i) => <div key={i} className="tarjeta solicitud"><Esqueleto ancho="40%" /><Esqueleto ancho="70%" /><Esqueleto alto={80} /></div>)
        : datos.data?.items.map((s) => <TarjetaSolicitud key={s.id} solicitud={s} />)}
      {datos.data?.total === 0 && (
        <div className="tarjeta vacio">
          <h2>{estado === 'Abierta' ? 'No hay solicitudes por responder' : 'No hay solicitudes'}</h2>
          <p>Los titulares piden desde el portal de empleo («Tus datos personales») o usted registra las que lleguen por correo, teléfono o en persona.</p>
        </div>
      )}
      {datos.data && datos.data.total > TAMANO_PAGINA && <div className="tarjeta"><Paginacion pagina={datos.data} cargando={datos.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} /></div>}
    </section>
  )
}

function Plazo({ s }: { s: Solicitud }) {
  if (s.estado === 'Respondida') return <span className="pill pill-ok"><IconoCheck />Respondida</span>
  const d = s.diasHabilesRestantes
  const clase = d < 0 ? 'pill-error' : d <= 3 ? 'pill-alerta' : 'pill-neutra'
  const texto = d < 0 ? `Vencida hace ${-d} ${-d === 1 ? 'día hábil' : 'días hábiles'}` : d === 0 ? 'Vence hoy' : `Quedan ${d} de ${s.plazoDiasHabiles} días hábiles`
  return <span className={`pill ${clase}`}>{texto}</span>
}

function TarjetaSolicitud({ solicitud: s }: { solicitud: Solicitud }) {
  const responder = useResponderSolicitud(s.id)
  const [respuesta, setRespuesta] = useState(s.borradorRespuesta)
  const [aprobar, setAprobar] = useState(true)
  const editada = respuesta.trim() !== s.borradorRespuesta.trim()
  const urgente = s.estado === 'Abierta' && s.diasHabilesRestantes <= 3

  return (
    <article className={`tarjeta solicitud ${urgente ? 'urgente' : ''}`}>
      <div className="solicitud-cabeza">
        <div>
          <b>{nombreTipo[s.tipo]} · {s.titular}</b>
          <span className="sub-celda mono">{s.documento} · {s.contacto}</span>
        </div>
        <Plazo s={s} />
      </div>
      {s.descripcion && <p className="solicitud-pide">«{s.descripcion}»</p>}
      <span className="sub-celda">Recibida el {fechaHora.format(new Date(s.recibidaEn))} por {s.canal} · vence el {fechaCorta.format(new Date(`${s.vence}T00:00:00Z`))}</span>

      {s.estado === 'Abierta' ? (
        <form className="formulario" onSubmit={(e: FormEvent) => { e.preventDefault(); responder.mutate({ respuesta, aprobarSupresion: s.esSupresion && aprobar }) }}>
          <label>
            <span className={editada ? '' : 'etiqueta-borrador'}>{editada ? 'Respuesta (editada por usted)' : 'Respuesta preparada con los datos que tenemos'}</span>
            <textarea className={`area-texto ${editada ? '' : 'borrador-preparado'}`} rows={4} value={respuesta} onChange={(e) => setRespuesta(e.target.value)} />
            <span className="ayuda">Verifique la identidad del titular antes de responder. La respuesta se le envía al contacto que dejó; mientras conectamos el correo y WhatsApp, cópiela y envíela usted.</span>
          </label>
          {s.esSupresion && (
            <label className="interruptor aviso-supresion">
              <input type="checkbox" checked={aprobar} onChange={(e) => setAprobar(e.target.checked)} />
              Aprobar la supresión: se borran su hoja de vida, sus datos y sus respuestas; queda solo la constancia de que revocó la autorización.
            </label>
          )}
          {responder.error && <span role="alert" className="error-formulario">{responder.error.message}</span>}
          <div className="decision" style={{ padding: '4px 0 0', borderTop: 0 }}>
            <Boton type="submit" variante="primario" icono={<IconoCheck />} cargando={responder.isPending} textoCargando="Respondiendo…" disabled={!respuesta.trim()}>
              {s.esSupresion && aprobar ? 'Responder y suprimir datos' : 'Marcar como respondida'}
            </Boton>
            <span className="registro">Se registran la respuesta y quién la dio</span>
          </div>
        </form>
      ) : (
        <div className="solicitud-respuesta">
          <span className="sub-celda">Respondida por {s.respondidaPor} el {s.respondidaEn && fechaHora.format(new Date(s.respondidaEn))}{s.supresionAprobada ? ' · datos suprimidos' : ''}</span>
          <p>{s.respuesta}</p>
        </div>
      )}
    </article>
  )
}

const tipos: TipoSolicitud[] = ['Consulta', 'Actualizacion', 'Supresion', 'Revocatoria']

function RegistrarSolicitud({ onListo }: { onListo: () => void }) {
  const registrar = useRegistrarSolicitud()
  const [tipo, setTipo] = useState<TipoSolicitud>('Consulta')
  const [titular, setTitular] = useState('')
  const [documento, setDocumento] = useState('')
  const [contacto, setContacto] = useState('')
  const [canal, setCanal] = useState('Correo')
  const [descripcion, setDescripcion] = useState('')

  return (
    <form className="tarjeta formulario solicitud" onSubmit={(e) => {
      e.preventDefault()
      registrar.mutate({ tipo, titular, documento, contacto, canal, descripcion }, { onSuccess: onListo })
    }}>
      <b>Registrar una solicitud recibida por otro canal</b>
      <div className="segmentado" role="group" aria-label="Qué pide">
        {tipos.map((t) => <button key={t} type="button" aria-pressed={tipo === t} onClick={() => setTipo(t)}>{nombreTipo[t]}</button>)}
      </div>
      <div className="dos-campos">
        <label>Titular<input className="entrada" value={titular} onChange={(e) => setTitular(e.target.value)} required /></label>
        <label>Cédula<input className="entrada mono" inputMode="numeric" value={documento} onChange={(e) => setDocumento(e.target.value)} required /></label>
        <label>Correo o celular para responder<input className="entrada" value={contacto} onChange={(e) => setContacto(e.target.value)} required /></label>
        <label>Llegó por
          <select className="entrada" value={canal} onChange={(e) => setCanal(e.target.value)}>
            {['Correo', 'Teléfono', 'WhatsApp', 'En persona', 'Carta'].map((c) => <option key={c}>{c}</option>)}
          </select>
        </label>
      </div>
      <label>Qué pide<textarea className="area-texto" rows={2} value={descripcion} onChange={(e) => setDescripcion(e.target.value)} /></label>
      <span className="ayuda">Selvana busca lo que tenemos de esa cédula y prepara la respuesta; el plazo legal se cuenta en días hábiles desde mañana.</span>
      {registrar.error && <span role="alert" className="error-formulario">{registrar.error.message}</span>}
      <span className="enlace-acciones">
        <Boton type="submit" variante="primario" cargando={registrar.isPending} textoCargando="Registrando…">Registrar</Boton>
        <Boton onClick={onListo} disabled={registrar.isPending}>Cancelar</Boton>
      </span>
    </form>
  )
}
