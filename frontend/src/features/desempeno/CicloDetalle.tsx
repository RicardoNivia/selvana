import { useCallback, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoCheck } from '../../shared/ui/Iconos'
import { Buscador, Paginacion } from '../../shared/ui/Paginacion'
import { usePaginacion } from '../../shared/ui/paginado'
import {
  type Ciclo, type Competencia, type Formulario, type ObjetivoBase, decimal, fecha, nombreAlcance, nombreEstadoCiclo, nombreTipo, useAbrirCiclo, useAprobarFormulario,
  useCerrarCiclo, useCiclo, useEvaluaciones, useGuardarFormulario, usePasarCalibracion, usePedirFormularioIa, useUsarPlantilla,
} from './api'
import './desempeno.css'

type Pestana = 'formularios' | 'evaluaciones' | 'calibracion'
const etapas = ['Borrador', 'Abierto', 'Calibracion', 'Cerrado'] as const

export function CicloDetalle() {
  const { id = '' } = useParams()
  const ciclo = useCiclo(id)
  const [parametros, setParametros] = useSearchParams()

  if (ciclo.isPending) return <><header className="encabezado"><Esqueleto alto={32} ancho="40%" /><Esqueleto alto={14} ancho="60%" /></header><Esqueleto alto={320} /></>
  if (ciclo.isError) return <p role="alert" className="aviso aviso-error"><IconoAlerta />{ciclo.error.message}</p>

  const c = ciclo.data
  const pestana = (parametros.get('pestana') as Pestana) ?? (c.estado === 'Borrador' ? 'formularios' : c.estado === 'Calibracion' ? 'calibracion' : 'evaluaciones')
  const pestanas: [Pestana, string][] = [['formularios', 'Formularios por cargo'], ['evaluaciones', 'Evaluaciones'], ['calibracion', 'Calibración y 9-box']]
  const indice = etapas.indexOf(c.estado)

  return (
    <>
      <header className="encabezado">
        <p className="vin-migas"><Link to="/consola/desempeno">Desempeño</Link> · {nombreTipo[c.tipo]}</p>
        <h1>{c.nombre}</h1>
        <p>{nombreAlcance[c.alcance]} · <span className="mono">{fecha(c.desde)} – {fecha(c.hasta)}</span> · objetivos {c.pesoObjetivos} % y competencias {100 - c.pesoObjetivos} %</p>
      </header>

      <div className="des-barra">
        <div className="des-pasos" aria-label="Etapas del ciclo">
          {etapas.map((e, i) => (
            <span key={e} className={`des-paso ${i < indice ? 'hecho' : i === indice ? 'actual' : ''}`}>{i < indice && <IconoCheck />}{nombreEstadoCiclo[e]}</span>
          ))}
        </div>
        <AccionesCiclo c={c} />
      </div>

      <div role="tablist" aria-label="Ciclo" className="pestanas">
        {pestanas.map(([clave, texto]) => (
          <button key={clave} type="button" role="tab" className="pestana" aria-selected={pestana === clave} onClick={() => setParametros({ pestana: clave }, { replace: true })}>
            {texto}
            {clave === 'formularios' && c.estado === 'Borrador' && <span className="emp-cuenta mono">{c.formularios.filter((f) => f.aprobadoEn).length}/{c.formularios.length}</span>}
            {clave === 'calibracion' && c.sesgos.length > 0 && <span className="nav-contador">{c.sesgos.length}</span>}
          </button>
        ))}
      </div>

      <div role="tabpanel">
        {pestana === 'formularios' && <Formularios c={c} />}
        {pestana === 'evaluaciones' && <Evaluaciones c={c} />}
        {pestana === 'calibracion' && <Calibracion c={c} />}
      </div>
    </>
  )
}

