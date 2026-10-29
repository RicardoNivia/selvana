import { type CSSProperties, Fragment, type ReactNode, useCallback, useEffect, useState } from 'react'
import { createPortal } from 'react-dom'
import { Link } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import {
  Chispa, IconoAlerta, IconoChat, IconoCheck, IconoCerrar, IconoDocumento, IconoEscudo, IconoFlecha, IconoInsignia, IconoPrueba, IconoReloj,
} from '../../shared/ui/Iconos'
import { Buscador, Paginacion } from '../../shared/ui/Paginacion'
import { useContador } from '../../shared/ui/animaciones'
import { usePaginacion } from '../../shared/ui/paginado'
import { PreseleccionPostulante } from '../preseleccion/PreseleccionPostulante'
import { usePuede } from '../sesion/api'
import { AprobarContratacion } from '../vinculacion/AprobarContratacion'
import { AbrirHojaDeVida } from './AbrirHojaDeVida'
import { type Etapa, type Vacante, useProponerTerna } from './api'
import {
  type Avance, type EtapaProceso, type PersonaProceso, type Proceso, type ReglasAvance, enlaceDe, nombreEtapa, queHace, useAplicarSugerencias, useAvanzar,
  useGuardarReglas, useProceso,
} from './procesoApi'
import { TableroVacante } from './TableroVacante'
import './proceso.css'

const flujo: Etapa[] = ['Recibida', 'Preseleccion', 'Pruebas', 'Terna']
const etapas: Etapa[] = [...flujo, 'Descartada']
const iconoEtapa: Record<Etapa, ReactNode> = {
  Recibida: <IconoDocumento />, Preseleccion: <IconoChat />, Pruebas: <IconoPrueba />, Terna: <IconoInsignia />, Descartada: <IconoCerrar />,
}
const claseEtapa = (e: Etapa) => `etapa-${e.toLowerCase()}`
const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const pesos = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 })
const iniciales = (n: string) => n.split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p[0]!.toUpperCase()).join('')

/** Pasos que una persona puede aceptar en bloque (descartar va uno por uno, con su motivo). */
const aceptable = (p: PersonaProceso) =>
  (p.sugerencia.accion === 'Avanzar' && p.sugerencia.destino !== 'Descartada') || p.sugerencia.accion === 'Invitar' || p.sugerencia.accion === 'EnviarPruebas'

/**
 * El proceso de selección de la vacante, pensado como lo haría un psicólogo organizacional:
 * mismos criterios para todos (selección estructurada), cada paso con su evidencia, la IA propone y una persona decide.
 */
