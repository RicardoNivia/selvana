import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoCheck } from '../../shared/ui/Iconos'
import {
  type Evaluacion, type Objetivo, type Potencial, decimal, fecha, nombresCaja, useCalibrar, useCalificar, useDefinirPlan, useEvaluacion, usePedirBorrador,
  useReabrir, useRegistrarAvance, useUsarBorrador,
} from './api'
import './desempeno.css'

export function EvaluacionDetalle() {
  const { id = '' } = useParams()
  const evaluacion = useEvaluacion(id)

  if (evaluacion.isPending) return <><header className="encabezado"><Esqueleto alto={32} ancho="40%" /><Esqueleto alto={14} ancho="50%" /></header><Esqueleto alto={360} /></>
  if (evaluacion.isError) return <p role="alert" className="aviso aviso-error"><IconoAlerta />{evaluacion.error.message}</p>

  const e = evaluacion.data
  const abierta = e.estadoCiclo === 'Abierto' || e.estadoCiclo === 'Calibracion'
  const editable = abierta && e.estado !== 'Calificada'

  return (
    <>
      <header className="encabezado">
        <p className="vin-migas"><Link to="/consola/desempeno">Desempeño</Link> · <Link to={`/consola/desempeno/${e.cicloId}?pestana=evaluaciones`}>{e.ciclo}</Link></p>
        <h1>{e.nombre} {e.estado === 'Calificada' && <span className="pill pill-ok"><IconoCheck />Calificada</span>}</h1>
        <p>{e.cargo}{e.area ? ` · ${e.area}` : ''}{e.sede ? ` · ${e.sede}` : ''} · <span className="mono">{e.codigo}</span>
          {' '}· <Link to={`/consola/empleados/${e.empleadoId}`}>ver el expediente</Link></p>
      </header>

      <div className="des-evaluacion">
        <div className="des-formularios">
          <Objetivos e={e} editable={editable} />
          <Calificar e={e} editable={editable} />
          <Plan e={e} />
        </div>
        <div className="des-formularios">
          <Resumen e={e} abierta={abierta} />
          {e.alcance !== 'Grados90' && <Autoevaluacion e={e} />}
          <Copiloto e={e} editable={editable} />
          {e.estadoCiclo === 'Calibracion' && e.estado === 'Calificada' && <CalibrarCaja e={e} />}
        </div>
      </div>
    </>
  )
}

function Resumen({ e, abierta }: { e: Evaluacion; abierta: boolean }) {
  const reabrir = useReabrir(e.id)
  return (
    <section className="tarjeta des-bloque">
      <header><h2>Puntaje</h2>{e.caja && <span className="pill pill-neutra">{e.nombreCaja}</span>}</header>
      <div><span className="des-puntaje mono">{decimal(e.puntaje ?? e.puntajeEnCurso)}</span> <span className="ayuda">de 5{e.puntaje == null ? ' · en curso' : ''}</span></div>
      <p className="ayuda">Objetivos {e.pesoObjetivos} % · competencias {100 - e.pesoObjetivos} %. La calificación es del líder; la autoevaluación y los pares son referencia.</p>
      {e.calificadaPor && <p className="ayuda">Calificó {e.calificadaPor} el <span className="mono">{fecha(e.calificadaEn)}</span> · potencial <b>{e.potencial}</b></p>}
      {e.ajuste && <p className="aviso aviso-neutro">El comité la movió a «{nombresCaja[e.ajuste.caja - 1]}»: {e.ajuste.motivo} · {e.ajuste.por}</p>}
      {abierta && e.estado === 'Calificada' && (
        <div><Boton pequeno cargando={reabrir.isPending} textoCargando="Reabriendo…" onClick={() => reabrir.mutate(undefined)}>Reabrir para corregir</Boton></div>
      )}
    </section>
  )
}

/* ---------- D2 · Objetivos con avance y evidencias ---------- */

