import { useState } from 'react'
import { Boton } from '../../shared/ui/Boton'
import { Chispa, IconoAlerta, IconoCheck } from '../../shared/ui/Iconos'
import { useConversacion, useInvitar, useResponder } from './api'

const pesos = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 })
const hora = new Intl.DateTimeFormat('es-CO', { dateStyle: 'short', timeStyle: 'short' })
const estados: Record<string, { texto: string; clase: string }> = {
  Invitada: { texto: 'Invitado, sin responder', clase: 'pill-neutra' },
  Abierta: { texto: 'Conversando con la IA', clase: 'pill-ia' },
  Completada: { texto: 'Preselección completa', clase: 'pill-ok' },
  Escalada: { texto: 'Espera a una persona', clase: 'pill-alerta' },
}

/** Preselección de una postulación en la consola: invitar, ver el resumen de la IA, leer la conversación y responder como persona. */
export function PreseleccionPostulante({ postulacionId }: { postulacionId: string }) {
  const conversacion = useConversacion(postulacionId)
  const invitar = useInvitar(postulacionId)
  const responder = useResponder(postulacionId)
  const [verTodo, setVerTodo] = useState(false)
  const [respuesta, setRespuesta] = useState('')
  const [copiado, setCopiado] = useState(false)

  const enlace = invitar.data ? `${window.location.origin}/preseleccion/${invitar.data.token}` : null
  const copiar = async () => {
    if (!enlace) return
    try {
      await navigator.clipboard.writeText(enlace)
      setCopiado(true)
    } catch {
      setCopiado(false)
    }
  }

  const bloqueEnlace = enlace && (
    <div className="presel-enlace">
      <span>Enlace de un solo uso para el candidato (un enlace nuevo invalida el anterior):</span>
      <code className="mono">{enlace}</code>
      <Boton pequeno icono={copiado ? <IconoCheck /> : undefined} onClick={copiar}>{copiado ? 'Copiado' : 'Copiar enlace'}</Boton>
      <small>Compártalo por WhatsApp o correo. El envío automático por WhatsApp llega con las integraciones.</small>
    </div>
  )

  if (conversacion.isPending) return <div className="presel"><span className="ayuda">Cargando la preselección…</span></div>
  if (conversacion.isError || !conversacion.data) {
    return (
      <div className="presel">
        <div className="presel-cabeza"><Chispa /><b>Preselección conversacional</b></div>
        <p className="sesgo-texto">El asistente confirma interés, disponibilidad, ubicación y aspiración salarial, hace las preguntas técnicas de la vacante y responde las dudas del candidato. Si el candidato lo pide, le responde una persona.</p>
        {bloqueEnlace ?? <Boton variante="ia" pequeno icono={<Chispa />} cargando={invitar.isPending} textoCargando="Creando enlace…" onClick={() => invitar.mutate()}>Invitar a preselección</Boton>}
        {invitar.error && <span role="alert" className="error-formulario">{invitar.error.message}</span>}
      </div>
    )
  }

  const c = conversacion.data
  const r = c.resumen
  const e = estados[c.estado] ?? estados.Abierta
  const mensajes = verTodo ? c.mensajes : c.mensajes.slice(-4)

  return (
    <div className="presel">
      <div className="presel-cabeza">
        <Chispa /><b>Preselección conversacional</b>
        <span className={`pill ${e.clase}`}>{e.texto}</span>
        {c.pensando && <span className="afinidad-calculando"><span className="orbe trabajando" />La IA está respondiendo</span>}
        <span className="cita" style={{ marginLeft: 'auto' }}>{c.modelo ?? 'sin respuestas aún'}</span>
      </div>
      {c.estado === 'Escalada' && c.motivoEscalamiento && <p className="aviso aviso-alerta"><IconoAlerta />{c.motivoEscalamiento}</p>}

      <div className="presel-resumen bloque-ia">
        <div className="bloque-ia-cabeza" style={{ fontSize: 13 }}><Chispa />Lo que entendió la IA</div>
        <dl>
          <div><dt>Interés</dt><dd>{r.interesado === null ? '—' : r.interesado ? 'Sí' : 'No'}</dd></div>
          <div><dt>Aspiración</dt><dd className="mono">{r.aspiracionSalarialCop ? pesos.format(r.aspiracionSalarialCop) : '—'}</dd></div>
          <div><dt>Disponibilidad</dt><dd>{r.disponibilidad || '—'}</dd></div>
          <div><dt>Ubicación</dt><dd>{r.ubicacion || '—'}</dd></div>
        </dl>
        {r.respuestas.length > 0 && <ul className="presel-respuestas">{r.respuestas.map((x) => <li key={x.pregunta}><b>{x.pregunta}</b> {x.respuesta}</li>)}</ul>}
        {r.dudas.length > 0 && <p className="presel-dudas">Preguntó: {r.dudas.join(' · ')}</p>}
        {r.texto && <p className="presel-texto">{r.texto}</p>}
        <span className="ficha-nota" style={{ padding: '0 var(--espacio-4) var(--espacio-3)' }}>No pregunta ni guarda edad, género, estado civil, hijos ni dirección.</span>
      </div>

      <div className="presel-chat">
        {c.mensajes.length > 4 && <button type="button" className="enlace-boton" onClick={() => setVerTodo((x) => !x)}>{verTodo ? 'Ver solo lo último' : `Ver los ${c.mensajes.length} mensajes`}</button>}
        {mensajes.map((m, i) => (
          <div key={i} className={`presel-mensaje ${m.autor.toLowerCase()}`}>
            <span>{m.autor === 'Agente' ? 'IA' : m.autor === 'Persona' ? (m.por ?? 'Persona') : 'Candidato'} · <span className="mono">{hora.format(new Date(m.en))}</span></span>
            <p>{m.texto}</p>
          </div>
        ))}
      </div>

      {c.estado !== 'Completada' && (
        <form className="formulario presel-responder" onSubmit={(ev) => { ev.preventDefault(); responder.mutate({ conversacionId: c.id, texto: respuesta.trim(), cerrar: false }, { onSuccess: () => setRespuesta('') }) }}>
          <label>Responder como persona<input className="entrada" value={respuesta} onChange={(ev) => setRespuesta(ev.target.value)} placeholder="Hola, soy del equipo de selección…" /></label>
          <div className="decision" style={{ padding: 0, borderTop: 0 }}>
            <Boton type="submit" variante="primario" disabled={!respuesta.trim()} cargando={responder.isPending} textoCargando="Enviando…">Enviar respuesta</Boton>
            <Boton disabled={!respuesta.trim() || responder.isPending} onClick={() => responder.mutate({ conversacionId: c.id, texto: respuesta.trim(), cerrar: true }, { onSuccess: () => setRespuesta('') })}>Enviar y cerrar</Boton>
            <span className="registro">Queda en la conversación y en la bitácora</span>
          </div>
          {responder.error && <span role="alert" className="error-formulario">{responder.error.message}</span>}
        </form>
      )}
      {bloqueEnlace ?? <button type="button" className="enlace-boton" onClick={() => invitar.mutate()}>Generar un enlace nuevo para el candidato</button>}
    </div>
  )
}
