import { useState } from 'react'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { usePuede } from '../sesion/api'
import {
  fechaCorta, type MomentoInduccion, momentosInduccion, type Proceso, responsablesInduccion, type TareaInduccion, type TareaInduccionRequest,
  useAprobarPlanInduccion, useCerrarOnboarding, useEditarPlanInduccion, useMarcarTareaInduccion, usePedirPlanInduccion, useUsarPlanBase,
} from './api'

const fechaHora = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const diaTexto = (d: number) => (d === 0 ? 'día del ingreso' : d < 0 ? `${-d} ${d === -1 ? 'día' : 'días'} antes` : `día ${d}`)

/**
 * Onboarding: la IA propone el plan de inducción del cargo (violeta hasta que alguien lo confirma), RRHH lo ajusta y lo aprueba,
 * el equipo marca las tareas y al terminar se cierra el proceso. Sin IA hay un plan base por reglas.
 */
export function PlanInduccionPanel({ p }: { p: Proceso }) {
  const plan = p.induccion
  const aprueba = usePuede('AprobarBandeja')
  const pedir = usePedirPlanInduccion(p.id)
  const base = useUsarPlanBase(p.id)
  const aprobar = useAprobarPlanInduccion(p.id)
  const cerrar = useCerrarOnboarding(p.id)
  const [editando, setEditando] = useState<TareaInduccionRequest[] | null>(null)
  const error = pedir.error ?? base.error ?? aprobar.error ?? cerrar.error
  const ia = plan.estado === 'PorAprobar' && plan.origen === 'IA'

  const editar = () => setEditando(plan.tareas.map((t) => ({ id: t.id, titulo: t.titulo, detalle: t.detalle, responsable: t.responsable, momento: t.momento, dia: t.dia })))

  return (
    <section className={`tarjeta vin-panel vin-plan ${ia ? 'vin-ia' : ''}`}>
      <header>
        <div>
          <h2>Plan de inducción</h2>
          <p className="vin-plan-sub">
            Empleado creado por {p.alta.empleadoCreadoPor}{p.alta.empleadoCreadoEn ? ` el ${fechaHora.format(new Date(p.alta.empleadoCreadoEn))}` : ''} · ingresa el{' '}
            <span className="mono">{fechaCorta(p.condiciones.fechaIngreso)}</span>
          </p>
        </div>
        <EstadoPlan p={p} />
      </header>

      {(plan.estado === 'EnCurso' || plan.estado === 'Terminado') && (
        <div className="vin-plan-avance" aria-label={`Avance ${plan.avance} %`}>
          <span className="vin-plan-barra"><span style={{ width: `${plan.avance}%` }} /></span>
          <span className="mono">{plan.tareas.filter((t) => t.hecha).length}/{plan.tareas.length}</span>
        </div>
      )}

      {plan.estado === 'SinPlan' && (
        <div className="vin-plan-vacio">
          <p>Este proceso aún no tiene plan de inducción. Pida uno a la IA para el cargo «{p.cargo}» o empiece con el plan base.</p>
          <div className="vin-acciones">
            <Boton variante="ia" icono={<Chispa />} cargando={pedir.isPending} textoCargando="Pidiendo…" onClick={() => pedir.mutate()}>Pedir el plan a la IA</Boton>
            <Boton cargando={base.isPending} textoCargando="Preparando…" onClick={() => base.mutate()}>Usar el plan base</Boton>
          </div>
        </div>
      )}

      {plan.estado === 'Generando' && (
        <div className="vin-plan-generando" role="status">
          <div className="estado-agente"><span className="orbe trabajando" /><span>La IA prepara el plan de inducción para «{p.cargo}»…</span></div>
          {[56, 56, 56].map((a, i) => <Esqueleto key={i} alto={a} />)}
        </div>
      )}

      {plan.estado === 'FallaIa' && (
        <div className="vin-plan-vacio">
          <p className="aviso aviso-alerta"><IconoAlerta />{plan.errorIa ?? 'La IA no respondió.'}</p>
          <div className="vin-acciones">
            <Boton variante="ia" icono={<Chispa />} cargando={pedir.isPending} textoCargando="Pidiendo…" onClick={() => pedir.mutate()}>Pedir de nuevo a la IA</Boton>
            <Boton cargando={base.isPending} textoCargando="Preparando…" onClick={() => base.mutate()}>Usar el plan base</Boton>
          </div>
        </div>
      )}

      {ia && (plan.razones.length > 0 || plan.alertasIa.length > 0) && (
        <div className="bloque-ia">
          <div className="bloque-ia-cabeza"><Chispa />Por qué este plan<span className="derecha">{plan.modelo}</span></div>
          {plan.razones.map((r) => (
            <div key={r.texto} className="razon">
              <span className={`signo ${r.signo === '+' ? 'mas' : 'menos'}`}>{r.signo}</span>
              <span className="texto">{r.texto}</span>
              <span className="cita">{r.fuente}</span>
            </div>
          ))}
          {plan.alertasIa.map((a) => <div key={a} className="razon"><IconoAlerta className="vin-plan-alerta" /><span className="texto">{a}</span></div>)}
        </div>
      )}

      {editando ? (
        <EditorPlan p={p} tareas={editando} onCambiar={setEditando} onListo={() => setEditando(null)} />
      ) : (
        plan.tareas.length > 0 && <ListaTareas p={p} />
      )}

      {error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{error.message}</p>}

      {plan.estado === 'PorAprobar' && !editando && (
        <div className="decision">
          {aprueba && <Boton variante="primario" cargando={aprobar.isPending} textoCargando="Aprobando…" onClick={() => aprobar.mutate()}>Aprobar el plan</Boton>}
          <Boton onClick={editar}>Ajustar</Boton>
          {plan.origen === 'IA'
            ? <Boton variante="ia" icono={<Chispa />} cargando={pedir.isPending} textoCargando="Pidiendo…" onClick={() => pedir.mutate()}>Otra versión</Boton>
            : <Boton variante="ia" icono={<Chispa />} cargando={pedir.isPending} textoCargando="Pidiendo…" onClick={() => pedir.mutate()}>Pedir a la IA</Boton>}
          <span className="registro"><IconoEscudo />Se registran la sugerencia y tu decisión</span>
        </div>
      )}

      {plan.estado === 'EnCurso' && !editando && (
        <div className="decision">
          {aprueba && (
            <Boton variante="primario" disabled={plan.avance < 100} cargando={cerrar.isPending} textoCargando="Cerrando…" onClick={() => cerrar.mutate()}>
              Cerrar el onboarding
            </Boton>
          )}
          <Boton onClick={editar}>Ajustar el plan</Boton>
          <span className="registro">{plan.avance < 100 ? 'Se cierra cuando todas las tareas estén hechas' : 'El proceso quedará «Vinculado»'}</span>
        </div>
      )}

      {plan.estado === 'Terminado' && (
        <p className="aviso aviso-ok"><IconoCheck />Onboarding cerrado por {plan.cerradoPor}{plan.cerradoEn ? ` el ${fechaHora.format(new Date(plan.cerradoEn))}` : ''}. El proceso quedó vinculado.</p>
      )}
    </section>
  )
}