function Objetivos({ e, editable }: { e: Evaluacion; editable: boolean }) {
  return (
    <section className="tarjeta des-bloque">
      <header><h2>Objetivos del periodo</h2><span className="ayuda">Avance y evidencias durante el ciclo</span></header>
      {e.objetivos.length === 0 && <p className="ayuda">Este cargo no tiene objetivos en el formulario.</p>}
      {e.objetivos.map((o) => <FilaObjetivo key={o.id} evaluacionId={e.id} o={o} editable={editable} />)}
    </section>
  )
}

function FilaObjetivo({ evaluacionId, o, editable }: { evaluacionId: string; o: Objetivo; editable: boolean }) {
  const avanzar = useRegistrarAvance(evaluacionId)
  const [avance, setAvance] = useState(o.avance)
  const [evidencia, setEvidencia] = useState('')
  const cambio = avance !== o.avance || evidencia.trim().length > 0
  return (
    <div className="des-objetivo">
      <div className="des-objetivo-fila"><b>{o.descripcion}</b><span className="mono">{o.peso} %</span></div>
      <small className="mono">Meta: {o.meta || '—'}</small>
      <div className="des-objetivo-fila">
        <input type="range" min={0} max={100} step={5} value={avance} disabled={!editable} aria-label={`Avance de ${o.descripcion}`} onChange={(x) => setAvance(Number(x.target.value))} />
        <span className="mono" style={{ minWidth: 48, textAlign: 'right' }}>{avance} %</span>
      </div>
      {o.evidencias.length > 0 && <ul className="des-evidencias">{o.evidencias.map((x, i) => <li key={i}>{x.texto} <span className="cita">{fecha(x.fecha)} · {x.por}</span></li>)}</ul>}
      {editable && (
        <div className="des-objetivo-fila">
          <input className="entrada" placeholder="Evidencia (opcional): qué pasó y dónde se ve" value={evidencia} onChange={(x) => setEvidencia(x.target.value)} />
          <Boton pequeno disabled={!cambio} cargando={avanzar.isPending} textoCargando="Guardando…"
            onClick={() => avanzar.mutate({ objetivoId: o.id, avance, evidencia: evidencia.trim() || undefined }, { onSuccess: () => setEvidencia('') })}>Guardar</Boton>
        </div>
      )}
      {avanzar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{avanzar.error.message}</p>}
    </div>
  )
}

/* ---------- Calificación del líder (decisión humana) ---------- */

function Calificar({ e, editable }: { e: Evaluacion; editable: boolean }) {
  const calificar = useCalificar(e.id)
  const [notas, setNotas] = useState<Record<string, number>>(() => Object.fromEntries(e.competencias.filter((c) => c.lider).map((c) => [c.nombre, c.lider!])))
  const [comentario, setComentario] = useState(e.comentarioLider)
  const [potencial, setPotencial] = useState<Potencial | undefined>(e.potencial ?? undefined)
  const completo = e.competencias.every((c) => notas[c.nombre]) && potencial && comentario.trim().length > 0

  return (
    <section className="tarjeta des-bloque">
      <header><h2>Competencias</h2><span className="ayuda">1 = muy por debajo · 5 = sobresaliente</span></header>
      {e.competencias.map((c) => (
        <div key={c.nombre} className="des-competencia">
          <div>
            <b>{c.nombre}</b> <span className="mono ayuda">{c.peso} %</span><small>{c.descripcion}</small>
            <span className="des-referencias">
              {c.auto != null && <span>Autoevaluación {c.auto}</span>}
              {c.promedioPares != null && <span>Pares {decimal(c.promedioPares)} ({c.pares})</span>}
            </span>
          </div>
          <span className="des-escala" role="group" aria-label={c.nombre}>
            {[1, 2, 3, 4, 5].map((n) => (
              <button key={n} type="button" aria-pressed={notas[c.nombre] === n} disabled={!editable} onClick={() => setNotas((x) => ({ ...x, [c.nombre]: n }))}>{n}</button>
            ))}
          </span>
        </div>
      ))}
      <label className="campo">Comentario para {e.nombre.split(' ')[0]}
        <textarea className="area-texto" rows={5} value={comentario} disabled={!editable} onChange={(x) => setComentario(x.target.value)}
          placeholder="Lo que logró, lo que debe mejorar y el siguiente paso. Puede partir del borrador del copiloto." />
      </label>
      <div className="des-objetivo-fila">
        <span>Potencial</span>
        <span className="des-potencial" role="group" aria-label="Potencial">
          {(['Bajo', 'Medio', 'Alto'] as const).map((p) => <button key={p} type="button" aria-pressed={potencial === p} disabled={!editable} onClick={() => setPotencial(p)}>{p}</button>)}
        </span>
      </div>
      {calificar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{calificar.error.message}</p>}
      {editable && (
        <div className="decision">
          <Boton variante="primario" disabled={!completo} cargando={calificar.isPending} textoCargando="Calificando…"
            onClick={() => calificar.mutate({ calificaciones: notas, comentario, potencial: potencial! })}>Calificar</Boton>
          <span className="registro">Se registran la calificación y quién la puso</span>
        </div>
      )}
    </section>
  )
}