/** «encabezado»: el espacio del header verde de la vacante donde se pinta el embudo. */
export function ProcesoSeleccion({ vacante, encabezado }: { vacante: Vacante; encabezado: HTMLElement | null }) {
  const [etapa, setEtapa] = useState<Etapa | null>(null)
  const [porDecidir, setPorDecidir] = useState(false)
  const [buscar, setBuscar] = useState('')
  const [vista, setVista] = useState<'tarjetas' | 'tablero'>('tarjetas')
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const proceso = useProceso(vacante.id, { etapa, porDecidir, buscar, pagina, tamano })
  const alBuscar = useCallback((t: string) => { setBuscar(t); setPagina(1) }, [setPagina])
  const [seleccion, setSeleccion] = useState<Set<string>>(new Set())
  const [abierta, setAbierta] = useState<PersonaProceso>()
  const [aviso, setAviso] = useState<{ nombre: string; avance: Avance }>()
  const [editandoReglas, setEditandoReglas] = useState(false)
  const elegir = (e: Etapa | null) => { setEtapa(e); setPagina(1); setSeleccion(new Set()) }

  if (proceso.isPending) return <EsqueletoProceso />
  if (proceso.isError) return <p role="alert" className="aviso aviso-error"><IconoAlerta />{proceso.error.message}</p>
  const d = proceso.data

  return (
    <div className="proceso">
      {encabezado && createPortal(<Embudo etapas={d.etapas} contratados={d.contratados} elegida={etapa} onElegir={elegir} />, encabezado)}

      <AgenteSeleccion proceso={d} vacante={vacante} seleccion={seleccion} editando={editandoReglas} onEditar={setEditandoReglas}
        onAplicado={() => setSeleccion(new Set())} />
      {editandoReglas && <EditorReglas vacanteId={vacante.id} reglas={d.reglas} tieneBateria={d.tieneBateria} onListo={() => setEditandoReglas(false)} />}

      {aviso && <AvisoAvance {...aviso} onCerrar={() => setAviso(undefined)} />}

      <section className="tarjeta proceso-lista" aria-label="Personas del proceso">
        <div className="barra-filtros proceso-barra">
          <h2 className={etapa ? claseEtapa(etapa) : undefined}>
            {etapa ? <><span className="proceso-punto" />{nombreEtapa[etapa]}</> : 'Todas en proceso'}
            <span className="mono">{d.personas.total}</span>
          </h2>
          <Buscador etiqueta="Buscar por nombre o número" onBuscar={alBuscar} />
          <div className="segmentado" role="group" aria-label="Mostrar">
            <button type="button" aria-pressed={!porDecidir} onClick={() => { setPorDecidir(false); setPagina(1) }}>Todas</button>
            <button type="button" aria-pressed={porDecidir} onClick={() => { setPorDecidir(true); setPagina(1) }}>Por decidir</button>
          </div>
          <div className="segmentado" role="group" aria-label="Vista">
            <button type="button" aria-pressed={vista === 'tarjetas'} onClick={() => setVista('tarjetas')}>Tarjetas</button>
            <button type="button" aria-pressed={vista === 'tablero'} onClick={() => setVista('tablero')}>Tablero</button>
          </div>
        </div>

        {vista === 'tablero' ? <div className="proceso-tablero"><TableroVacante vacante={vacante} /></div> : (
          <>
            {d.personas.items.length === 0 ? (
              <div className="vacio proceso-vacio">
                <h2>{porDecidir ? 'Nada por decidir aquí' : 'Nadie en esta etapa'}</h2>
                <p>{porDecidir ? 'La IA no tiene pasos pendientes en esta etapa: está esperando a los candidatos.' : etapa === 'Descartada' ? 'Nadie ha sido descartado.' : 'Cuando lleguen postulaciones, aparecerán aquí con su siguiente paso.'}</p>
              </div>
            ) : (
              <ol className="proceso-personas">
                {d.personas.items.map((p, i) => (
                  <TarjetaPersona key={p.postulacionId} persona={p} indice={i} modo={d.reglas.modo}
                    seleccionada={seleccion.has(p.postulacionId)}
                    onSeleccionar={(si) => setSeleccion((s) => { const n = new Set(s); if (si) n.add(p.postulacionId); else n.delete(p.postulacionId); return n })}
                    onAbrir={() => setAbierta(p)} onAvance={(avance) => setAviso({ nombre: p.nombre, avance })} />
                ))}
              </ol>
            )}
            <Paginacion pagina={d.personas} cargando={proceso.isPlaceholderData} onCambiar={(n) => { setPagina(n); setSeleccion(new Set()) }} onCambiarTamano={setTamano} />
          </>
        )}
      </section>

      {abierta && <PanelPersona persona={abierta} onCerrar={() => setAbierta(undefined)} />}
    </div>
  )
}

/* ---------- Embudo en el encabezado verde: una ficha por etapa con su color, cuántos están y qué espera decisión ---------- */

function Embudo({ etapas: datos, contratados, elegida, onElegir }: { etapas: EtapaProceso[]; contratados: number; elegida: Etapa | null; onElegir: (e: Etapa | null) => void }) {
  const de = (e: Etapa) => datos.find((x) => x.etapa === e) ?? { etapa: e, total: 0, alcanzaron: 0, porDecidir: 0, esperando: 0 }
  const base = Math.max(1, de('Recibida').alcanzaron)
  const descartadas = de('Descartada')
  return (
    <nav className="eh" aria-label="Etapas del proceso">
      {flujo.map((e, i) => (
        <Fragment key={e}>
          {i > 0 && <IconoFlecha className="eh-flecha" aria-hidden="true" />}
          <NodoEtapa dato={de(e)} base={base} indice={i} activo={elegida === e} onElegir={() => onElegir(elegida === e ? null : e)} />
        </Fragment>
      ))}
      <span className="eh-sep" aria-hidden="true" />
      <button type="button" className={`eh-nodo eh-chico ${claseEtapa('Descartada')}`} aria-pressed={elegida === 'Descartada'} title={queHace.Descartada}
        onClick={() => onElegir(elegida === 'Descartada' ? null : 'Descartada')} style={{ '--i': 4 } as CSSProperties}>
        <span className="eh-punto" /><span className="eh-nombre">Descartadas</span><b className="eh-total">{descartadas.total}</b>
      </button>
      {contratados > 0 && <span className="eh-contratados" title="Contratación aprobada: siguen en Vinculación"><IconoCheck /><b className="mono">{contratados}</b> en vinculación</span>}
    </nav>
  )
}