function EstadoPlan({ p }: { p: Proceso }) {
  const e = p.induccion.estado
  const [clase, texto] =
    e === 'Generando' ? ['pill-ia', 'La IA prepara'] : e === 'PorAprobar' ? ['pill-alerta', 'Por aprobar'] : e === 'EnCurso' ? ['pill-info', `En curso · ${p.induccion.avance} %`]
      : e === 'Terminado' ? ['pill-ok', 'Terminado'] : e === 'FallaIa' ? ['pill-error', 'La IA no respondió'] : ['pill-neutra', 'Sin plan']
  return <span className={`pill ${clase}`}>{texto}</span>
}

/** Tareas agrupadas por momento; en curso se marcan con un clic. */
function ListaTareas({ p }: { p: Proceso }) {
  const plan = p.induccion
  const marcar = useMarcarTareaInduccion(p.id)
  const [marcando, setMarcando] = useState<string>()
  const enCurso = plan.estado === 'EnCurso'
  const grupos = momentosInduccion.map(([m, nombre]) => [nombre, plan.tareas.filter((t) => t.momento === m)] as const).filter(([, ts]) => ts.length > 0)

  return (
    <div className="vin-plan-grupos">
      {marcar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{marcar.error.message}</p>}
      {grupos.map(([nombre, tareas]) => (
        <section key={nombre} className="vin-plan-grupo">
          <h3>{nombre} <span className="mono">{tareas.filter((t) => t.hecha).length}/{tareas.length}</span></h3>
          <ul>
            {tareas.map((t) => (
              <Tarea key={t.id} t={t} enCurso={enCurso} cargando={marcar.isPending && marcando === t.id}
                onMarcar={() => { setMarcando(t.id); marcar.mutate({ tareaId: t.id, hecha: !t.hecha }) }} />
            ))}
          </ul>
        </section>
      ))}
    </div>
  )
}