function Autoevaluacion({ e }: { e: Evaluacion }) {
  return (
    <section className="tarjeta des-bloque">
      <header><h2>Autoevaluación</h2>{e.autoevaluadaEn ? <span className="pill pill-ok">Respondida</span> : <span className="pill pill-neutra">Pendiente</span>}</header>
      {e.autoevaluadaEn
        ? <p style={{ margin: 0 }}>«{e.comentarioAuto || 'Sin comentario'}» <span className="cita">Portal del empleado · {fecha(e.autoevaluadaEn)}</span></p>
        : <p className="ayuda">La persona se autoevalúa desde su portal (Mi empleo). Sus calificaciones aparecen junto a cada competencia.</p>}
    </section>
  )
}

/* ---------- D3 · Copiloto del evaluador (IA, nunca califica) ---------- */

function Copiloto({ e, editable }: { e: Evaluacion; editable: boolean }) {
  const pedir = usePedirBorrador(e.id)
  const usar = useUsarBorrador(e.id)
  const b = e.borrador
  return (
    <section className="bloque-ia">
      <div className="bloque-ia-cabeza"><Chispa /><b>Copiloto del evaluador</b><span className="derecha">{b?.modelo ?? ''}</span></div>
      <div className="des-bloque">
        {!b && <p className="ayuda">Redacta fortalezas, oportunidades y un comentario con los objetivos, las evidencias y la autoevaluación. Nunca pone la calificación.</p>}
        {b?.estado === 'Preparando' && <p className="estado-agente" role="status"><span className="orbe trabajando" />El copiloto está leyendo las evidencias…</p>}
        {b?.estado === 'Fallido' && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{b.error ?? 'No se pudo redactar.'} Escriba su comentario a mano.</p>}
        {b && (b.estado === 'Listo' || b.estado === 'Usado') && (
          <>
            <div><p className="des-subtitulo">Fortalezas</p><ul className="des-evidencias">{b.fortalezas.map((x) => <li key={x}>{x}</li>)}</ul></div>
            <div><p className="des-subtitulo">Oportunidades</p><ul className="des-evidencias">{b.oportunidades.map((x) => <li key={x}>{x}</li>)}</ul></div>
            <p style={{ margin: 0, fontSize: 14 }}>{b.comentario}</p>
            {b.razones.map((r) => <div key={r.texto} className="razon"><span className={`signo ${r.signo === '+' ? 'mas' : 'menos'}`}>{r.signo}</span><span className="texto">{r.texto}</span><span className="cita">{r.fuente}</span></div>)}
            <p className="ayuda">No se usaron para el borrador: edad, género, estado civil, foto ni dirección exacta.</p>
          </>
        )}
        {(pedir.error ?? usar.error) && <p role="alert" className="aviso aviso-error"><IconoAlerta />{(pedir.error ?? usar.error)!.message}</p>}
        {editable && (
          <div className="vin-acciones">
            {b?.estado === 'Listo' && <Boton variante="primario" cargando={usar.isPending} textoCargando="Usando…" onClick={() => usar.mutate(undefined)}>Usar como punto de partida</Boton>}
            {b?.estado !== 'Preparando' && (
              <Boton variante="ia" icono={<Chispa />} cargando={pedir.isPending} textoCargando="Pidiendo…" onClick={() => pedir.mutate(undefined)}>{b ? 'Pedir otro borrador' : 'Pedir borrador'}</Boton>
            )}
          </div>
        )}
      </div>
    </section>
  )
}