function NodoEtapa({ dato, base, indice, activo, onElegir }: { dato: EtapaProceso; base: number; indice: number; activo: boolean; onElegir: () => void }) {
  const total = useContador(0, dato.total, true, 900)
  const conversion = Math.round((100 * dato.alcanzaron) / base)
  return (
    <button type="button" className={`eh-nodo ${claseEtapa(dato.etapa)}`} aria-pressed={activo} onClick={onElegir} style={{ '--i': indice } as CSSProperties}
      title={`${queHace[dato.etapa]} · ${dato.etapa === 'Recibida' ? `${dato.alcanzaron} en total` : `${conversion} % llegó aquí`}`}
      aria-label={`${nombreEtapa[dato.etapa]}: ${dato.total} hoy, ${dato.porDecidir} por decidir, ${dato.esperando} esperando`}>
      <span className="eh-punto" />
      <span className="eh-nombre">{nombreEtapa[dato.etapa]}</span>
      <b className="eh-total">{total}</b>
      {dato.porDecidir > 0 && <span className="eh-ia" title="Pasos que sugiere la IA y esperan su decisión"><Chispa />{dato.porDecidir}</span>}
      {dato.esperando > 0 && <span className="eh-espera" title="Esperando al candidato"><IconoReloj />{dato.esperando}</span>}
      <span className="eh-barra" aria-hidden="true"><span style={{ width: `${conversion}%` }} /></span>
    </button>
  )
}

/* ---------- El agente de selección: protagonista en violeta, pero la decisión es de una persona ---------- */

function AgenteSeleccion({ proceso: d, vacante, seleccion, editando, onEditar, onAplicado }: {
  proceso: Proceso; vacante: Vacante; seleccion: Set<string>; editando: boolean; onEditar: (v: boolean) => void; onAplicado: () => void
}) {
  const aplicar = useAplicarSugerencias(vacante.id)
  const proponer = useProponerTerna(vacante.id)
  const enPagina = d.personas.items.filter(aceptable).map((p) => p.postulacionId)
  const ids = seleccion.size > 0 ? [...seleccion] : enPagina
  const automatico = d.reglas.modo === 'Automatico'
  const listos = useContador(0, d.sugerenciasListas, true, 900)

  return (
    <section className={`agente-banda ${automatico ? 'automatico' : ''}`} aria-label="Agente de selección">
      <span className="agente-orbe" aria-hidden="true"><span /><span /><Chispa /></span>
      <div className="agente-texto">
        <span className="agente-nombre">Agente de selección · {automatico ? 'Automático con límites' : 'Sugerir'}</span>
        <p>
          {d.sugerenciasListas > 0
            ? <><b className="mono">{listos}</b> {d.sugerenciasListas === 1 ? 'paso listo' : 'pasos listos'} para aceptar. </>
            : 'Revisé a todas las personas: no hay pasos listos. '}
          {automatico
            ? 'Invito al chat y envío pruebas solo; descartar y la terna los decide una persona.'
            : `Aplico los mismos criterios a todos: afinidad de ${d.reglas.umbralAfinidad} o más, excluyentes y aspiración hasta +${d.reglas.toleranciaSalarial} %.`}
        </p>
        {(aplicar.error ?? proponer.error) && <span role="alert" className="error-formulario">{(aplicar.error ?? proponer.error)!.message}</span>}
        {aplicar.data && <span role="status" className="agente-hecho"><IconoCheck />Aplicados {aplicar.data.aplicadas} {aplicar.data.aplicadas === 1 ? 'paso' : 'pasos'}. Quedan en la bitácora con su nombre.</span>}
        {proponer.data && <span role="status" className="agente-hecho"><IconoCheck />La terna propuesta quedó en la <Link to="/consola/bandeja">bandeja</Link> para aprobación.</span>}
      </div>
      <div className="agente-acciones">
        {!editando && ids.length > 0 && (
          <Boton variante="primario" icono={<IconoCheck />} cargando={aplicar.isPending} textoCargando="Aplicando…" onClick={() => aplicar.mutate(ids, { onSuccess: onAplicado })}>
            {seleccion.size > 0 ? `Aceptar ${seleccion.size} ${seleccion.size === 1 ? 'seleccionado' : 'seleccionados'}` : `Aceptar ${ids.length} de esta página`}
          </Boton>
        )}
        <Boton variante="ia" pequeno icono={<Chispa />} cargando={proponer.isPending} textoCargando="Armando…" onClick={() => proponer.mutate()}>Proponer terna</Boton>
        <Boton pequeno aria-expanded={editando} onClick={() => onEditar(!editando)}>{editando ? 'Cerrar reglas' : 'Reglas de avance'}</Boton>
      </div>
    </section>
  )
}