function AccionesCiclo({ c }: { c: Ciclo }) {
  const abrir = useAbrirCiclo(c.id)
  const calibrar = usePasarCalibracion(c.id)
  const cerrar = useCerrarCiclo(c.id)
  const error = abrir.error ?? calibrar.error ?? cerrar.error
  const faltan = c.formularios.filter((f) => !f.aprobadoEn).length
  return (
    <div className="vin-acciones">
      {error && <span role="alert" className="postular-nota-error">{error.message}</span>}
      {c.estado === 'Borrador' && (
        <Boton variante="primario" disabled={faltan > 0} title={faltan > 0 ? `Faltan ${faltan} formularios por aprobar` : undefined}
          cargando={abrir.isPending} textoCargando="Abriendo…" onClick={() => abrir.mutate(undefined)}>Abrir el ciclo</Boton>
      )}
      {c.estado === 'Abierto' && <Boton variante="primario" cargando={calibrar.isPending} textoCargando="Pasando…" onClick={() => calibrar.mutate(undefined)}>Pasar a calibración</Boton>}
      {c.estado === 'Calibracion' && <Boton variante="primario" cargando={cerrar.isPending} textoCargando="Cerrando…" onClick={() => cerrar.mutate(undefined)}>Cerrar el ciclo</Boton>}
    </div>
  )
}

/* ---------- D1 · Formularios por cargo ---------- */

function Formularios({ c }: { c: Ciclo }) {
  const ia = usePedirFormularioIa(c.id)
  const plantilla = useUsarPlantilla(c.id)
  const vacios = c.formularios.filter((f) => f.competencias.length === 0 && !f.preparando).length
  const editable = c.estado === 'Borrador'

  return (
    <div className="des-formularios">
      {editable && vacios > 0 && (
        <div className="tarjeta des-formulario">
          <p style={{ margin: 0 }}>{vacios} {vacios === 1 ? 'cargo no tiene' : 'cargos no tienen'} formulario. Pídale a la IA una propuesta por cargo o use la plantilla base.</p>
          <div className="vin-acciones">
            <Boton variante="ia" icono={<Chispa />} cargando={ia.isPending} textoCargando="Pidiendo…" onClick={() => ia.mutate(null)}>Pedir a la IA los {vacios}</Boton>
            <Boton cargando={plantilla.isPending} textoCargando="Aplicando…" onClick={() => plantilla.mutate(null)}>Usar la plantilla base</Boton>
          </div>
          {(ia.error ?? plantilla.error) && <p role="alert" className="aviso aviso-error"><IconoAlerta />{(ia.error ?? plantilla.error)!.message}</p>}
        </div>
      )}
      {c.formularios.map((f) => <TarjetaFormulario key={f.cargo} cicloId={c.id} f={f} editable={editable} />)}
    </div>
  )
}