/* ---------- D4 · Calibración y plan de desarrollo ---------- */

function CalibrarCaja({ e }: { e: Evaluacion }) {
  const calibrar = useCalibrar(e.id)
  const [caja, setCaja] = useState(e.caja ?? 5)
  const [motivo, setMotivo] = useState('')
  return (
    <section className="tarjeta des-bloque">
      <header><h2>Comité de calibración</h2></header>
      <label className="campo">Caja en la matriz
        <select className="selector" value={caja} onChange={(x) => setCaja(Number(x.target.value))}>{nombresCaja.map((n, i) => <option key={n} value={i + 1}>{i + 1} · {n}</option>)}</select>
      </label>
      <label className="campo">Motivo del comité<textarea className="area-texto" rows={3} value={motivo} onChange={(x) => setMotivo(x.target.value)} /></label>
      {calibrar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{calibrar.error.message}</p>}
      <div className="decision">
        <Boton variante="primario" disabled={!motivo.trim() || caja === e.caja} cargando={calibrar.isPending} textoCargando="Moviendo…"
          onClick={() => calibrar.mutate({ caja, motivo }, { onSuccess: () => setMotivo('') })}>Mover en la matriz</Boton>
        <span className="registro">Queda en la bitácora con el motivo</span>
      </div>
    </section>
  )
}

function Plan({ e }: { e: Evaluacion }) {
  const definir = useDefinirPlan(e.id)
  const [acciones, setAcciones] = useState(() => e.plan.map((a) => ({ ...a, id: a.id as string | null })))
  const poner = (i: number, c: Partial<(typeof acciones)[number]>) => setAcciones((l) => l.map((x, j) => (j === i ? { ...x, ...c } : x)))
  const enNoventa = () => new Date(Date.now() + 90 * 86400000).toISOString().slice(0, 10)

  return (
    <section className="tarjeta des-bloque">
      <header><h2>Plan de desarrollo</h2><span className="ayuda">{e.plan.some((p) => p.origen === 'Reglas') ? 'Propuesto por las brechas (competencias en 1 o 2); edítelo con la persona.' : 'Acciones acordadas con la persona.'}</span></header>
      {acciones.length === 0 && <p className="ayuda">{e.estado === 'Calificada' ? 'No hubo brechas. Puede agregar acciones de crecimiento.' : 'Se propone al calificar, con las competencias que salgan bajas.'}</p>}
      {acciones.map((a, i) => (
        <div key={a.id ?? i} className="des-plan-fila">
          <input className="entrada" aria-label="Competencia" value={a.competencia} onChange={(x) => poner(i, { competencia: x.target.value })} />
          <input className="entrada" aria-label="Acción" value={a.accion} onChange={(x) => poner(i, { accion: x.target.value })} />
          <input className="entrada mono" type="date" aria-label="Fecha" value={a.fecha.slice(0, 10)} onChange={(x) => poner(i, { fecha: x.target.value })} />
          <label className="ayuda"><input type="checkbox" checked={a.cumplida} onChange={(x) => poner(i, { cumplida: x.target.checked })} /> Cumplida</label>
          <Boton pequeno onClick={() => setAcciones((l) => l.filter((_, j) => j !== i))}>Quitar</Boton>
        </div>
      ))}
      {definir.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{definir.error.message}</p>}
      <div className="vin-acciones">
        <Boton pequeno onClick={() => setAcciones((l) => [...l, { id: null, competencia: '', accion: '', fecha: enNoventa(), cumplida: false, origen: 'Manual' }])}>Agregar acción</Boton>
        <Boton pequeno variante="primario" cargando={definir.isPending} textoCargando="Guardando…"
          onClick={() => definir.mutate(acciones.map((a) => ({ id: a.id, competencia: a.competencia, accion: a.accion, fecha: a.fecha.slice(0, 10), cumplida: a.cumplida })))}>Guardar el plan</Boton>
      </div>
    </section>
  )
}
