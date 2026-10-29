import { type FormEvent, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoAlerta, IconoCheck } from '../../shared/ui/Iconos'
import { Paginacion } from '../../shared/ui/Paginacion'
import { usePaginacion } from '../../shared/ui/paginado'
import { type FilaPulso, decimal, fecha, useCrearEncuesta, useEncuestas, useProgramarPulsos, usePulsos, useRotacion, useSeguimientoPulso } from './api'
import './clima.css'

type Pestana = 'encuestas' | 'pulsos' | 'rotacion'

/** Clima (Etapa 5): encuestas anónimas con eNPS, temas de los comentarios, pulsos de ingreso y señales de rotación. */
export function Clima() {
  const [parametros, setParametros] = useSearchParams()
  const pestana = (parametros.get('pestana') as Pestana) ?? 'encuestas'
  const pestanas: [Pestana, string][] = [['encuestas', 'Encuestas de clima'], ['pulsos', 'Pulsos de ingreso'], ['rotacion', 'Riesgo de rotación']]
  return (
    <>
      <header className="encabezado">
        <h1>Clima</h1>
        <p>Mida cómo está su gente y actúe a tiempo: encuestas anónimas con eNPS, los temas de los comentarios, pulsos a los 30, 60 y 90 días de ingreso
          y señales de rotación explicadas.</p>
      </header>
      <div role="tablist" aria-label="Clima" className="pestanas">
        {pestanas.map(([clave, texto]) => (
          <button key={clave} type="button" role="tab" className="pestana" aria-selected={pestana === clave} onClick={() => setParametros(clave === 'encuestas' ? {} : { pestana: clave }, { replace: true })}>{texto}</button>
        ))}
      </div>
      <div role="tabpanel">
        {pestana === 'encuestas' && <Encuestas />}
        {pestana === 'pulsos' && <Pulsos />}
        {pestana === 'rotacion' && <Rotacion />}
      </div>
    </>
  )
}

