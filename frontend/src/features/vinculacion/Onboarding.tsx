import { useState } from 'react'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { usePuede } from '../sesion/api'
import {
  estadosPlan, fechaCorta, type PlanInduccion, type Proceso, type TareaEditable, type TareaInduccion, useActualizarTarea, useAprobarPlan,
  useCerrarOnboarding, useEditarPlan, useRedactarPlan,
} from './api'

const fechaHora = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })

/** Tramos del plan, en el orden en que los vive el trabajador. */
const tramos: { titulo: string; hasta: number }[] = [
  { titulo: 'Antes del ingreso', hasta: -1 },
  { titulo: 'Primer día', hasta: 0 },
  { titulo: 'Primera semana', hasta: 7 },
  { titulo: 'Primer mes', hasta: 30 },
  { titulo: 'Hasta los 90 días', hasta: 90 },
  { titulo: 'Después', hasta: Number.MAX_SAFE_INTEGER },
]

function porTramo(tareas: TareaInduccion[]) {
  let desde = Number.MIN_SAFE_INTEGER
  return tramos.map((t) => {
    const grupo = { titulo: t.titulo, tareas: tareas.filter((x) => x.dia > desde && x.dia <= t.hasta) }
    desde = t.hasta
    return grupo
  }).filter((g) => g.tareas.length > 0)
}

const cuando = (dia: number) => (dia < 0 ? `${-dia} ${dia === -1 ? 'día' : 'días'} antes` : dia === 0 ? 'Día del ingreso' : `Día ${dia}`)

/** Onboarding: plan de inducción (tareas legales por reglas + las del cargo que propone la IA), aprobación y seguimiento hasta cerrar. */
export function Onboarding({ p }: { p: Proceso }) {
  const plan = p.induccion
  const redactar = useRedactarPlan(p.id)

  if (!plan) {
    return (
      <section className="tarjeta vin-panel" aria-label="Plan de inducción">
        <header><h2>Plan de inducción</h2></header>
        <p className="vin-sub">Este proceso llegó al onboarding antes del plan de inducción. Pida a la IA las tareas del cargo; las legales las pone Selvana por reglas.</p>
        {redactar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{redactar.error.message}</p>}
        <div className="vin-acciones">
          <Boton variante="ia" icono={<Chispa />} cargando={redactar.isPending} textoCargando="Pidiendo…" onClick={() => redactar.mutate()}>Pedir el plan a la IA</Boton>
        </div>
      </section>
    )
  }

  return (
    <div className="vin-alta">
      <Resumen p={p} plan={plan} />
      {plan.estado === 'Redactando' ? <Redactando plan={plan} /> : <Plan p={p} plan={plan} />}
    </div>
  )
}

function Resumen({ p, plan }: { p: Proceso; plan: PlanInduccion }) {
  const total = plan.tareas.length
  const avance = total ? Math.round((plan.hechas / total) * 100) : 0
  return (
    <section className="tarjeta vin-panel vin-plan-resumen" aria-label="Avance del onboarding">
      <div>
        <span className="vin-etiqueta">Ingreso</span>
        <b className="mono">{fechaCorta(p.condiciones.fechaIngreso)}</b>
        <span className="vin-sub">Empleado creado por {p.alta.empleadoCreadoPor}{p.alta.empleadoCreadoEn ? ` el ${fechaHora.format(new Date(p.alta.empleadoCreadoEn))}` : ''}</span>
      </div>
      <div>
        <span className="vin-etiqueta">Plan</span>
        <span className={`pill ${p.etapa === 'Vinculado' ? 'pill-ok' : estadosPlan[plan.estado].clase}`}>{p.etapa === 'Vinculado' ? 'Onboarding cerrado' : estadosPlan[plan.estado].texto}</span>
      </div>
      <div className="vin-plan-avance">
        <span className="vin-etiqueta">Avance</span>
        <b className="mono">{plan.hechas}/{total}</b>
        <span className="vin-barra" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={avance} aria-label="Tareas cerradas"><span style={{ width: `${avance}%` }} /></span>
      </div>
      <div>
        <span className="vin-etiqueta">Vencidas</span>
        <b className={`mono ${plan.vencidas > 0 ? 'vin-vencida' : ''}`}>{plan.vencidas}</b>
      </div>
    </section>
  )
}

