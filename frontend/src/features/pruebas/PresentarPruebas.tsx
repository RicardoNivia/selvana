import { type CSSProperties, useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoAlerta, IconoCheck, IconoEscudo, IconoFlecha, IconoReloj } from '../../shared/ui/Iconos'
import { type PreguntasPrueba, useAplicacion, useConsentimientoPruebas, useIniciarPrueba, useTerminarPrueba } from './api'

const fechaHora = new Intl.DateTimeFormat('es-CO', { weekday: 'long', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' })
const v = (i: number) => ({ '--i': i }) as CSSProperties

/** Mezcla las preguntas para que cada candidato las vea en otro orden (se hace al abrir la prueba, no al dibujar). */
function mezclar<T>(lista: T[]): T[] {
  const copia = [...lista]
  for (let i = copia.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[copia[i], copia[j]] = [copia[j]!, copia[i]!]
  }
  return copia
}

/** Pantalla del candidato (sin cuenta, con su enlace): consentimiento, lista de pruebas y presentación una por una. */
export function PresentarPruebas() {
  const { token = '' } = useParams()
  const aplicacion = useAplicacion(token)
  const consentir = useConsentimientoPruebas(token)
  const iniciar = useIniciarPrueba(token)
  const [acepta, setAcepta] = useState(false)
  const [enCurso, setEnCurso] = useState<PreguntasPrueba>()

  if (aplicacion.isPending) {
    return <main className="portal-ancho postular"><Esqueleto alto={28} ancho="70%" /><Esqueleto alto={16} ancho="90%" /><Esqueleto alto={180} /></main>
  }
  if (aplicacion.isError) {
    return <main className="portal-ancho postular"><div className="portal-vacio"><h2>Este enlace no sirve</h2><p>{aplicacion.error.message}</p><Link to="/empleos" className="boton boton-secundario">Ver vacantes</Link></div></main>
  }

  const a = aplicacion.data
  if (enCurso) {
    return <Presentando token={token} prueba={enCurso} onTerminar={() => setEnCurso(undefined)} />
  }

  if (a.vencida) {
    return <main className="portal-ancho postular"><div className="portal-vacio"><h2>El plazo terminó</h2><p>Las pruebas para {a.vacante} vencieron el {fechaHora.format(new Date(a.venceEn))}. Si aún te interesa, escríbele a {a.empresa}.</p></div></main>
  }
  if (a.estado === 'Rechazada') {
    return <main className="portal-ancho postular"><div className="portal-vacio"><h2>Decidiste no presentar las pruebas</h2><p>Respetamos tu decisión. Si cambias de opinión, pídele a {a.empresa} un enlace nuevo.</p></div></main>
  }

  const minutos = a.pruebas.reduce((s, p) => s + p.minutos, 0)
  const terminadas = a.pruebas.filter((p) => p.estado === 'Terminada').length
  const todas = terminadas === a.pruebas.length

  return (
    <main className="portal-ancho postular">
      <section className="postular-cuerpo">
        <h1>{todas ? `Listo, ${a.nombre}` : `Hola, ${a.nombre}`}</h1>
        <p className="postular-ayuda">
          {todas
            ? `Terminaste tus pruebas para ${a.vacante}. ${a.empresa} revisará tus resultados junto con tu hoja de vida; ninguna decisión se toma solo con un puntaje.`
            : `${a.empresa} te invitó a presentar ${a.pruebas.length} pruebas cortas para ${a.vacante}. Te toman unos ${minutos} minutos en total y puedes hacerlas desde el celular.`}
        </p>
        {!todas && <p className="pruebas-vence"><IconoReloj />Tienes hasta el {fechaHora.format(new Date(a.venceEn))}.</p>}

        <ol className="pruebas-lista">
          {a.pruebas.map((p, i) => (
            <li key={p.codigo} className={p.estado === 'Terminada' ? 'hecha' : ''} style={v(i)}>
              <span className="pruebas-lista-num">{p.estado === 'Terminada' ? <IconoCheck /> : i + 1}</span>
              <span className="pruebas-lista-texto"><b>{p.nombre}</b><span>{p.mide} · {p.preguntas} preguntas · {p.minutos} min</span></span>
              {a.consentimientoAceptado && p.estado !== 'Terminada' && (
                <Boton variante={terminadas === i ? 'primario' : 'secundario'} pequeno cargando={iniciar.isPending && iniciar.variables === p.codigo} textoCargando="Abriendo…"
                  onClick={() => iniciar.mutate(p.codigo, { onSuccess: (r) => setEnCurso({ ...r, preguntas: mezclar(r.preguntas) }) })}>
                  {p.estado === 'EnCurso' ? 'Continuar' : 'Empezar'}
                </Boton>
              )}
            </li>
          ))}
        </ol>
        {iniciar.error && <p role="alert" className="aviso aviso-alerta postular-error"><IconoAlerta />{iniciar.error.message}</p>}

        {!a.consentimientoAceptado && (
          <>
            <label className={`postular-autorizacion ${acepta ? 'aceptada' : ''}`}>
              <input type="checkbox" checked={acepta} onChange={(e) => setAcepta(e.target.checked)} />
              <span>{a.consentimientoTexto} <span className="mono">({a.consentimientoVersion})</span></span>
            </label>
            <ul className="pruebas-consejos">
              <li>Busca un lugar tranquilo y con buena conexión.</li>
              <li>Cada prueba tiene tiempo límite; corre desde que la abres.</li>
              <li>Responde tú mismo y sin ayuda: no hay respuestas de personalidad buenas o malas.</li>
            </ul>
            {consentir.error && <p role="alert" className="aviso aviso-alerta">{consentir.error.message}</p>}
            <div className="postular-enviar-barra">
              <Boton variante="primario" className="postular-enviar" disabled={!acepta} cargando={consentir.isPending && consentir.variables === true} textoCargando="Preparando…"
                onClick={() => consentir.mutate(true)}>
                Acepto y quiero empezar <IconoFlecha />
              </Boton>
              <button type="button" className="portal-limpiar" onClick={() => consentir.mutate(false)} disabled={consentir.isPending}>Prefiero no presentarlas</button>
            </div>
          </>
        )}
        <p className="postular-confianza"><IconoEscudo />Tus resultados se guardan cifrados y solo los ve la empresa. Una persona los revisa: nadie te descarta solo por un puntaje.</p>
      </section>
    </main>
  )
}

function Presentando({ token, prueba, onTerminar }: { token: string; prueba: PreguntasPrueba; onTerminar: () => void }) {
  const terminar = useTerminarPrueba(token)
  const [indice, setIndice] = useState(0)
  const [respuestas, setRespuestas] = useState<Record<string, { opcionId: string; milisegundos: number }>>({})
  const [segundos, setSegundos] = useState(prueba.segundosRestantes)
  const mostradaEn = useRef(0)
  const salidas = useRef(0)
  const entregada = useRef(false)
  const pregunta = prueba.preguntas[indice]!
  const total = prueba.preguntas.length
  const respondidas = Object.keys(respuestas).length
  const likert = prueba.tipo === 'Personalidad'

  const entregar = () => {
    if (entregada.current) return
    entregada.current = true
    terminar.mutate({
      codigo: prueba.codigo,
      respuestas: Object.entries(respuestas).map(([preguntaId, r]) => ({ preguntaId, ...r })),
      cambiosDePestana: salidas.current,
    }, { onSuccess: onTerminar, onError: () => { entregada.current = false } })
  }

  // Cuenta regresiva; al llegar a cero se entrega lo que haya
  useEffect(() => {
    const id = setInterval(() => setSegundos((s) => Math.max(0, s - 1)), 1000)
    return () => clearInterval(id)
  }, [])
  useEffect(() => {
    if (segundos === 0) entregar()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [segundos])

  // Antifraude: cuántas veces sale de la prueba (cambia de pestaña o de aplicación)
  useEffect(() => {
    const alSalir = () => { if (document.visibilityState === 'hidden') salidas.current += 1 }
    document.addEventListener('visibilitychange', alSalir)
    return () => document.removeEventListener('visibilitychange', alSalir)
  }, [])

  useEffect(() => { mostradaEn.current = performance.now() }, [indice])

  const elegir = (opcionId: string) => {
    const milisegundos = Math.round(performance.now() - mostradaEn.current)
    setRespuestas((r) => ({ ...r, [pregunta.id]: { opcionId, milisegundos } }))
    if (indice < total - 1) setTimeout(() => setIndice((i) => i + 1), 180)
  }

  const minutos = Math.floor(segundos / 60)
  const resto = String(segundos % 60).padStart(2, '0')

  return (
    <main className="portal-ancho postular presentando">
      <div className="presentando-cabeza">
        <b>{prueba.nombre}</b>
        <span className={`mono presentando-reloj ${segundos <= 60 ? 'poco' : ''}`} role="timer" aria-live="off"><IconoReloj />{minutos}:{resto}</span>
      </div>
      <div className="barra" aria-hidden="true"><span style={{ width: `${(respondidas / total) * 100}%`, background: 'var(--color-primario)' }} /></div>
      {indice === 0 && respondidas === 0 && <p className="postular-ayuda">{prueba.instrucciones}</p>}

      <section key={pregunta.id} className="presentando-pregunta" aria-live="polite">
        <span className="mono sub-celda">Pregunta {indice + 1} de {total}</span>
        <h2>{pregunta.texto}</h2>
        <div className={`presentando-opciones ${likert ? 'likert' : ''}`} role="radiogroup" aria-label={pregunta.texto}>
          {pregunta.opciones.map((o) => (
            <button key={o.id} type="button" role="radio" aria-checked={respuestas[pregunta.id]?.opcionId === o.id} onClick={() => elegir(o.id)}>
              {o.texto}
            </button>
          ))}
        </div>
      </section>

      <div className="presentando-pie">
        <Boton pequeno disabled={indice === 0} onClick={() => setIndice((i) => i - 1)}>Anterior</Boton>
        {indice < total - 1
          ? <Boton pequeno onClick={() => setIndice((i) => i + 1)}>Siguiente</Boton>
          : <Boton variante="primario" cargando={terminar.isPending} textoCargando="Entregando…" onClick={entregar}>Entregar prueba ({respondidas}/{total})</Boton>}
      </div>
      {terminar.error && <p role="alert" className="aviso aviso-alerta">{terminar.error.message}</p>}
    </main>
  )
}
