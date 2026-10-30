import { type CSSProperties, type FormEvent, useState } from 'react'
import { Link } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoAlerta, IconoBuscar, IconoCerrar, IconoCheck, IconoEscudo, IconoReloj } from '../../shared/ui/Iconos'
import { Paginacion } from '../../shared/ui/Paginacion'
import { usePaginacion } from '../../shared/ui/paginado'
import { usePuede } from '../sesion/api'
import { type FiltroRapido, type Miembro, type MiEquipo as Datos, useAsignarLider, useMiEquipo, useOpcionesEquipo } from './api'
import './equipo.css'

const ejemplos = ['¿Quién tiene el curso de alturas vencido?', '¿A quién le termina el contrato?', '¿Quién tiene riesgo de irse?', '¿A quién me falta evaluar?', '¿Quiénes son nuevos?']
const fecha = (iso: string) => { const [a, m, d] = iso.slice(0, 10).split('-'); return `${d}/${m}/${a}` }
const iniciales = (n: string) => n.split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p[0]!.toUpperCase()).join('')
const antiguedad = (dias: number) => (dias < 31 ? `${dias} ${dias === 1 ? 'día' : 'días'}` : dias < 365 ? `${Math.floor(dias / 30)} meses` : `${(dias / 365).toFixed(1).replace('.', ',')} años`)
const nivelPrioridad = { Urgente: ['Urgente', 'pill-error'], EstaSemana: ['Esta semana', 'pill-alerta'], Acompanar: ['Acompañar', 'pill-info'] } as const
const nivelRiesgo = { Alto: 'pill-error', Medio: 'pill-alerta', Bajo: 'pill-ok' } as const
const estadoEvaluacion = { Pendiente: 'Sin empezar', EnCurso: 'En curso', Calificada: 'Calificada' } as const

/**
 * E4 · «Mi equipo»: el líder trabaja dentro de la consola (no hay portal aparte). Ve a su gente con sus alertas, su evaluación y su riesgo de rotación,
 * lo que conviene atender esta semana y responde preguntas en lenguaje natural. RRHH puede mirar el equipo de cualquier líder y armar los equipos.
 */
