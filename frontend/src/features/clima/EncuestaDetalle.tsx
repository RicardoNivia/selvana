import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoCheck } from '../../shared/ui/Iconos'
import {
  type Encuesta, type Pregunta, decimal, fecha, useAbrirEncuesta, useAnalizarIa, useAnalizarReglas, useCerrarEncuesta, useEditarEncuesta, useEncuesta, useMarcarRevisado,
} from './api'
import '../desempeno/desempeno.css'
import './clima.css'

export function EncuestaDetalle() {
  const { id = '' } = useParams()
  const encuesta = useEncuesta(id)
  if (encuesta.isPending) return <><header className="encabezado"><Esqueleto alto={32} ancho="40%" /></header><Esqueleto alto={300} /></>
  if (encuesta.isError) return <p role="alert" className="aviso aviso-error"><IconoAlerta />{encuesta.error.message}</p>
  const e = encuesta.data

  return (
    <>
      <header className="encabezado">
        <p className="vin-migas"><Link to="/consola/clima">Clima</Link> · encuesta anónima</p>
        <h1>{e.titulo} <span className={`pill ${e.estado === 'Abierta' ? 'pill-info' : e.estado === 'Cerrada' ? 'pill-ok' : 'pill-neutra'}`}>{e.estado}</span></h1>
        <p>{e.preguntas.length} preguntas{e.incluyeEnps ? ' · eNPS' : ''} · creada por {e.creadaPor}{e.abiertaEn ? ` · abierta el ${fecha(e.abiertaEn)}` : ''}</p>
      </header>
      <Enlace e={e} />
      {e.estado === 'Borrador' ? <EditorPreguntas e={e} /> : <Resultados e={e} />}
    </>
  )
}

function Enlace({ e }: { e: Encuesta }) {
  const abrir = useAbrirEncuesta(e.id)
  const cerrar = useCerrarEncuesta(e.id)
  const [copiado, setCopiado] = useState(false)
  const enlace = abrir.data ? `${window.location.origin}${abrir.data.ruta}` : null
  return (
    <section className="tarjeta des-bloque">
      <header>
        <h2>Enlace anónimo</h2>
        <span className="vin-acciones">
          {e.estado !== 'Cerrada' && (
            <Boton variante={e.estado === 'Borrador' ? 'primario' : 'secundario'} cargando={abrir.isPending} textoCargando="Generando…" onClick={() => abrir.mutate(undefined)}>
              {e.estado === 'Borrador' ? 'Abrir la encuesta' : 'Generar enlace nuevo'}
            </Boton>
          )}
          {e.estado === 'Abierta' && <Boton cargando={cerrar.isPending} textoCargando="Cerrando…" onClick={() => cerrar.mutate(undefined)}>Cerrar la encuesta</Boton>}
        </span>
      </header>
      <p className="ayuda">Compártalo por WhatsApp, correo o un código QR en la sede. No pide nombre ni cédula: cada persona elige su área y su sede, y los resultados se muestran solo
        para grupos de 5 o más respuestas. Por seguridad solo se guarda la huella del enlace: si lo pierde, genere uno nuevo (el anterior deja de servir).</p>
      {enlace && (
        <div className="cli-enlace">
          <code>{enlace}</code>
          <Boton pequeno onClick={() => { void navigator.clipboard?.writeText(enlace); setCopiado(true) }}>{copiado ? 'Copiado' : 'Copiar'}</Boton>
        </div>
      )}
      {(abrir.error ?? cerrar.error) && <p role="alert" className="aviso aviso-error"><IconoAlerta />{(abrir.error ?? cerrar.error)!.message}</p>}
    </section>
  )
}