function Tarea({ t, enCurso, cargando, onMarcar }: { t: TareaInduccion; enCurso: boolean; cargando: boolean; onMarcar: () => void }) {
  return (
    <li className={`vin-tarea ${t.hecha ? 'hecha' : ''} ${t.deIa ? 'de-ia' : ''}`}>
      {enCurso ? (
        <button type="button" className="vin-tarea-marca" role="checkbox" aria-checked={t.hecha} aria-busy={cargando || undefined} disabled={cargando}
          aria-label={t.hecha ? `Reabrir «${t.titulo}»` : `Marcar hecha «${t.titulo}»`} onClick={onMarcar}>
          {cargando ? <span className="indicador" aria-hidden="true" /> : t.hecha ? <IconoCheck /> : null}
        </button>
      ) : (
        <span className="vin-tarea-punto" aria-hidden="true">{t.deIa ? <Chispa /> : null}</span>
      )}
      <div className="vin-tarea-texto">
        <b>{t.titulo}</b>
        {t.detalle && <span>{t.detalle}</span>}
        {t.hecha && t.hechaPor && <span className="vin-tarea-hecha">Hecha por {t.hechaPor}{t.hechaEn ? ` · ${fechaHora.format(new Date(t.hechaEn))}` : ''}</span>}
      </div>
      <span className="vin-tarea-quien">{t.responsable}</span>
      <span className="vin-tarea-cuando"><span className="mono">{fechaCorta(t.fecha)}</span><small>{diaTexto(t.dia)}</small></span>
    </li>
  )
}

function EditorPlan({ p, tareas, onCambiar, onListo }: {
  p: Proceso; tareas: TareaInduccionRequest[]; onCambiar: (t: TareaInduccionRequest[]) => void; onListo: () => void
}) {
  const guardar = useEditarPlanInduccion(p.id)
  const poner = (i: number, c: Partial<TareaInduccionRequest>) => onCambiar(tareas.map((t, j) => (j === i ? { ...t, ...c } : t)))
  const responsables = p.empresaUsuaria ? responsablesInduccion : responsablesInduccion.filter((r) => r !== 'Empresa usuaria')

  return (
    <div className="vin-plan-editor">
      {tareas.map((t, i) => (
        <div key={t.id ?? `nueva-${i}`} className="vin-plan-fila">
          <input className="entrada" aria-label="Tarea" placeholder="Qué hay que hacer" value={t.titulo} onChange={(e) => poner(i, { titulo: e.target.value })} />
          <select className="selector" aria-label="Responsable" value={t.responsable} onChange={(e) => poner(i, { responsable: e.target.value })}>
            {responsables.map((r) => <option key={r}>{r}</option>)}
          </select>
          <select className="selector" aria-label="Cuándo" value={t.momento} onChange={(e) => poner(i, { momento: e.target.value as MomentoInduccion })}>
            {momentosInduccion.map(([v, n]) => <option key={v} value={v}>{n}</option>)}
          </select>
          <label className="vin-plan-dia">Día<input className="entrada mono" type="number" min={-30} max={180} value={t.dia} onChange={(e) => poner(i, { dia: Number(e.target.value) || 0 })} /></label>
          <button type="button" className="enlace-boton" onClick={() => onCambiar(tareas.filter((_, j) => j !== i))}>Quitar</button>
          <input className="entrada vin-plan-detalle" aria-label="Detalle" placeholder="Detalle (opcional)" value={t.detalle} onChange={(e) => poner(i, { detalle: e.target.value })} />
        </div>
      ))}
      <button type="button" className="enlace-boton" onClick={() => onCambiar([...tareas, { id: null, titulo: '', detalle: '', responsable: 'Líder', momento: 'PrimeraSemana', dia: 2 }])}>
        + Agregar tarea
      </button>
      <span className="ayuda">El día cuenta desde el ingreso (<span className="mono">0</span> es el primer día; negativo, antes de ingresar). Ajustar lo que propuso la IA cuenta como confirmarlo.</span>
      {guardar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{guardar.error.message}</p>}
      <div className="vin-acciones">
        <Boton variante="primario" cargando={guardar.isPending} textoCargando="Guardando…" disabled={tareas.length === 0 || tareas.some((t) => !t.titulo.trim())}
          onClick={() => guardar.mutate(tareas, { onSuccess: onListo })}>Guardar el plan</Boton>
        <Boton onClick={onListo} disabled={guardar.isPending}>Cancelar</Boton>
      </div>
    </div>
  )
}