function EditorReglas({ vacanteId, reglas, tieneBateria, onListo }: { vacanteId: string; reglas: ReglasAvance; tieneBateria: boolean; onListo: () => void }) {
  const guardar = useGuardarReglas(vacanteId)
  const [r, setR] = useState(reglas)
  const filas: [Etapa, Etapa, string, boolean][] = [
    ['Recibida', 'Preseleccion', `Cumple los excluyentes y su afinidad es de ${r.umbralAfinidad} o más`, true],
    ['Preseleccion', tieneBateria ? 'Pruebas' : 'Terna', `Terminó el chat, confirma interés y aspira hasta ${r.toleranciaSalarial} % sobre el máximo`, tieneBateria],
    ['Pruebas', 'Terna', 'Presentó todas las pruebas y supera los puntajes de corte', false],
    ['Recibida', 'Descartada', 'No cumple un excluyente o dijo que no le interesa: la IA lo sugiere con el motivo', false],
  ]
  return (
    <form className="tarjeta reglas-editor panel-decision" onSubmit={(e) => { e.preventDefault(); guardar.mutate(r, { onSuccess: onListo }) }}>
      <header>
        <h2>Reglas de avance</h2>
        <p className="ayuda">Selección estructurada: los mismos criterios para todas las personas, visibles y ajustables. La prueba y el puntaje orientan; la decisión es suya.</p>
      </header>
      <div className="segmentado reglas-modo" role="group" aria-label="Autonomía del agente">
        <button type="button" aria-pressed={r.modo === 'Sugerir'} onClick={() => setR({ ...r, modo: 'Sugerir' })}>Sugerir</button>
        <button type="button" aria-pressed={r.modo === 'Automatico'} onClick={() => setR({ ...r, modo: 'Automatico' })}>Automático con límites</button>
      </div>
      <p className="ayuda">{r.modo === 'Sugerir'
        ? 'La IA propone el siguiente paso de cada persona y usted lo acepta, uno a uno o en bloque.'
        : 'Cada cinco minutos la IA invita al chat y envía pruebas a quien cumple. Nunca descarta ni arma la terna sola.'}</p>
      <div className="reglas-campos">
        <label className="campo">Afinidad mínima para preselección
          <span className="reglas-rango"><input type="range" min={0} max={100} step={5} value={r.umbralAfinidad} onChange={(e) => setR({ ...r, umbralAfinidad: Number(e.target.value) })} /><b className="mono">{r.umbralAfinidad}</b></span>
        </label>
        <label className="campo">Tolerancia sobre el salario máximo
          <span className="reglas-rango"><input type="range" min={0} max={50} step={5} value={r.toleranciaSalarial} onChange={(e) => setR({ ...r, toleranciaSalarial: Number(e.target.value) })} /><b className="mono">+{r.toleranciaSalarial} %</b></span>
        </label>
      </div>
      <ol className="reglas-mapa">
        {filas.map(([de, a, texto, ia]) => (
          <li key={`${de}-${a}`}>
            <span className={`reglas-etapa ${claseEtapa(de)}`}>{nombreEtapa[de]}</span><IconoFlecha /><span className={`reglas-etapa ${claseEtapa(a)}`}>{nombreEtapa[a]}</span>
            <span className="reglas-texto">{texto}</span>
            {ia && r.modo === 'Automatico' ? <span className="pill pill-ia">La IA lo hace sola</span> : <span className="pill pill-neutra">Lo decide una persona</span>}
          </li>
        ))}
      </ol>
      <p className="protegidas"><IconoEscudo />No se usan para avanzar ni descartar: edad, género, estado civil, foto ni dirección exacta.</p>
      {guardar.error && <span role="alert" className="error-formulario">{guardar.error.message}</span>}
      <div className="decision">
        <Boton type="submit" variante="primario" cargando={guardar.isPending} textoCargando="Guardando…">Guardar reglas</Boton>
        <Boton onClick={onListo} disabled={guardar.isPending}>Cancelar</Boton>
        <span className="registro">Queda en la bitácora con su nombre</span>
      </div>
    </form>
  )
}