/** Camino manual: las preguntas de la plantilla base se editan, se quitan o se agregan antes de abrir. */
function EditorPreguntas({ e }: { e: Encuesta }) {
  const editar = useEditarEncuesta(e.id)
  const [titulo, setTitulo] = useState(e.titulo)
  const [preguntas, setPreguntas] = useState<Pregunta[]>(e.preguntas)
  const [enps, setEnps] = useState(e.incluyeEnps)
  const [abierta, setAbierta] = useState(e.preguntaAbierta)
  const poner = (i: number, c: Partial<Pregunta>) => setPreguntas((l) => l.map((x, j) => (j === i ? { ...x, ...c } : x)))

  return (
    <section className="tarjeta des-bloque">
      <header><h2>Preguntas</h2><span className="ayuda">Se responden de 1 (muy en desacuerdo) a 5 (muy de acuerdo)</span></header>
      <label className="campo">Título<input className="entrada" value={titulo} onChange={(x) => setTitulo(x.target.value)} /></label>
      {preguntas.map((p, i) => (
        <div key={i} className="des-editor-fila">
          <input className="entrada" aria-label="Dimensión" value={p.dimension} onChange={(x) => poner(i, { dimension: x.target.value })} />
          <input className="entrada" aria-label="Afirmación" value={p.texto} onChange={(x) => poner(i, { texto: x.target.value })} />
          <span />
          <Boton pequeno onClick={() => setPreguntas((l) => l.filter((_, j) => j !== i))}>Quitar</Boton>
        </div>
      ))}
      <div><Boton pequeno onClick={() => setPreguntas((l) => [...l, { id: '', dimension: '', texto: '' }])}>Agregar pregunta</Boton></div>
      <label className="ayuda"><input type="checkbox" checked={enps} onChange={(x) => setEnps(x.target.checked)} /> Incluir eNPS («¿Recomendarías esta empresa como lugar de trabajo?», de 0 a 10)</label>
      <label className="campo">Pregunta abierta<input className="entrada" value={abierta} onChange={(x) => setAbierta(x.target.value)} /></label>
      {editar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{editar.error.message}</p>}
      <div className="acciones-formulario">
        <Boton variante="primario" cargando={editar.isPending} textoCargando="Guardando…" onClick={() => editar.mutate({ titulo, preguntas, incluyeEnps: enps, preguntaAbierta: abierta })}>
          {editar.isSuccess ? <><IconoCheck />Guardado</> : 'Guardar cambios'}
        </Boton>
      </div>
    </section>
  )
}

