import { type CSSProperties, useState } from 'react'
import { useParams } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoAlerta, IconoEscudo } from '../../shared/ui/Iconos'
import { useEncuestaPublica, useResponderEncuesta } from './api'
import './clima.css'

/** Encuesta de clima anónima por enlace (C1): sin nombre ni cédula; solo el área y la sede que la persona elige. */
export function EncuestaPublica() {
  const { token = '' } = useParams()
  const encuesta = useEncuestaPublica(token)
  const responder = useResponderEncuesta(token)
  const [area, setArea] = useState('')
  const [sede, setSede] = useState('')
  const [puntajes, setPuntajes] = useState<Record<string, number>>({})
  const [enps, setEnps] = useState<number | null>(null)
  const [comentario, setComentario] = useState('')

  if (encuesta.isPending) return <main className="cli-publica"><Esqueleto alto={40} ancho="70%" /><Esqueleto alto={120} /><Esqueleto alto={120} /></main>
  if (encuesta.isError) return <main className="cli-publica"><p role="alert" className="aviso aviso-alerta"><IconoAlerta />{encuesta.error.message}</p></main>
  if (responder.isSuccess) {
    return (
      <main className="cli-publica" role="status">
        <h1>¡Gracias por responder!</h1>
        <p className="postular-ayuda">Tu respuesta es anónima y ayuda a que {encuesta.data.empresa} mejore. Ya puedes cerrar esta página.</p>
      </main>
    )
  }

  const e = encuesta.data
  const completa = e.preguntas.every((p) => puntajes[p.id]) && (!e.incluyeEnps || enps !== null)

  return (
    <main className="cli-publica">
      <span className="mp-ceja">{e.empresa}</span>
      <h1>{e.titulo}</h1>
      <p className="postular-confianza"><IconoEscudo /><span>Es anónima: no te pedimos nombre ni cédula. Los resultados solo se ven por grupos de 5 o más personas.</span></p>

      {(e.areas.length > 0 || e.sedes.length > 0) && (
        <div className="cli-pregunta">
          <small>Para ver los resultados por grupo</small>
          {e.areas.length > 0 && <label className="campo">Tu área<select className="selector" value={area} onChange={(x) => setArea(x.target.value)}><option value="">Prefiero no decirlo</option>{e.areas.map((a) => <option key={a}>{a}</option>)}</select></label>}
          {e.sedes.length > 0 && <label className="campo">Tu sede<select className="selector" value={sede} onChange={(x) => setSede(x.target.value)}><option value="">Prefiero no decirlo</option>{e.sedes.map((s) => <option key={s}>{s}</option>)}</select></label>}
        </div>
      )}

      {e.preguntas.map((p, i) => (
        <div key={p.id} className="cli-pregunta" style={{ '--i': i } as CSSProperties}>
          <small>{p.dimension}</small>
          <span>{p.texto}</span>
          <div className="cli-escala" role="group" aria-label={p.texto}>
            {[1, 2, 3, 4, 5].map((n) => <button key={n} type="button" aria-pressed={puntajes[p.id] === n} onClick={() => setPuntajes((x) => ({ ...x, [p.id]: n }))}>{n}</button>)}
          </div>
          <div className="cli-escala-extremos"><span>Muy en desacuerdo</span><span>Muy de acuerdo</span></div>
        </div>
      ))}

      {e.incluyeEnps && (
        <div className="cli-pregunta">
          <small>Recomendación</small>
          <span>¿Qué tan probable es que recomiendes {e.empresa} como lugar de trabajo a un amigo?</span>
          <div className="cli-escala diez" role="group" aria-label="Recomendación">
            {Array.from({ length: 11 }, (_, n) => <button key={n} type="button" aria-pressed={enps === n} onClick={() => setEnps(n)}>{n}</button>)}
          </div>
          <div className="cli-escala-extremos"><span>Nada probable</span><span>Muy probable</span></div>
        </div>
      )}

      {e.preguntaAbierta && (
        <label className="cli-pregunta">
          <small>Opcional</small>
          <span>{e.preguntaAbierta}</span>
          <textarea className="area-texto" rows={4} maxLength={1000} value={comentario} onChange={(x) => setComentario(x.target.value)}
            placeholder="No escribas nombres: así tu comentario sigue siendo anónimo." />
        </label>
      )}

      {responder.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{responder.error.message}</p>}
      <Boton variante="primario" disabled={!completa} cargando={responder.isPending} textoCargando="Enviando…"
        onClick={() => responder.mutate({ area, sede, puntajes, enps, comentario })}>Enviar mis respuestas</Boton>
      {!completa && <span className="postular-ayuda">Responde todas las preguntas para enviar.</span>}
    </main>
  )
}