/* ---------- Cada persona: dónde está, qué se sabe y cuál es su siguiente paso ---------- */

const textoAccion = (p: PersonaProceso): string | null => {
  const s = p.sugerencia
  switch (s.accion) {
    case 'Avanzar': return s.destino ? `Pasar a ${nombreEtapa[s.destino]}` : null
    case 'Invitar': return 'Invitar al chat'
    case 'EnviarPruebas': return 'Enviar pruebas'
    default: return null
  }
}

function TarjetaPersona({ persona: p, indice, modo, seleccionada, onSeleccionar, onAbrir, onAvance }: {
  persona: PersonaProceso; indice: number; modo: string; seleccionada: boolean; onSeleccionar: (si: boolean) => void; onAbrir: () => void; onAvance: (a: Avance) => void
}) {
  const avanzar = useAvanzar()
  const contrata = usePuede('AprobarBandeja')
  const [descartando, setDescartando] = useState(false)
  const [motivo, setMotivo] = useState('')
  const [contratando, setContratando] = useState(false)
  const [destinoManual, setDestinoManual] = useState<Etapa | ''>('')
  const s = p.sugerencia
  const accion = textoAccion(p)
  const destino: Etapa | null = s.accion === 'Invitar' ? 'Preseleccion' : s.accion === 'EnviarPruebas' ? 'Pruebas' : s.destino
  const mover = (a: Etapa, m: string | null) => avanzar.mutate({ postulacionId: p.postulacionId, destino: a, motivo: m }, { onSuccess: (r) => { onAvance(r); setDescartando(false) } })
  const empezarDescarte = () => { setMotivo(s.accion === 'Descartar' ? s.titulo : ''); setDescartando(true) }
  const demora = s.accion === 'Esperar' && p.diasEnEtapa >= 5
  const ia = s.accion !== 'Ninguna' && s.accion !== 'Contratar'

  return (
    <li className={`persona ${claseEtapa(p.etapa)} ${seleccionada ? 'seleccionada' : ''}`} style={{ '--i': indice } as CSSProperties}>
      <div className="persona-cabeza">
        {aceptable(p) && <input type="checkbox" className="persona-check" checked={seleccionada} onChange={(e) => onSeleccionar(e.target.checked)} aria-label={`Seleccionar a ${p.nombre}`} />}
        <span className="persona-avatar" aria-hidden="true">{iniciales(p.nombre)}</span>
        <div className="persona-quien">
          <Link to={`/consola/candidatos/${p.candidatoId}`} className="persona-nombre">{p.nombre}</Link>
          <span className="persona-meta"><span className="mono">{p.numero}</span> · {p.ciudad || 'Sin ciudad'} · <span className="persona-etapa"><span className="proceso-punto" />{nombreEtapa[p.etapa]}</span></span>
        </div>
        <Anillo valor={p.puntaje} ia={!p.ajustado} calculando={p.estadoAfinidad === 'Calculando'} />
      </div>

      <ul className="persona-senales" aria-label="Señales">
        <Senal etiqueta="Excluyentes" estado={p.resultadoFiltros === 'NoCumple' ? 'no' : p.resultadoFiltros === 'Cumple' || p.resultadoFiltros === 'Rescatada' ? 'si' : 'nada'}
          texto={p.resultadoFiltros === 'SinFiltros' ? 'Sin filtros' : p.resultadoFiltros === 'NoCumple' ? 'No cumple' : p.resultadoFiltros === 'SinEvidencia' ? 'Sin evidencia' : p.resultadoFiltros === 'Rescatada' ? 'Rescatada' : 'Cumple'} />
        <Senal etiqueta="Chat" estado={p.preseleccion?.estado === 'Completada' ? (p.preseleccion.interesado === false ? 'no' : 'si') : p.preseleccion ? 'espera' : 'nada'}
          texto={p.preseleccion ? ({ Invitada: 'Invitada', Abierta: 'Respondiendo', Completada: p.preseleccion.interesado === false ? 'Sin interés' : 'Completo', Escalada: 'Pide una persona' } as Record<string, string>)[p.preseleccion.estado] ?? p.preseleccion.estado : 'Sin invitar'} />
        <Senal etiqueta="Pruebas" estado={p.pruebas?.estado === 'Terminada' ? (p.pruebas.cumpleCortes === false ? 'no' : 'si') : p.pruebas ? 'espera' : 'nada'}
          texto={p.pruebas ? (p.pruebas.estado === 'Terminada' ? `Promedio ${p.pruebas.promedio ?? '—'}` : `${p.pruebas.terminadas}/${p.pruebas.total}`) : 'Sin enviar'} />
        <li className={`senal ${demora ? 'senal-demora' : ''}`}><IconoReloj /><span>{p.diasEnEtapa === 0 ? 'Hoy' : `${p.diasEnEtapa} ${p.diasEnEtapa === 1 ? 'día' : 'días'}`} en la etapa</span></li>
      </ul>

      {ia && (
        <div className={`persona-sugerencia accion-${s.accion.toLowerCase()}`}>
          <div className="sugerencia-cabeza"><Chispa /><span>La IA sugiere</span><b>{s.titulo}</b>
            {s.automatica && modo === 'Automatico' && <span className="sugerencia-auto">la hará sola</span>}</div>
          {s.criterios.length > 0 && (
            <ul className="sugerencia-criterios">
              {s.criterios.map((c) => (
                <li key={c.texto} className={c.cumple === true ? 'cumple' : c.cumple === false ? 'falla' : 'pendiente'}>
                  <span className="criterio-signo" aria-label={c.cumple === true ? 'Cumple' : c.cumple === false ? 'No cumple' : 'Aún no se sabe'}>{c.cumple === true ? '✓' : c.cumple === false ? '✕' : '·'}</span>
                  <span>{c.texto}</span><span className="cita">{c.evidencia}</span>
                </li>
              ))}
            </ul>
          )}
          {demora && <p className="sugerencia-demora"><IconoAlerta />Lleva {p.diasEnEtapa} días sin responder: escríbale o reenvíe el enlace. Una espera larga desgasta la experiencia del candidato.</p>}
        </div>
      )}
      {p.etapa === 'Descartada' && p.motivoDescarte && <p className="persona-motivo">Motivo: {p.motivoDescarte}</p>}

      {descartando ? (
        <form className="persona-descarte" onSubmit={(e) => { e.preventDefault(); mover('Descartada', motivo.trim()) }}>
          <label>¿Por qué la descarta? <span className="ayuda">Queda en su historial; sea concreto y respetuoso</span>
            <input className="entrada" value={motivo} onChange={(e) => setMotivo(e.target.value)} placeholder="No tiene la licencia C1 que pide la vacante" autoFocus /></label>
          <div className="decision">
            <Boton type="submit" disabled={!motivo.trim()} cargando={avanzar.isPending} textoCargando="Descartando…">Descartar con motivo</Boton>
            <Boton onClick={() => setDescartando(false)} disabled={avanzar.isPending}>Cancelar</Boton>
          </div>
        </form>
      ) : (
        <div className="persona-acciones">
          {accion && destino && (
            <Boton className={`boton-etapa ${claseEtapa(destino)}`} icono={<IconoFlecha />} cargando={avanzar.isPending} textoCargando="Aplicando…" onClick={() => mover(destino, null)}>{accion}</Boton>
          )}
          {s.accion === 'Descartar' && <Boton icono={<IconoCerrar />} onClick={empezarDescarte}>Descartar con motivo</Boton>}
          {s.accion === 'Esperar' && (p.etapa === 'Preseleccion' || p.etapa === 'Pruebas') && (
            <Boton cargando={avanzar.isPending} textoCargando="Renovando…" onClick={() => mover(p.etapa, null)}>Reenviar enlace</Boton>
          )}
          {s.accion === 'Contratar' && contrata && !p.procesoVinculacionId && <Boton icono={<IconoCheck />} onClick={() => setContratando(true)}>Aprobar contratación</Boton>}
          {p.procesoVinculacionId && <Link className="boton boton-secundario" to={`/consola/vinculacion/${p.procesoVinculacionId}`}>Ver vinculación</Link>}
          <span className="persona-manual">
            <select className="selector" aria-label={`Mover a ${p.nombre} a otra etapa`} value={destinoManual}
              onChange={(e) => { const v = e.target.value as Etapa | ''; setDestinoManual(''); if (v === 'Descartada') empezarDescarte(); else if (v) mover(v, null) }}>
              <option value="">Mover a…</option>
              {etapas.filter((e) => e !== p.etapa).map((e) => <option key={e} value={e}>{e === 'Descartada' ? 'Descartar…' : nombreEtapa[e]}</option>)}
            </select>
            <Boton pequeno onClick={onAbrir}>Detalle</Boton>
          </span>
        </div>
      )}
      {avanzar.error && <span role="alert" className="error-formulario">{avanzar.error.message}</span>}
      {contratando && <AprobarContratacion postulacionId={p.postulacionId} onCerrar={() => setContratando(false)} />}
    </li>
  )
}