function Resultados({ e }: { e: Encuesta }) {
  const r = e.resultados
  if (r.promedio == null) {
    return <div className="vacio"><p>Hay <span className="mono">{r.respuestas}</span> respuestas. Los resultados aparecen desde 5, para que nadie quede expuesto.</p></div>
  }

  return (
    <div className="des-formularios">
      <div className="cli-resumen">
        <div className="tarjeta cli-cifra"><b>{r.respuestas}</b><span>respuestas anónimas</span></div>
        <div className="tarjeta cli-cifra"><b>{decimal(r.promedio)}</b><span>promedio de 5</span></div>
        {r.enps != null && <div className="tarjeta cli-cifra"><b className={r.enps >= 0 ? 'positivo' : 'negativo'}>{r.enps > 0 ? `+${r.enps}` : r.enps}</b><span>eNPS · {r.promotores} % promotores, {r.detractores} % detractores</span></div>}
      </div>
      <section className="tarjeta des-bloque">
        <header><h2>Por dimensión</h2><span className="ayuda">Porcentaje que respondió 4 o 5</span></header>
        <div className="cli-dimensiones">
          {r.dimensiones.map((d) => (
            <div key={d.dimension} className="cli-dimension">
              <span>{d.dimension}</span>
              <span className="cli-barra"><span className={d.promedio < 3.2 ? 'baja' : ''} style={{ width: `${d.favorables}%` }} /></span>
              <span className="mono">{decimal(d.promedio)} · {d.favorables} %</span>
            </div>
          ))}
        </div>
      </section>
      <section className="tarjeta des-bloque">
        <header><h2>Por área y sede</h2><span className="ayuda">{r.gruposOcultos > 0 ? `${r.gruposOcultos} grupos con menos de 5 respuestas no se muestran` : 'Solo grupos con 5 o más respuestas'}</span></header>
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead><tr><th>Grupo</th><th className="num">Respuestas</th><th className="num">Promedio</th><th className="num">eNPS</th><th>Dimensión más baja</th></tr></thead>
            <tbody>
              {r.grupos.map((g) => {
                const baja = [...g.dimensiones].sort((a, b) => a.promedio - b.promedio)[0]
                return (
                  <tr key={g.tipo + g.nombre}>
                    <td>{g.nombre}<span className="emp-sub">{g.tipo}</span></td>
                    <td className="num">{g.respuestas}</td>
                    <td className="num">{decimal(g.promedio)}</td>
                    <td className="num">{g.enps ?? '—'}</td>
                    <td>{baja ? `${baja.dimension} (${decimal(baja.promedio)})` : '—'}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      </section>
      <Comentarios e={e} />
    </div>
  )
}

/* ---------- C2 · Temas de los comentarios ---------- */

function Comentarios({ e }: { e: Encuesta }) {
  const ia = useAnalizarIa(e.id)
  const reglas = useAnalizarReglas(e.id)
  const revisar = useMarcarRevisado(e.id)
  const a = e.analisis
  const deIa = a?.origen === 'IA' && !a.revisadoPor && a.estado === 'Listo'

  return (
    <section className={deIa ? 'bloque-ia' : 'tarjeta'}>
      {deIa && <div className="bloque-ia-cabeza"><Chispa /><b>Análisis de la IA · por revisar</b><span className="derecha">{a?.modelo}</span></div>}
      <div className="des-bloque">
        <header>
          <h2>Comentarios · <span className="mono">{e.comentarios}</span></h2>
          <span className="vin-acciones">
            <Boton pequeno variante="ia" icono={<Chispa />} disabled={e.comentarios < 5} cargando={ia.isPending || a?.estado === 'Preparando'} textoCargando="Analizando…" onClick={() => ia.mutate(undefined)}>
              Agrupar con IA
            </Boton>
            <Boton pequeno disabled={e.comentarios < 5} cargando={reglas.isPending} textoCargando="Agrupando…" onClick={() => reglas.mutate(undefined)}>Agrupar por palabras clave</Boton>
          </span>
        </header>
        {e.comentarios < 5 && <p className="ayuda">Hacen falta al menos 5 comentarios para analizarlos sin exponer a nadie.</p>}
        {a?.estado === 'Fallido' && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{a.error} Use el análisis por palabras clave.</p>}
        {a && a.estado === 'Listo' && (
          <>
            <p style={{ margin: 0 }}>{a.resumen} <span className="cita">{a.origen === 'IA' ? 'IA' : 'Palabras clave'} · {a.comentarios} comentarios</span></p>
            <div className="cli-temas">
              {a.temas.map((t) => (
                <article key={t.nombre} className="cli-tema">
                  <header><b>{t.nombre}</b><span className={`pill ${t.sentimiento === 'Negativo' ? 'pill-error' : t.sentimiento === 'Positivo' ? 'pill-ok' : 'pill-alerta'}`}>{t.sentimiento} · {t.cantidad}</span></header>
                  {t.citas.map((c) => <q key={c}>{c}</q>)}
                  <p><b>Qué hacer:</b> {t.accion}</p>
                </article>
              ))}
            </div>
            {deIa && (
              <div className="decision">
                <Boton variante="primario" cargando={revisar.isPending} textoCargando="Marcando…" onClick={() => revisar.mutate(undefined)}>Marcar como revisado</Boton>
                <span className="registro">Se registran el análisis de la IA y quién lo revisó</span>
              </div>
            )}
          </>
        )}
        {(ia.error ?? reglas.error) && <p role="alert" className="aviso aviso-error"><IconoAlerta />{(ia.error ?? reglas.error)!.message}</p>}
      </div>
    </section>
  )
}