function Redactando({ plan }: { plan: PlanInduccion }) {
  return (
    <section className="tarjeta vin-panel vin-ia" aria-label="Plan de inducción" aria-busy="true">
      <header><h2>Plan de inducción</h2><span className="pill pill-ia"><span className="orbe trabajando" /> La IA propone las tareas del cargo</span></header>
      <p className="vin-sub">Ya están las {plan.tareas.length} tareas legales. El planificador agrega las propias del cargo para los primeros 90 días; suele tardar menos de un minuto.</p>
      {Array.from({ length: 5 }, (_, i) => <Esqueleto key={i} alto={56} />)}
    </section>
  )
}

function Plan({ p, plan }: { p: Proceso; plan: PlanInduccion }) {
  const aprueba = usePuede('AprobarBandeja')
  const aprobar = useAprobarPlan(p.id)
  const redactar = useRedactarPlan(p.id)
  const cerrar = useCerrarOnboarding(p.id)
  const [editando, setEditando] = useState<TareaEditable[] | null>(null)
  const porAprobar = plan.estado === 'PorAprobar' || plan.estado === 'FallaIa'
  const seguimiento = plan.estado === 'Aprobado'
  const abierto = p.etapa === 'Onboarding'
  const deIa = plan.tareas.filter((t) => t.deIa).length
  const error = aprobar.error ?? redactar.error ?? cerrar.error

  if (editando) return <EditarPlan p={p} plan={plan} tareas={editando} onCambiar={setEditando} onTerminar={() => setEditando(null)} />

  return (
    <section className={`tarjeta vin-panel ${porAprobar && deIa > 0 ? 'vin-ia' : ''}`} aria-label="Plan de inducción">
      <header>
        <h2>Plan de inducción</h2>
        {abierto && <Boton pequeno onClick={() => setEditando(plan.tareas.map((t) => ({ id: t.id, titulo: t.titulo, descripcion: t.descripcion, responsable: t.responsable, asignado: t.asignado, dia: t.dia })))}>Ajustar</Boton>}
      </header>
      <p className="vin-sub">
        {plan.modelo ? <><Chispa width={13} height={13} /> {deIa} tareas propuestas por la IA ({plan.modelo}){plan.redactadoEn ? ` · ${fechaHora.format(new Date(plan.redactadoEn))}` : ''}</> : 'Tareas legales por reglas'}
        {plan.editadoPor && <> · ajustado por {plan.editadoPor}</>}
        {plan.aprobadoPor && <> · aprobado por {plan.aprobadoPor}{plan.aprobadoEn ? ` el ${fechaHora.format(new Date(plan.aprobadoEn))}` : ''}</>}
      </p>

      {plan.estado === 'FallaIa' && (
        <p className="aviso aviso-alerta"><IconoAlerta />La IA no respondió ({plan.errorIa}). El plan trae las tareas legales: agregue las del cargo con «Ajustar» o pida otra versión.</p>
      )}
      {porAprobar && plan.razones.length > 0 && (
        <div className="vin-razones">
          {plan.razones.map((r) => <p key={r.texto} className="razon"><span className={`signo ${r.signo === '+' ? 'mas' : 'menos'}`}>{r.signo}</span><span className="texto">{r.texto}</span><span className="cita">{r.fuente}</span></p>)}
        </div>
      )}
      {porAprobar && plan.alertasIa.length > 0 && (
        <ul className="vin-alertas">{plan.alertasIa.map((a) => <li key={a} className="vin-alerta revisar"><IconoAlerta /><span>{a}</span><span className="cita">Planificador</span></li>)}</ul>
      )}

      {porTramo(plan.tareas).map((g) => (
        <div key={g.titulo} className="vin-plan-tramo">
          <h3>{g.titulo}</h3>
          <ul className="vin-tareas">
            {g.tareas.map((t) => <Tarea key={t.id} p={p} t={t} porAprobar={porAprobar} seguimiento={seguimiento && abierto} />)}
          </ul>
        </div>
      ))}
      <p className="ficha-nota"><IconoEscudo />Las tareas legales salen de reglas (Decreto 1072 de 2015 y el Código Sustantivo del Trabajo) y no se pueden quitar. El trabajador ve su plan aprobado en el enlace de su oferta.</p>

      {error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{error.message}</p>}
      {porAprobar && (
        <div className="decision">
          {aprueba && <Boton variante="primario" cargando={aprobar.isPending} textoCargando="Aprobando…" onClick={() => aprobar.mutate()}>Aprobar el plan</Boton>}
          <Boton variante="ia" icono={<Chispa />} cargando={redactar.isPending} textoCargando="Pidiendo…" onClick={() => redactar.mutate()}>Otra versión</Boton>
          <span className="registro">Se registran la sugerencia y tu decisión</span>
        </div>
      )}
      {seguimiento && abierto && aprueba && (
        <div className="decision">
          <Boton variante="primario" disabled={!plan.completo} cargando={cerrar.isPending} textoCargando="Cerrando…" onClick={() => cerrar.mutate()}>Cerrar el onboarding</Boton>
          <span className="registro">{plan.completo ? 'La persona queda vinculada; queda en la bitácora con su nombre' : `Faltan ${plan.tareas.length - plan.hechas} tareas por cerrar`}</span>
        </div>
      )}
      {p.etapa === 'Vinculado' && (
        <p className="aviso aviso-ok"><IconoCheck />Onboarding cerrado por {plan.cerradoPor}{plan.cerradoEn ? ` el ${fechaHora.format(new Date(plan.cerradoEn))}` : ''}. El proceso de vinculación terminó.</p>
      )}
    </section>
  )
}