function Senal({ etiqueta, estado, texto }: { etiqueta: string; estado: 'si' | 'no' | 'espera' | 'nada'; texto: string }) {
  return <li className={`senal senal-${estado}`}><span className="senal-etiqueta">{etiqueta}</span><span>{texto}</span></li>
}

/** Puntaje de afinidad como anillo: violeta mientras es de la IA, tinta cuando una persona lo ajustó. */
function Anillo({ valor, ia, calculando }: { valor: number | null; ia: boolean; calculando: boolean }) {
  const r = 19
  const c = 2 * Math.PI * r
  return (
    <span className={`anillo ${ia ? 'anillo-ia' : ''} ${calculando ? 'calculando' : ''}`} title={calculando ? 'La IA la está calificando' : ia ? 'Afinidad calculada por la IA' : 'Afinidad ajustada por una persona'}>
      <svg viewBox="0 0 48 48" aria-hidden="true">
        <circle cx="24" cy="24" r={r} className="anillo-pista" />
        {valor !== null && <circle cx="24" cy="24" r={r} className="anillo-valor" style={{ '--c': c, strokeDasharray: c, strokeDashoffset: c * (1 - valor / 100) } as CSSProperties} />}
      </svg>
      <b className="mono">{calculando ? '…' : valor ?? '—'}</b>
    </span>
  )
}