function TarjetaFormulario({ cicloId, f, editable }: { cicloId: string; f: Formulario; editable: boolean }) {
  const ia = usePedirFormularioIa(cicloId)
  const plantilla = useUsarPlantilla(cicloId)
  const aprobar = useAprobarFormulario(cicloId)
  const [editando, setEditando] = useState(false)
  // Lo que propuso la IA va en violeta hasta que alguien lo apruebe
  const deIa = f.origen === 'IA' && !f.aprobadoEn
  const error = ia.error ?? plantilla.error ?? aprobar.error

  return (
    <section className={`tarjeta des-formulario${deIa ? ' ia' : ''}`}>
      <header>
        <h3>{f.cargo}</h3>
        <span className="pill pill-neutra">{f.personas} {f.personas === 1 ? 'persona' : 'personas'}</span>
        {f.preparando ? <span className="pill pill-ia"><span className="orbe trabajando" />La IA lo está preparando</span>
          : f.aprobadoEn ? <span className="pill pill-ok"><IconoCheck />Aprobado por {f.aprobadoPor}</span>
            : f.competencias.length > 0 ? <span className={`pill ${deIa ? 'pill-ia' : 'pill-alerta'}`}>{deIa && <Chispa />}{f.origen} · por aprobar</span>
              : <span className="pill pill-neutra">Sin formulario</span>}
        {editable && !f.preparando && !editando && (
          <div className="derecha">
            <Boton pequeno variante="ia" icono={<Chispa />} cargando={ia.isPending} textoCargando="Pidiendo…" onClick={() => ia.mutate(f.cargo)}>
              {f.competencias.length ? 'Pedir otra propuesta' : 'Pedir a la IA'}
            </Boton>
            {f.competencias.length === 0 && <Boton pequeno cargando={plantilla.isPending} textoCargando="Aplicando…" onClick={() => plantilla.mutate(f.cargo)}>Plantilla base</Boton>}
            {f.competencias.length > 0 && <Boton pequeno onClick={() => setEditando(true)}>Editar</Boton>}
            {f.competencias.length > 0 && !f.aprobadoEn && (
              <Boton pequeno variante="primario" cargando={aprobar.isPending} textoCargando="Aprobando…" onClick={() => aprobar.mutate(f.cargo)}>Aprobar</Boton>
            )}
          </div>
        )}
      </header>
      {f.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{f.error}</p>}
      {error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{error.message}</p>}
      {editando ? <EditorFormulario cicloId={cicloId} f={f} onListo={() => setEditando(false)} /> : f.competencias.length > 0 && (
        <>
          <div className="des-listas">
            <div>
              <p className="des-subtitulo">Competencias</p>
              <ul className="des-lista">{f.competencias.map((x) => <li key={x.nombre}><span>{x.nombre}<small>{x.descripcion}</small></span><span className="peso">{x.peso} %</span></li>)}</ul>
            </div>
            <div>
              <p className="des-subtitulo">Objetivos sugeridos</p>
              <ul className="des-lista">{f.objetivos.map((x) => <li key={x.descripcion}><span>{x.descripcion}<small className="mono">{x.meta}</small></span><span className="peso">{x.peso} %</span></li>)}</ul>
            </div>
          </div>
          {f.razones.length > 0 && !f.aprobadoEn && (
            <div className={deIa ? 'bloque-ia' : 'tarjeta'}>
              {f.razones.map((r) => <div key={r.texto} className="razon"><span className={`signo ${r.signo === '+' ? 'mas' : 'menos'}`}>{r.signo}</span><span className="texto">{r.texto}</span><span className="cita">{r.fuente}</span></div>)}
            </div>
          )}
        </>
      )}
    </section>
  )
}

/** Camino manual: competencias y objetivos se escriben o corrigen a mano; los pesos deben sumar 100. */
function EditorFormulario({ cicloId, f, onListo }: { cicloId: string; f: Formulario; onListo: () => void }) {
  const guardar = useGuardarFormulario(cicloId)
  const [competencias, setCompetencias] = useState<Competencia[]>(f.competencias)
  const [objetivos, setObjetivos] = useState<ObjetivoBase[]>(f.objetivos)
  const sumaC = competencias.reduce((a, x) => a + (Number(x.peso) || 0), 0)
  const sumaO = objetivos.reduce((a, x) => a + (Number(x.peso) || 0), 0)

  return (
    <form className="des-editor" onSubmit={(e) => { e.preventDefault(); guardar.mutate({ cargo: f.cargo, competencias, objetivos }, { onSuccess: onListo }) }}>
      <p className="des-subtitulo">Competencias · <span className={`des-suma ${sumaC !== 100 ? 'mal' : ''}`}>suman {sumaC} %</span></p>
      {competencias.map((x, i) => (
        <div key={i} className="des-editor-fila">
          <input className="entrada" aria-label="Competencia" value={x.nombre} onChange={(e) => setCompetencias((l) => l.map((y, j) => (j === i ? { ...y, nombre: e.target.value } : y)))} required />
          <input className="entrada" aria-label="Descripción" value={x.descripcion} onChange={(e) => setCompetencias((l) => l.map((y, j) => (j === i ? { ...y, descripcion: e.target.value } : y)))} />
          <input className="entrada mono" aria-label="Peso" type="number" min={1} max={100} value={x.peso} onChange={(e) => setCompetencias((l) => l.map((y, j) => (j === i ? { ...y, peso: Number(e.target.value) } : y)))} />
          <Boton pequeno onClick={() => setCompetencias((l) => l.filter((_, j) => j !== i))}>Quitar</Boton>
        </div>
      ))}
      <div><Boton pequeno onClick={() => setCompetencias((l) => [...l, { nombre: '', descripcion: '', peso: 0 }])}>Agregar competencia</Boton></div>
      <p className="des-subtitulo">Objetivos · <span className={`des-suma ${objetivos.length > 0 && sumaO !== 100 ? 'mal' : ''}`}>suman {sumaO} %</span></p>
      {objetivos.map((x, i) => (
        <div key={i} className="des-editor-fila">
          <input className="entrada" aria-label="Objetivo" value={x.descripcion} onChange={(e) => setObjetivos((l) => l.map((y, j) => (j === i ? { ...y, descripcion: e.target.value } : y)))} required />
          <input className="entrada" aria-label="Meta" value={x.meta} onChange={(e) => setObjetivos((l) => l.map((y, j) => (j === i ? { ...y, meta: e.target.value } : y)))} />
          <input className="entrada mono" aria-label="Peso" type="number" min={1} max={100} value={x.peso} onChange={(e) => setObjetivos((l) => l.map((y, j) => (j === i ? { ...y, peso: Number(e.target.value) } : y)))} />
          <Boton pequeno onClick={() => setObjetivos((l) => l.filter((_, j) => j !== i))}>Quitar</Boton>
        </div>
      ))}
      <div><Boton pequeno onClick={() => setObjetivos((l) => [...l, { descripcion: '', meta: '', peso: 0 }])}>Agregar objetivo</Boton></div>
      {guardar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{guardar.error.message}</p>}
      <div className="acciones-formulario">
        <Boton onClick={onListo} disabled={guardar.isPending}>Cancelar</Boton>
        <Boton type="submit" variante="primario" cargando={guardar.isPending} textoCargando="Guardando…" disabled={sumaC !== 100 || (objetivos.length > 0 && sumaO !== 100)}>Guardar</Boton>
      </div>
    </form>
  )
}