function Tarea({ p, t, porAprobar, seguimiento }: { p: Proceso; t: TareaInduccion; porAprobar: boolean; seguimiento: boolean }) {
  const actualizar = useActualizarTarea(p.id)
  const [nota, setNota] = useState<string | null>(null)
  const sugerida = t.deIa && porAprobar
  const ocupado = actualizar.isPending

  return (
    <li className={`vin-tarea ${sugerida ? 'vin-doc-ia' : ''} ${t.estado !== 'Pendiente' ? 'cerrada' : ''}`}>
      <div className="vin-tarea-cuando">
        <span className="mono">{fechaCorta(t.fecha)}</span>
        <span className="vin-sub">{cuando(t.dia)}</span>
      </div>
      <div className="vin-tarea-que">
        <b>{t.titulo}</b>
        {t.descripcion && <span className="vin-tarea-desc">{t.descripcion}</span>}
        <span className="vin-sub">
          {t.responsable}{t.asignado ? ` · ${t.asignado}` : ''} · <span className="cita">{t.fuente}</span>
        </span>
        {t.estado !== 'Pendiente' && <span className="vin-sub">{t.estado === 'Hecha' ? 'Hecha' : 'No aplica'} · {t.cerradaPor}{t.nota ? ` · «${t.nota}»` : ''}</span>}
        {nota !== null && (
          <form className="vin-rechazo" onSubmit={(e) => { e.preventDefault(); actualizar.mutate({ tareaId: t.id, estado: 'NoAplica', nota: nota.trim() }, { onSuccess: () => setNota(null) }) }}>
            <input className="entrada" value={nota} onChange={(e) => setNota(e.target.value)} placeholder="¿Por qué no aplica?" aria-label="Motivo" autoFocus />
            <Boton type="submit" pequeno disabled={!nota.trim()} cargando={ocupado} textoCargando="Guardando…">Guardar</Boton>
            <Boton pequeno onClick={() => setNota(null)}>Cancelar</Boton>
          </form>
        )}
        {actualizar.error && <span role="alert" className="vin-error">{actualizar.error.message}</span>}
      </div>
      <div className="vin-tarea-estado">
        {t.obligatoria && <span className="pill pill-neutra">Legal</span>}
        {sugerida && <span className="pill pill-ia"><Chispa width={11} height={11} /> IA</span>}
        {t.vencida && <span className="pill pill-error">Vencida</span>}
        {t.estado === 'Hecha' && <span className="pill pill-ok">Hecha</span>}
        {t.estado === 'NoAplica' && <span className="pill pill-neutra">No aplica</span>}
        {seguimiento && nota === null && (
          t.estado === 'Pendiente' ? (
            <>
              <Boton pequeno icono={<IconoCheck />} cargando={ocupado && actualizar.variables?.estado === 'Hecha'} textoCargando="Marcando…"
                onClick={() => actualizar.mutate({ tareaId: t.id, estado: 'Hecha', nota: null })}>Hecha</Boton>
              {!t.obligatoria && <button type="button" className="enlace-boton" onClick={() => setNota('')}>No aplica</button>}
            </>
          ) : (
            <Boton pequeno cargando={ocupado} textoCargando="Reabriendo…" onClick={() => actualizar.mutate({ tareaId: t.id, estado: 'Pendiente', nota: null })}>Reabrir</Boton>
          )
        )}
      </div>
    </li>
  )
}