/* ---------- Después de un paso: qué pasó y el enlace para compartir mientras llegan WhatsApp y correo ---------- */

function AvisoAvance({ nombre, avance, onCerrar }: { nombre: string; avance: Avance; onCerrar: () => void }) {
  const enlace = enlaceDe(avance)
  const [copiado, setCopiado] = useState(false)
  useEffect(() => { if (!enlace) { const t = setTimeout(onCerrar, 6000); return () => clearTimeout(t) } }, [enlace, onCerrar])
  return (
    <div className={`aviso-avance ${claseEtapa(avance.etapa)}`} role="status">
      <span className="aviso-avance-icono">{iconoEtapa[avance.etapa]}</span>
      <div>
        <b>{avance.etapa === 'Descartada' ? `${nombre} quedó descartada con su motivo` : `${nombre} está en ${nombreEtapa[avance.etapa]}`}</b>
        {enlace && (
          <p>{avance.tipoEnlace === 'Pruebas' ? 'Las pruebas quedaron listas.' : 'El chat de preselección quedó listo.'} Lo ve en su portal; también puede enviarle este enlace de un solo uso:
            <span className="aviso-avance-enlace"><span className="mono">{enlace}</span>
              <Boton pequeno onClick={() => { void navigator.clipboard?.writeText(enlace).then(() => setCopiado(true)) }}>{copiado ? 'Copiado' : 'Copiar'}</Boton></span>
          </p>
        )}
      </div>
      <button type="button" className="aviso-avance-cerrar" aria-label="Cerrar" onClick={onCerrar}><IconoCerrar /></button>
    </div>
  )
}

