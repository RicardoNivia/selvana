import { type FormEvent, useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoEscudo, IconoFlecha } from '../../shared/ui/Iconos'
import { useConversacionPublica, useEscribir } from './api'

const hora = new Intl.DateTimeFormat('es-CO', { hour: '2-digit', minute: '2-digit' })

/** Preselección del candidato (sin cuenta, con su enlace): conversa con el asistente desde el celular. */
export function ConversacionCandidato() {
  const { token = '' } = useParams()
  const conversacion = useConversacionPublica(token)
  const escribir = useEscribir(token)
  const [texto, setTexto] = useState('')
  const final = useRef<HTMLDivElement>(null)
  const cuantos = conversacion.data?.mensajes.length ?? 0

  useEffect(() => {
    final.current?.scrollIntoView({ behavior: 'smooth', block: 'end' })
  }, [cuantos, conversacion.data?.pensando])

  if (conversacion.isPending) {
    return <main className="portal-ancho chat-candidato"><Esqueleto alto={22} ancho="60%" /><Esqueleto alto={60} ancho="75%" /><Esqueleto alto={44} ancho="50%" style={{ alignSelf: 'flex-end' }} /></main>
  }
  if (conversacion.isError) {
    return <main className="portal-ancho postular"><div className="portal-vacio"><h2>Este enlace no sirve</h2><p>{conversacion.error.message}</p><Link to="/empleos" className="boton boton-secundario">Ver vacantes</Link></div></main>
  }

  const c = conversacion.data
  const cerrada = c.estado === 'Completada'
  const enviar = (e: FormEvent) => {
    e.preventDefault()
    const t = texto.trim()
    if (!t) return
    escribir.mutate(t, { onSuccess: () => setTexto('') })
  }

  return (
    <main className="portal-ancho chat-candidato">
      <header className="chat-cabeza">
        <span className="chat-avatar" aria-hidden="true">S</span>
        <div>
          <b>Asistente de selección · {c.empresa}</b>
          <span>{c.vacante} · {c.ciudad}</span>
        </div>
      </header>

      <div className="chat-mensajes" role="log" aria-live="polite" aria-label="Conversación">
        {c.mensajes.map((m, i) => (
          <div key={i} className={`chat-burbuja ${m.autor === 'Candidato' ? 'propia' : m.autor === 'Persona' ? 'persona' : 'agente'}`}>
            {m.autor === 'Persona' && <span className="chat-quien">{m.por ?? 'Equipo de selección'}</span>}
            <p>{m.texto}</p>
            <span className="chat-hora mono">{hora.format(new Date(m.en))}</span>
          </div>
        ))}
        {c.pensando && <div className="chat-burbuja agente chat-escribiendo" aria-label="El asistente está escribiendo"><span /><span /><span /></div>}
        <div ref={final} />
      </div>

      {c.estado === 'Escalada' && <p className="chat-aviso">Una persona del equipo de {c.empresa} lee tu conversación y te responde aquí mismo.</p>}
      {cerrada ? (
        <p className="chat-aviso">Gracias por tus respuestas. El equipo de {c.empresa} las revisará y te avisará cómo sigue tu proceso.</p>
      ) : (
        <form className="chat-escribir" onSubmit={enviar}>
          <label className="sr-only" htmlFor="mensaje">Tu mensaje</label>
          <input id="mensaje" className="entrada" value={texto} onChange={(e) => setTexto(e.target.value)} placeholder="Escribe tu respuesta" autoComplete="off"
            disabled={c.pensando || escribir.isPending} maxLength={1000} />
          <button type="submit" className="boton boton-primario" disabled={!texto.trim() || c.pensando || escribir.isPending} aria-busy={escribir.isPending || undefined}>
            {escribir.isPending ? <span className="indicador" aria-hidden="true" /> : <IconoFlecha />}
            <span className="sr-only">Enviar</span>
          </button>
        </form>
      )}
      {escribir.error && <p role="alert" className="error-formulario">{escribir.error.message}</p>}
      <p className="chat-privacidad"><IconoEscudo />Tus respuestas solo las ve {c.empresa} para tu proceso, según la autorización que aceptaste. Nunca te preguntaremos tu edad, estado civil ni dirección.</p>
    </main>
  )
}