/* ---------- Evaluaciones del ciclo ---------- */

const estados = [['', 'Todas'], ['Pendiente', 'Pendientes'], ['EnCurso', 'En curso'], ['Calificada', 'Calificadas']] as const

function Evaluaciones({ c }: { c: Ciclo }) {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const [buscar, setBuscar] = useState('')
  const [estado, setEstado] = useState('')
  const evaluaciones = useEvaluaciones(c.id, { pagina, tamano, buscar, estado })
  const alBuscar = useCallback((t: string) => { setBuscar(t); setPagina(1) }, [setPagina])

  if (c.estado === 'Borrador') return <div className="vacio"><p>Las evaluaciones se crean al abrir el ciclo, con el formulario aprobado de cada cargo.</p></div>

  return (
    <>
      <div className="emp-cifras">
        {estados.slice(1).map(([v, t]) => (
          <button key={v} type="button" className={`emp-cifra ${estado === v ? 'activa' : ''}`} onClick={() => { setEstado(estado === v ? '' : v); setPagina(1) }}>
            <b>{c.conteo[v] ?? 0}</b><span>{t.toLowerCase()}</span>
          </button>
        ))}
        {c.alcance !== 'Grados90' && <div className="emp-cifra"><b>{c.conteo.Autoevaluadas ?? 0}</b><span>autoevaluadas</span></div>}
        <div className="emp-cifra"><b>{decimal(c.promedio)}</b><span>puntaje promedio</span></div>
      </div>
      <section className="tarjeta" aria-label="Evaluaciones">
        <div className="barra-filtros"><Buscador etiqueta="Buscar por nombre, cargo, área o código" onBuscar={alBuscar} /></div>
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead><tr><th>Persona</th><th>Cargo</th><th>Estado</th><th style={{ textAlign: 'right' }}>Puntaje</th><th>Potencial</th><th>Calificó</th></tr></thead>
            <tbody>
              {evaluaciones.isPending
                ? Array.from({ length: 5 }, (_, i) => <tr key={i}>{[50, 40, 20, 15, 15, 30].map((a, j) => <td key={j}><Esqueleto ancho={`${a}%`} /></td>)}</tr>)
                : evaluaciones.data?.items.map((e) => (
                    <tr key={e.id}>
                      <td><Link to={`/consola/desempeno/evaluaciones/${e.id}`} style={{ fontWeight: 600 }}>{e.nombre}</Link><span className="emp-sub mono">{e.codigo}</span></td>
                      <td>{e.cargo}<span className="emp-sub">{e.area}</span></td>
                      <td>
                        <span className={`pill ${e.estado === 'Calificada' ? 'pill-ok' : e.estado === 'EnCurso' ? 'pill-info' : 'pill-neutra'}`}>
                          {e.estado === 'EnCurso' ? 'En curso' : e.estado}
                        </span>
                        {e.autoevaluada && <span className="emp-sub">Se autoevaluó</span>}
                      </td>
                      <td className="num">{decimal(e.puntaje)}</td>
                      <td>{e.potencial ?? '—'}{e.calibrada && <span className="emp-sub">Calibrada</span>}</td>
                      <td style={{ color: 'var(--color-texto-suave)' }}>{e.calificadaPor ?? '—'}</td>
                    </tr>
                  ))}
            </tbody>
          </table>
        </div>
        {evaluaciones.data && evaluaciones.data.total === 0 && <div className="vacio" style={{ padding: 32 }}><p>No hay evaluaciones con ese filtro.</p></div>}
        {evaluaciones.data && <Paginacion pagina={evaluaciones.data} cargando={evaluaciones.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
      </section>
    </>
  )
}

/* ---------- D3 y D4 · Sesgos y matriz 9-box ---------- */

function Calibracion({ c }: { c: Ciclo }) {
  // Filas de arriba hacia abajo: alto, medio y bajo potencial; columnas: bajo, medio y alto desempeño
  const filas = [['Alto', [7, 8, 9]], ['Medio', [4, 5, 6]], ['Bajo', [1, 2, 3]]] as const
  return (
    <div className="des-formularios">
      <section className="tarjeta des-bloque">
        <header><h2>Alertas de sesgo</h2><span className="ayuda">Señales por reglas para el comité, con su evidencia. No cambian ninguna calificación.</span></header>
        {c.sesgos.length === 0 ? <p className="ayuda">Sin alertas: las calificaciones de este ciclo no muestran patrones de sesgo.</p> : (
          <ul className="des-sesgos">
            {c.sesgos.map((s) => <li key={s.texto} className="razon"><span className="signo menos">!</span><span className="texto"><b>{s.tipo}.</b> {s.texto}</span><span className="cita">{s.evidencia}</span></li>)}
          </ul>
        )}
      </section>
      <section className="tarjeta des-bloque">
        <header>
          <h2>Matriz 9-box</h2>
          <span className="ayuda">{c.estado === 'Calibracion' ? 'Abra a una persona para moverla de caja con el motivo del comité.' : 'Se ajusta cuando el ciclo pasa a calibración.'}</span>
        </header>
        <div className="des-matriz">
          {filas.map(([potencial, cajas]) => (
            <div key={potencial} style={{ display: 'contents' }}>
              <span className="des-matriz-eje">Potencial {potencial.toLowerCase()}</span>
              {cajas.map((n) => {
                const caja = c.matriz.find((x) => x.caja === n)
                return (
                  <div key={n} className={`des-caja ${n === 9 || n === 8 || n === 6 ? 'alta' : n === 1 || n === 2 || n === 4 ? 'baja' : ''}`}>
                    <b>{caja?.nombre} · <span className="mono">{caja?.personas.length ?? 0}</span></b>
                    <ul>{caja?.personas.slice(0, 12).map((p) => (
                      <li key={p.evaluacionId}><Link to={`/consola/desempeno/evaluaciones/${p.evaluacionId}`} className={p.calibrada ? 'calibrada' : undefined} title={`${p.cargo} · ${decimal(p.puntaje)}`}>{p.nombre.split(' ').slice(0, 2).join(' ')}</Link></li>
                    ))}</ul>
                  </div>
                )
              })}
            </div>
          ))}
          <span />
          {['Desempeño bajo', 'Desempeño medio', 'Desempeño alto'].map((t) => <span key={t} className="des-matriz-eje">{t}</span>)}
        </div>
      </section>
    </div>
  )
}