/* ---------- Detalle: la historia de la persona en el proceso ---------- */

function PanelPersona({ persona: p, onCerrar }: { persona: PersonaProceso; onCerrar: () => void }) {
  useEffect(() => {
    const tecla = (e: KeyboardEvent) => { if (e.key === 'Escape') onCerrar() }
    window.addEventListener('keydown', tecla)
    return () => window.removeEventListener('keydown', tecla)
  }, [onCerrar])
  const pasos = [{ a: 'Recibida' as Etapa, por: 'Postulación', motivo: null as string | null, en: p.recibidaEn }, ...p.historial.map((h) => ({ a: h.a, por: h.por, motivo: h.motivo, en: h.en }))]

  return (
    <div className="panel-velo" onClick={onCerrar}>
      <aside className="panel-persona" role="dialog" aria-modal="true" aria-label={`Proceso de ${p.nombre}`} onClick={(e) => e.stopPropagation()}>
        <header className={claseEtapa(p.etapa)}>
          <span className="persona-avatar grande" aria-hidden="true">{iniciales(p.nombre)}</span>
          <div>
            <h2>{p.nombre}</h2>
            <span className="persona-meta"><span className="mono">{p.numero}</span> · {p.ciudad} · <span className="persona-etapa"><span className="proceso-punto" />{nombreEtapa[p.etapa]}</span></span>
          </div>
          <button type="button" className="aviso-avance-cerrar" aria-label="Cerrar" onClick={onCerrar}><IconoCerrar /></button>
        </header>
        <div className="panel-persona-cuerpo">
          <div className="panel-persona-enlaces">
            <Link className="boton boton-secundario boton-pequeno" to={`/consola/candidatos/${p.candidatoId}`}>Ficha del candidato</Link>
            <AbrirHojaDeVida postulacionId={p.postulacionId} />
          </div>

          <section>
            <h3>Recorrido</h3>
            <ol className="linea-tiempo">
              {pasos.map((x, i) => (
                <li key={i} className={claseEtapa(x.a)} style={{ '--i': i } as CSSProperties}>
                  <span className="proceso-punto" />
                  <div><b>{nombreEtapa[x.a]}</b><span className="mono">{fecha.format(new Date(x.en))}</span><span>{x.por}{x.motivo ? ` · ${x.motivo}` : ''}</span></div>
                </li>
              ))}
            </ol>
          </section>

          {p.razonPrincipal && <section className="bloque-ia"><div className="bloque-ia-cabeza"><Chispa />Afinidad {p.puntaje ?? '—'}/100</div><div className="razon"><span className="signo mas">+</span><span className="texto">{p.razonPrincipal}</span><span className="cita">HV</span></div>
            <div className="protegidas"><IconoEscudo />No se usaron para el puntaje: edad, género, estado civil, foto ni dirección exacta.</div></section>}

          {p.preseleccion && (
            <section>
              <h3>Preselección</h3>
              {p.preseleccion.aspiracion !== null && <p className="ayuda">Aspira a <span className="mono">{pesos.format(p.preseleccion.aspiracion)}</span>{p.preseleccion.disponibilidad && ` · ${p.preseleccion.disponibilidad}`}</p>}
              <PreseleccionPostulante postulacionId={p.postulacionId} />
            </section>
          )}
          {p.pruebas && (
            <section>
              <h3>Pruebas</h3>
              <p className="ayuda">{p.pruebas.terminadas} de {p.pruebas.total} terminadas{p.pruebas.promedio !== null && <> · promedio <span className="mono">{p.pruebas.promedio}</span></>}
                {p.pruebas.cumpleCortes === false && ' · alguna bajo el corte'}. Los resultados completos están en la ficha del candidato.</p>
            </section>
          )}
        </div>
      </aside>
    </div>
  )
}

function EsqueletoProceso() {
  return (
    <div className="proceso">
      <Esqueleto alto={84} />
      <div className="tarjeta proceso-lista" style={{ padding: 16, display: 'grid', gap: 12 }}>{[0, 1, 2].map((i) => <Esqueleto key={i} alto={180} />)}</div>
    </div>
  )
}