function EditarPlan({ p, plan, tareas, onCambiar, onTerminar }:
  { p: Proceso; plan: PlanInduccion; tareas: TareaEditable[]; onCambiar: (t: TareaEditable[]) => void; onTerminar: () => void }) {
  const guardar = useEditarPlan(p.id)
  const legal = (id: string | null) => id !== null && plan.tareas.some((t) => t.id === id && t.obligatoria)
  const cambiar = (i: number, v: Partial<TareaEditable>) => onCambiar(tareas.map((t, j) => (j === i ? { ...t, ...v } : t)))
  const invalido = tareas.some((t) => !t.titulo.trim() || t.dia < -5 || t.dia > 180)

  return (
    <section className="tarjeta vin-panel" aria-label="Ajustar el plan de inducción">
      <header><h2>Ajustar el plan</h2><span className="vin-sub">Lo que cambie deja de ser sugerencia de la IA</span></header>
      <div className="vin-plan-editor">
        {tareas.map((t, i) => (
          <fieldset key={t.id ?? `nueva-${i}`} className="vin-plan-fila">
            <label className="vin-ancho">Tarea
              <input className="entrada" value={t.titulo} disabled={legal(t.id)} onChange={(e) => cambiar(i, { titulo: e.target.value })} />
            </label>
            <label className="vin-ancho">Detalle
              <textarea className="area-texto" rows={2} value={t.descripcion} onChange={(e) => cambiar(i, { descripcion: e.target.value })} />
            </label>
            <label>Responsable
              <select className="selector" value={t.responsable} onChange={(e) => cambiar(i, { responsable: e.target.value })}>
                {plan.responsables.map((r) => <option key={r} value={r}>{r}</option>)}
              </select>
            </label>
            <label>Persona <span className="ayuda">(opcional)</span>
              <input className="entrada" value={t.asignado ?? ''} onChange={(e) => cambiar(i, { asignado: e.target.value || null })} placeholder="Camilo Duarte" />
            </label>
            <label>Día desde el ingreso
              <input className="entrada mono" type="number" min={-5} max={180} value={t.dia} onChange={(e) => cambiar(i, { dia: Number(e.target.value) })} />
            </label>
            {legal(t.id)
              ? <span className="vin-sub vin-plan-quitar">Tarea legal: no se quita</span>
              : <button type="button" className="enlace-boton vin-plan-quitar" onClick={() => onCambiar(tareas.filter((_, j) => j !== i))}>Quitar</button>}
          </fieldset>
        ))}
      </div>
      <div className="vin-acciones">
        <Boton pequeno onClick={() => onCambiar([...tareas, { id: null, titulo: '', descripcion: '', responsable: plan.responsables[0], asignado: null, dia: 1 }])}>Agregar tarea</Boton>
      </div>
      {guardar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{guardar.error.message}</p>}
      <div className="decision">
        <Boton variante="primario" disabled={invalido || tareas.length === 0} cargando={guardar.isPending} textoCargando="Guardando…"
          onClick={() => guardar.mutate(tareas.map((t) => ({ ...t, titulo: t.titulo.trim(), descripcion: t.descripcion.trim() })), { onSuccess: onTerminar })}>Guardar el plan</Boton>
        <Boton onClick={onTerminar}>Cancelar</Boton>
        <span className="registro">{invalido ? 'Cada tarea necesita título y un día entre −5 y 180' : 'Queda en la bitácora con su nombre'}</span>
      </div>
    </section>
  )
}