export function MiEquipo() {
  const [lider, setLider] = useState<string | null>(null)
  const [texto, setTexto] = useState('')
  const [pregunta, setPregunta] = useState('')
  const [filtro, setFiltro] = useState<FiltroRapido>('Todos')
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const equipo = useMiEquipo({ lider, pregunta, filtro, pagina, tamano })
  const armaEquipos = usePuede('GestionarEmpleados')
  const [armando, setArmando] = useState(false)

  const preguntar = (p: string) => { setTexto(p); setPregunta(p.trim()); setFiltro('Todos'); setPagina(1) }
  const elegirFiltro = (f: FiltroRapido) => { setFiltro(f); setPregunta(''); setTexto(''); setPagina(1) }
  const enviar = (e: FormEvent) => { e.preventDefault(); preguntar(texto) }

  if (equipo.isPending) return <EsqueletoEquipo />
  if (equipo.isError) return <p role="alert" className="aviso aviso-error"><IconoAlerta />{equipo.error.message}</p>
  const d = equipo.data
  const propio = !lider || lider === d.lider.id

  return (
    <div className="equipo">
      <header className="encabezado equipo-cabeza">
        <div>
          <h1>{propio && !d.puedeElegirLider ? 'Mi equipo' : `Equipo de ${d.lider.nombre}`}</h1>
          <p>{d.resumen.personas === 1 ? 'Una persona a cargo' : `${d.resumen.personas} personas a cargo`}. Sus alertas, evaluaciones y señales de rotación en un solo lugar.</p>
        </div>
        <div className="equipo-cabeza-acciones">
          {d.puedeElegirLider && d.lideres.length > 0 && (
            <label className="campo equipo-lider">Ver el equipo de
              <select className="selector" value={lider ?? d.lider.id} onChange={(e) => { setLider(e.target.value); setPagina(1) }}>
                {!d.lideres.some((l) => l.id === d.lider.id) && <option value={d.lider.id}>{d.lider.nombre} (usted)</option>}
                {d.lideres.map((l) => <option key={l.id} value={l.id}>{l.nombre} · {l.personas}</option>)}
              </select>
            </label>
          )}
          {armaEquipos && <Boton onClick={() => setArmando((a) => !a)} aria-expanded={armando}>{armando ? 'Cerrar' : 'Armar equipos'}</Boton>}
        </div>
      </header>

      {armando && <ArmarEquipos onListo={() => setArmando(false)} />}

      {d.resumen.personas === 0 ? (
        <section className="tarjeta vacio equipo-vacio">
          <h2>Aún no tiene personas a cargo</h2>
          <p>RRHH asigna el líder directo de cada persona desde su ficha o por área con «Armar equipos». Cuando lo haga, verá aquí a su equipo.</p>
        </section>
      ) : (
        <>
          <form className="equipo-pregunta" onSubmit={enviar} role="search">
            <IconoBuscar />
            <input className="entrada" value={texto} onChange={(e) => setTexto(e.target.value)} placeholder="Pregúntele a su equipo: ¿quién tiene el curso de alturas vencido?"
              aria-label="Pregunta sobre su equipo" />
            <Boton type="submit" variante="primario" pequeno>Preguntar</Boton>
          </form>
          <div className="equipo-ejemplos" aria-label="Ejemplos">
            {ejemplos.map((x) => <button key={x} type="button" className="equipo-ejemplo" onClick={() => preguntar(x)}>{x}</button>)}
          </div>
          {pregunta && (
            <p className="equipo-entendido" role="status">
              <span>Entendí:</span>
              {d.entendido.length > 0 ? d.entendido.map((x) => <span key={x} className="pill pill-neutra">{x}</span>) : <span className="pill pill-neutra">Todas las personas</span>}
              <span className="mono">{d.miembros.total} {d.miembros.total === 1 ? 'persona' : 'personas'}</span>
              <button type="button" className="enlace-boton" onClick={() => preguntar('')}><IconoCerrar /> Quitar</button>
            </p>
          )}

          <Resumen d={d} onFiltro={elegirFiltro} />

          {d.prioridades.length > 0 && (
            <section className="tarjeta equipo-prioridades" aria-label="Para atender esta semana">
              <header>
                <h2>Para atender esta semana</h2>
                <span className="ayuda">Por reglas: alertas del expediente, ciclos de desempeño y señales de rotación.</span>
              </header>
              <ol>
                {d.prioridades.map((p, i) => {
                  const [texto, clase] = nivelPrioridad[p.nivel]
                  return (
                    <li key={`${p.empleadoId}-${i}`} style={{ '--i': i } as CSSProperties}>
                      <span className={`pill ${clase}`}>{texto}</span>
                      <span className="equipo-prioridad-texto">{p.texto}</span>
                      <span className="cita">{p.evidencia}</span>
                    </li>
                  )
                })}
              </ol>
            </section>
          )}

          <section className="tarjeta equipo-lista" aria-label="Personas del equipo">
            {d.miembros.items.length === 0
              ? <div className="vacio" style={{ padding: 32 }}><p>Nadie del equipo cumple eso. Pruebe otra pregunta o quite el filtro.</p></div>
              : <ul>{d.miembros.items.map((m, i) => <FilaMiembro key={m.id} m={m} indice={i} />)}</ul>}
            <Paginacion pagina={d.miembros} cargando={equipo.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />
          </section>
          <p className="protegidas equipo-nota"><IconoEscudo />El riesgo de rotación orienta la conversación con la persona; nunca es una decisión sobre ella. Salario y datos de salud no se muestran aquí.</p>
        </>
      )}
    </div>
  )
}

function Resumen({ d, onFiltro }: { d: Datos; onFiltro: (f: FiltroRapido) => void }) {
  const r = d.resumen
  const tiles: [FiltroRapido, string, number, string][] = [
    ['Todos', 'Personas', r.personas, ''],
    ['ConAlertas', 'Con alertas', r.conAlertas, 'alerta'],
    ['DocumentosVencidos', 'Documentos vencidos', r.documentosVencidos, 'error'],
    ['PorEvaluar', 'Por evaluar', r.porEvaluar, 'info'],
    ['RiesgoRotacion', 'Riesgo alto', r.riesgoAlto, 'error'],
    ['ContratoPorTerminar', 'Contratos por terminar', r.contratosPorTerminar, 'alerta'],
    ['Nuevos', 'Nuevos (90 días)', r.nuevos, 'ok'],
  ]
  return (
    <div className="equipo-resumen" role="group" aria-label="Filtrar el equipo">
      {tiles.map(([f, t, n, tono], i) => (
        <button key={f} type="button" className={`equipo-tile ${tono} ${n === 0 && f !== 'Todos' ? 'cero' : ''}`} aria-pressed={d.filtro === f}
          onClick={() => onFiltro(f)} style={{ '--i': i } as CSSProperties}>
          <b>{n}</b><span>{t}</span>
        </button>
      ))}
    </div>
  )
}

function FilaMiembro({ m, indice }: { m: Miembro; indice: number }) {
  const [abierta, setAbierta] = useState(false)
  const veFicha = usePuede('GestionarEmpleados')
  const veEvaluacion = usePuede('GestionarDesempeno')
  const vencidas = m.alertas.filter((a) => a.nivel === 'Vencido').length
  return (
    <li className={`miembro ${abierta ? 'abierta' : ''}`} style={{ '--i': indice } as CSSProperties}>
      <div className="miembro-fila">
        <span className="miembro-avatar" aria-hidden="true">{iniciales(m.nombre)}</span>
        <div className="miembro-quien">
          {veFicha ? <Link to={`/consola/empleados/${m.id}`} className="miembro-nombre">{m.nombre}</Link> : <b className="miembro-nombre">{m.nombre}</b>}
          <span className="miembro-meta">{m.cargo} · {m.area || 'Sin área'}{m.sede ? ` · ${m.sede}` : ''}{m.empresaUsuaria ? ` · ${m.empresaUsuaria}` : ''}</span>
        </div>
        <span className="miembro-dato"><IconoReloj />{antiguedad(m.diasEnLaEmpresa)}</span>
        <span className="miembro-chips">
          {m.alertas.length > 0 && <span className={`pill ${vencidas > 0 ? 'pill-error' : 'pill-alerta'}`} title={m.alertas.map((a) => a.texto).join('\n')}><IconoAlerta />{m.alertas.length} {m.alertas.length === 1 ? 'alerta' : 'alertas'}</span>}
          {m.evaluacion && <span className={`pill ${m.evaluacion.estado === 'Calificada' ? 'pill-ok' : 'pill-info'}`}>
            {m.evaluacion.estado === 'Calificada' && m.evaluacion.puntaje !== null ? <>Desempeño <span className="mono">{m.evaluacion.puntaje.toFixed(1).replace('.', ',')}</span></> : `Evaluación: ${estadoEvaluacion[m.evaluacion.estado]}`}
          </span>}
          {m.riesgo && m.riesgo.nivel !== 'Bajo' && <span className={`pill ${nivelRiesgo[m.riesgo.nivel]}`}>Riesgo {m.riesgo.nivel.toLowerCase()}</span>}
        </span>
        <button type="button" className="enlace-boton" aria-expanded={abierta} onClick={() => setAbierta((a) => !a)}>{abierta ? 'Ocultar' : 'Detalle'}</button>
      </div>
      {abierta && (
        <div className="miembro-detalle">
          <section>
            <h3>Alertas</h3>
            {m.alertas.length === 0 ? <p className="ayuda"><IconoCheck /> Todo al día.</p> : (
              <ul>{m.alertas.map((a) => <li key={a.texto} className={a.nivel === 'Vencido' ? 'vencido' : ''}><IconoAlerta /><span>{a.texto}</span><span className="cita">{a.fuente}</span></li>)}</ul>
            )}
            <p className="ayuda">Ingresó el <span className="mono">{fecha(m.fechaIngreso)}</span>{m.fechaFinContrato && <> · contrato hasta <span className="mono">{fecha(m.fechaFinContrato)}</span></>}</p>
          </section>
          <section>
            <h3>Desempeño</h3>
            {m.evaluacion ? (
              <p className="ayuda">«{m.evaluacion.ciclo}» · {estadoEvaluacion[m.evaluacion.estado]} · cierra el <span className="mono">{fecha(m.evaluacion.hasta)}</span>
                {veEvaluacion && <> · <Link to={`/consola/desempeno/evaluaciones/${m.evaluacion.id}`}>{m.evaluacion.estado === 'Calificada' ? 'Ver la evaluación' : 'Calificar'}</Link></>}</p>
            ) : <p className="ayuda">Sin ciclo de evaluación abierto.</p>}
          </section>
          {m.riesgo && (
            <section>
              <h3>Riesgo de rotación · <span className={`pill ${nivelRiesgo[m.riesgo.nivel]}`}>{m.riesgo.nivel}</span></h3>
              {m.riesgo.senales.length === 0 ? <p className="ayuda">Sin señales.</p> : (
                <ul>{m.riesgo.senales.map((s) => <li key={s.texto}><span>{s.texto}</span><span className="cita">{s.evidencia}</span></li>)}</ul>
              )}
              {m.riesgo.acciones.length > 0 && <p className="ayuda"><b>Qué hacer:</b> {m.riesgo.acciones.join(' ')}</p>}
            </section>
          )}
        </div>
      )}
    </li>
  )
}

/** RRHH arma los equipos: un líder por área (a quienes ya tienen líder también los cambia). Una persona sola se asigna desde su ficha. */
function ArmarEquipos({ onListo }: { onListo: () => void }) {
  const opciones = useOpcionesEquipo(true)
  const asignar = useAsignarLider()
  const [lider, setLider] = useState('')
  const [area, setArea] = useState('')

  if (opciones.isPending) return <section className="tarjeta equipo-armar"><Esqueleto alto={120} /></section>
  if (opciones.isError) return <p role="alert" className="aviso aviso-error"><IconoAlerta />{opciones.error.message}</p>
  const o = opciones.data
  const elegida = o.areas.find((a) => a.nombre === area)

  return (
    <form className="tarjeta equipo-armar panel-decision" onSubmit={(e) => { e.preventDefault(); asignar.mutate({ liderId: lider, area }, { onSuccess: onListo }) }}>
      <h2>Armar equipos</h2>
      <p className="ayuda">Elija el área y su líder: todas las personas activas del área quedan a su cargo. Para una sola persona, use «Líder directo» en su ficha.</p>
      <div className="equipo-armar-campos">
        <label className="campo">Área
          <select className="selector" value={area} onChange={(e) => setArea(e.target.value)} required>
            <option value="">Elija el área…</option>
            {o.areas.map((a) => <option key={a.nombre} value={a.nombre}>{a.nombre} · {a.personas} {a.personas === 1 ? 'persona' : 'personas'}{a.sinLider > 0 ? ` (${a.sinLider} sin líder)` : ''}</option>)}
          </select>
        </label>
        <label className="campo">Líder
          <select className="selector" value={lider} onChange={(e) => setLider(e.target.value)} required>
            <option value="">Elija el líder…</option>
            {o.usuarios.map((u) => <option key={u.id} value={u.id}>{u.nombre} · {u.rol === 'Lider' ? 'Líder' : u.rol === 'GerenteRrhh' ? 'Gerente RRHH' : u.rol}{u.personas > 0 ? ` · ${u.personas} a cargo` : ''}</option>)}
          </select>
        </label>
      </div>
      {elegida && elegida.sinLider < elegida.personas && <p className="aviso aviso-neutro">{elegida.personas - elegida.sinLider} de {elegida.personas} ya tienen líder: pasarán a cargo del que elija.</p>}
      {asignar.error && <span role="alert" className="error-formulario">{asignar.error.message}</span>}
      <div className="decision">
        <Boton type="submit" variante="primario" cargando={asignar.isPending} textoCargando="Asignando…" disabled={!lider || !area}>Asignar líder al área</Boton>
        <Boton onClick={onListo} disabled={asignar.isPending}>Cancelar</Boton>
        <span className="registro">Queda en la bitácora con su nombre</span>
      </div>
    </form>
  )
}

function EsqueletoEquipo() {
  return (
    <div className="equipo">
      <div className="encabezado"><Esqueleto alto={34} ancho="40%" /><Esqueleto alto={14} ancho="60%" style={{ marginTop: 8 }} /></div>
      <Esqueleto alto={48} />
      <div className="equipo-resumen">{Array.from({ length: 7 }, (_, i) => <Esqueleto key={i} alto={64} />)}</div>
      <div className="tarjeta" style={{ padding: 16, display: 'grid', gap: 10 }}>{Array.from({ length: 5 }, (_, i) => <Esqueleto key={i} alto={52} />)}</div>
    </div>
  )
}