function Encuestas() {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const encuestas = useEncuestas(pagina, tamano)
  const crear = useCrearEncuesta()
  const navegar = useNavigate()
  const [titulo, setTitulo] = useState(`Clima laboral ${new Date().getFullYear()}`)
  const [creando, setCreando] = useState(false)
  const enviar = (e: FormEvent) => { e.preventDefault(); crear.mutate(titulo, { onSuccess: (r) => navegar(`/consola/clima/encuestas/${r.id}`) }) }

  return (
    <div className="des-formularios">
      {creando ? (
        <form className="tarjeta emp-formulario" onSubmit={enviar}>
          <h2>Nueva encuesta de clima</h2>
          <p className="ayuda">Arranca con la plantilla base: seis dimensiones, eNPS y una pregunta abierta. Las áreas y sedes salen de su planta activa. Edítela antes de abrirla.</p>
          <label className="campo">Título<input className="entrada" value={titulo} onChange={(x) => setTitulo(x.target.value)} required /></label>
          {crear.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{crear.error.message}</p>}
          <div className="acciones-formulario">
            <Boton onClick={() => setCreando(false)} disabled={crear.isPending}>Cancelar</Boton>
            <Boton type="submit" variante="primario" cargando={crear.isPending} textoCargando="Creando…">Crear encuesta</Boton>
          </div>
        </form>
      ) : <div><Boton variante="primario" onClick={() => setCreando(true)}>Crear encuesta</Boton></div>}
      <section className="tarjeta" aria-label="Encuestas">
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead><tr><th>Encuesta</th><th>Estado</th><th className="num">Respuestas</th><th>Abierta</th><th>Cerrada</th></tr></thead>
            <tbody>
              {encuestas.isPending
                ? Array.from({ length: 3 }, (_, i) => <tr key={i}>{[50, 20, 15, 20, 20].map((a, j) => <td key={j}><Esqueleto ancho={`${a}%`} /></td>)}</tr>)
                : encuestas.data?.items.map((e) => (
                    <tr key={e.id}>
                      <td><Link to={`/consola/clima/encuestas/${e.id}`} style={{ fontWeight: 600 }}>{e.titulo}</Link><span className="emp-sub">{e.preguntas} preguntas</span></td>
                      <td><span className={`pill ${e.estado === 'Abierta' ? 'pill-info' : e.estado === 'Cerrada' ? 'pill-ok' : 'pill-neutra'}`}>{e.estado}</span></td>
                      <td className="num">{e.respuestas}</td>
                      <td className="mono" style={{ fontSize: 13 }}>{fecha(e.abiertaEn)}</td>
                      <td className="mono" style={{ fontSize: 13 }}>{fecha(e.cerradaEn)}</td>
                    </tr>
                  ))}
            </tbody>
          </table>
        </div>
        {encuestas.data && encuestas.data.total === 0 && <div className="vacio" style={{ padding: 32 }}><h2>Aún no hay encuestas</h2><p>Cree la primera con la plantilla base.</p></div>}
        {encuestas.data && <Paginacion pagina={encuestas.data} cargando={encuestas.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
      </section>
    </div>
  )
}

/* ---------- C3 · Pulsos de ingreso ---------- */

const filtros = [['', 'Todos'], ['alertas', 'En alerta'], ['sinSeguimiento', 'Sin conversación'], ['pendientes', 'Sin responder'], ['respondidos', 'Respondidos']] as const

function Pulsos() {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const [filtro, setFiltro] = useState('')
  const pulsos = usePulsos({ pagina, tamano, filtro })
  const programar = useProgramarPulsos()

  return (
    <div className="des-formularios">
      <div className="des-barra">
        <div className="des-pasos" role="group" aria-label="Filtrar pulsos">
          {filtros.map(([v, t]) => <button key={v} type="button" className={`des-paso ${filtro === v ? 'actual' : ''}`} onClick={() => { setFiltro(v); setPagina(1) }}>{t}</button>)}
        </div>
        <span className="vin-acciones">
          {programar.isSuccess && <span className="ayuda">{programar.data.cantidad} pulsos nuevos</span>}
          <Boton pequeno cargando={programar.isPending} textoCargando="Programando…" onClick={() => programar.mutate(undefined)}
            title="Se programan solos cada seis horas; esto los programa ya">Programar pulsos ahora</Boton>
        </span>
      </div>
      <p className="ayuda">Cada persona responde en su portal a los 30, 60 y 90 días de su ingreso. No es anónimo: solo lo ve Recursos Humanos para conversar a tiempo.</p>
      <section className="tarjeta" aria-label="Pulsos">
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead><tr><th>Persona</th><th>Pulso</th><th className="num">Promedio</th><th className="num">Recomendaría</th><th>Comentario</th><th>Seguimiento</th></tr></thead>
            <tbody>
              {pulsos.isPending
                ? Array.from({ length: 4 }, (_, i) => <tr key={i}>{[40, 20, 10, 10, 40, 30].map((a, j) => <td key={j}><Esqueleto ancho={`${a}%`} /></td>)}</tr>)
                : pulsos.data?.items.map((p) => <FilaPulsoTabla key={p.id} p={p} />)}
            </tbody>
          </table>
        </div>
        {pulsos.data && pulsos.data.total === 0 && <div className="vacio" style={{ padding: 32 }}><p>No hay pulsos con ese filtro.</p></div>}
        {pulsos.data && <Paginacion pagina={pulsos.data} cargando={pulsos.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
      </section>
    </div>
  )
}

function FilaPulsoTabla({ p }: { p: FilaPulso }) {
  const seguir = useSeguimientoPulso()
  const [nota, setNota] = useState('')
  const [abierta, setAbierta] = useState(false)
  return (
    <tr>
      <td><Link to={`/consola/empleados/${p.empleadoId}`} style={{ fontWeight: 600 }}>{p.nombre}</Link><span className="emp-sub">{p.cargo} · {p.area}</span></td>
      <td><span className="mono">{p.hito} días</span><span className="emp-sub mono">desde {fecha(p.pendienteDesde)}</span></td>
      <td className="num">{p.respondido ? decimal(p.promedio) : '—'}</td>
      <td className="num">{p.recomendaria ?? '—'}</td>
      <td style={{ maxWidth: 280 }}>
        {!p.respondido ? <span className="pill pill-neutra">Sin responder</span> : p.alerta ? <span className="pill pill-alerta"><IconoAlerta />Alerta</span> : <span className="pill pill-ok">Bien</span>}
        {p.comentario && <span className="emp-sub">«{p.comentario}»</span>}
      </td>
      <td style={{ minWidth: 220 }}>
        {p.seguimiento ? <span className="ayuda"><IconoCheck /> {p.seguimiento} · {p.seguimientoPor}</span>
          : p.alerta && (abierta ? (
            <span className="des-editor">
              <textarea className="area-texto" rows={2} placeholder="Qué conversaron y qué acordaron" value={nota} onChange={(x) => setNota(x.target.value)} />
              <Boton pequeno variante="primario" disabled={!nota.trim()} cargando={seguir.isPending} textoCargando="Guardando…" onClick={() => seguir.mutate({ id: p.id, nota })}>Registrar</Boton>
            </span>
          ) : <Boton pequeno onClick={() => setAbierta(true)}>Registrar conversación</Boton>)}
        {seguir.error && <span className="postular-nota-error">{seguir.error.message}</span>}
      </td>
    </tr>
  )
}

/* ---------- C4 · Riesgo de rotación ---------- */

function Rotacion() {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const [nivel, setNivel] = useState('')
  const rotacion = useRotacion({ pagina, tamano, nivel })
  const d = rotacion.data

  return (
    <div className="des-formularios">
      <div className="emp-cifras">
        {(['Alto', 'Medio', 'Bajo'] as const).map((n) => (
          <button key={n} type="button" className={`emp-cifra ${n === 'Alto' ? 'alerta' : ''} ${nivel === n ? 'activa' : ''}`} onClick={() => { setNivel(nivel === n ? '' : n); setPagina(1) }}>
            {d ? <b>{n === 'Alto' ? d.altos : n === 'Medio' ? d.medios : d.bajos}</b> : <Esqueleto alto={28} ancho={40} />}<span>riesgo {n.toLowerCase()}</span>
          </button>
        ))}
      </div>
      <p className="ayuda">Reglas explicadas, no un modelo predictivo: pulso de ingreso, antigüedad, fin de contrato, clima del área (solo con 5 o más respuestas) y desempeño.
        Nunca se usan edad, género, estado civil ni otros datos protegidos. El nivel orienta una conversación, no una decisión sobre la persona.</p>
      <section className="tarjeta" aria-label="Riesgo de rotación">
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead><tr><th>Persona</th><th>Riesgo</th><th>Señales</th><th>Qué hacer</th></tr></thead>
            <tbody>
              {rotacion.isPending
                ? Array.from({ length: 4 }, (_, i) => <tr key={i}>{[40, 15, 50, 40].map((a, j) => <td key={j}><Esqueleto ancho={`${a}%`} /></td>)}</tr>)
                : d?.personas.items.map((r) => (
                    <tr key={r.empleadoId}>
                      <td><Link to={`/consola/empleados/${r.empleadoId}`} style={{ fontWeight: 600 }}>{r.nombre}</Link><span className="emp-sub">{r.cargo} · {r.area}</span></td>
                      <td><span className={`pill ${r.nivel === 'Alto' ? 'pill-error' : r.nivel === 'Medio' ? 'pill-alerta' : 'pill-ok'}`}>{r.nivel}</span><span className="emp-sub mono">{r.puntos} puntos</span></td>
                      <td>{r.senales.length === 0 ? <span className="ayuda">Sin señales</span> : (
                        <ul className="cli-senales">{r.senales.map((s) => <li key={s.texto}><span>{s.texto}</span><span className="cita">{s.evidencia}</span></li>)}</ul>
                      )}</td>
                      <td>{r.acciones.length > 0 && <ul className="cli-acciones">{r.acciones.map((a) => <li key={a}>{a}</li>)}</ul>}</td>
                    </tr>
                  ))}
            </tbody>
          </table>
        </div>
        {d && <Paginacion pagina={d.personas} cargando={rotacion.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
      </section>
    </div>
  )
}
