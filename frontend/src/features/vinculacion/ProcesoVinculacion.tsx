import { useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoCheck, IconoCompartir, IconoEscudo } from '../../shared/ui/Iconos'
import { usePuede } from '../sesion/api'
import {
  aRequest, type Clausula, type CondicionesRequest, enlaceCandidato, estadosOferta, fechaCorta, pesos, type Proceso, useAprobarOferta,
  useCambiarCondiciones, useCancelarProceso, useEditarOferta, useProceso, useRedactarOferta, useRenovarEnlace, useRevisarCondiciones,
} from './api'
import { AlertasContrato, CondicionesFormulario } from './CondicionesFormulario'
import { AfiliacionesYAlta } from './AfiliacionesYAlta'
import { DocumentosIngreso } from './DocumentosIngreso'
import { PlanInduccionPanel } from './PlanInduccion'
import './vinculacion.css'

const pasos = ['Oferta', 'Documentos', 'Afiliaciones', 'Onboarding'] as const
type Paso = (typeof pasos)[number]
const nombrePaso: Record<Paso, string> = { Oferta: 'Oferta y contrato', Documentos: 'Documentos', Afiliaciones: 'Afiliaciones y alta', Onboarding: 'Onboarding' }
const fechaHora = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })

/** Paso en el que va el proceso (un proceso vinculado ya pasó el onboarding). */
function pasoActual(p: Proceso): number {
  if (p.etapa === 'Vinculado') return pasos.length
  const i = pasos.indexOf(p.etapa as Paso)
  return i < 0 ? 0 : i
}

/** Pestañas por paso: las ya alcanzadas se abren; las que vienen se ven, pero desactivadas. */
function Pestanas({ p, paso, onElegir }: { p: Proceso; paso: Paso; onElegir: (x: Paso) => void }) {
  const actual = pasoActual(p)
  const pendientesDocs = p.documentos.filter((d) => d.estado === 'PorRevisar').length
  return (
    <div role="tablist" aria-label="Pasos de la vinculación" className="vin-pasos">
      {pasos.map((x, i) => {
        const alcanzado = p.etapa !== 'Cancelado' ? i <= actual : i === 0
        return (
          <button key={x} type="button" role="tab" aria-selected={paso === x} disabled={!alcanzado}
            className={`vin-paso ${p.etapa !== 'Cancelado' && i < actual ? 'hecho' : ''} ${i === actual && p.etapa !== 'Cancelado' ? 'actual' : ''}`}
            onClick={() => onElegir(x)} title={alcanzado ? nombrePaso[x] : 'Aún no llega a este paso'}>
            <span className="vin-paso-num mono">{p.etapa !== 'Cancelado' && i < actual ? <IconoCheck /> : i + 1}</span>
            {nombrePaso[x]}
            {x === 'Documentos' && pendientesDocs > 0 && <span className="nav-contador">{pendientesDocs}</span>}
            {x === 'Onboarding' && p.induccion?.estado === 'PorAprobar' && <span className="nav-contador">!</span>}
          </button>
        )
      })}
    </div>
  )
}

/** Detalle de un proceso de vinculación: condiciones, oferta redactada por la IA, aprobación y respuesta del candidato. */
export function ProcesoVinculacion() {
  const { id = '' } = useParams()
  const proceso = useProceso(id)
  const [parametros, setParametros] = useSearchParams()

  if (proceso.isPending) {
    return <><header className="encabezado"><Esqueleto alto={32} ancho="40%" /><Esqueleto alto={14} ancho="60%" /></header><div className="vin-grilla"><Esqueleto alto={320} /><Esqueleto alto={420} /></div></>
  }
  if (proceso.isError) return <p role="alert" className="aviso aviso-error"><IconoAlerta />{proceso.error.message}</p>

  const p = proceso.data
  // La pestaña abre en el paso actual, salvo que el enlace pida otra (?paso=Documentos, por ejemplo desde la bandeja)
  const pedido = parametros.get('paso') as Paso | null
  const paso: Paso = pedido && pasos.includes(pedido) && pasos.indexOf(pedido) <= pasoActual(p) ? pedido : pasos[Math.min(pasoActual(p), pasos.length - 1)]
  const elegir = (x: Paso) => setParametros((q) => { q.set('paso', x); return q }, { replace: true })
  return (
    <>
      <header className="encabezado">
        <p className="vin-migas"><Link to="/consola/vinculacion">Vinculación</Link> · <span className="mono">{p.codigo}</span></p>
        <h1>{p.persona.nombre}</h1>
        <p>{p.cargo} · {p.empresaUsuaria ?? p.empresa} · <span className="mono">CC {p.persona.documento}</span> · viene de <Link to={`/consola/vacantes/${p.vacanteId}?pestana=tablero`}>{p.codigoVacante}</Link>
          {' '}· contratación aprobada por {p.aprobadaPor}</p>
      </header>
      <Pestanas p={p} paso={paso} onElegir={elegir} />
      {p.etapa === 'Cancelado' && <p className="aviso aviso-alerta"><IconoAlerta />Proceso cancelado: «{p.motivoCancelacion}».</p>}
      <div role="tabpanel" aria-label={nombrePaso[paso]}>
        {paso === 'Oferta' && (
          <div className="vin-grilla">
            <Condiciones p={p} />
            <Oferta p={p} />
          </div>
        )}
        {paso === 'Documentos' && <DocumentosIngreso p={p} />}
        {paso === 'Afiliaciones' && <AfiliacionesYAlta p={p} />}
        {paso === 'Onboarding' && <PlanInduccionPanel p={p} />}
      </div>
    </>
  )
}

function Condiciones({ p }: { p: Proceso }) {
  const aprueba = usePuede('AprobarBandeja')
  const [editando, setEditando] = useState<CondicionesRequest | null>(null)
  const revision = useRevisarCondiciones(p.postulacionId, editando)
  const cambiar = useCambiarCondiciones(p.id)
  const cancelar = useCancelarProceso(p.id)
  const [motivo, setMotivo] = useState<string | null>(null)
  const c = p.condiciones
  const editable = aprueba && p.etapa === 'Oferta' && p.oferta.estado !== 'Aceptada'
  const alertas = editando ? (revision.data ?? p.alertas) : p.alertas

  return (
    <section className="tarjeta vin-panel" aria-label="Condiciones de la contratación">
      <header><h2>Condiciones</h2>{editable && !editando && <Boton pequeno onClick={() => setEditando(aRequest(c))}>Cambiar</Boton>}</header>
      {editando ? (
        <>
          <CondicionesFormulario valor={editando} onCambiar={setEditando} empresaUsuaria={p.empresaUsuaria} />
          <p className="ayuda">Al guardar, la IA vuelve a redactar la oferta y el enlace anterior deja de servir.</p>
          <div className="vin-acciones">
            <Boton variante="primario" cargando={cambiar.isPending} textoCargando="Guardando…" disabled={alertas.some((a) => a.nivel === 'Bloquea')}
              onClick={() => cambiar.mutate(editando, { onSuccess: () => setEditando(null) })}>Guardar y redactar de nuevo</Boton>
            <Boton onClick={() => setEditando(null)}>Cancelar</Boton>
          </div>
          {cambiar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{cambiar.error.message}</p>}
        </>
      ) : (
        <dl className="vin-datos">
          <dt>Contrato</dt><dd>{c.tipoNombre}</dd>
          <dt>Salario</dt><dd className="mono">{pesos.format(c.salarioCop)}{c.auxilioTransporte && <span className="vin-sub">+ auxilio de transporte {pesos.format(c.auxilioTransporteCop)}</span>}</dd>
          <dt>Ingreso</dt><dd className="mono">{fechaCorta(c.fechaIngreso)}</dd>
          {c.fechaFin && <><dt>Terminación</dt><dd className="mono">{fechaCorta(c.fechaFin)}</dd></>}
          <dt>Periodo de prueba</dt><dd>{c.periodoPruebaDias ? `${c.periodoPruebaDias} días` : 'No tiene'}</dd>
          <dt>Jornada</dt><dd>{c.jornada}</dd>
          <dt>Lugar</dt><dd>{c.ciudad}</dd>
          {c.labor && <><dt>Labor</dt><dd>{c.labor}</dd></>}
          <dt>Rango de la vacante</dt><dd className="mono">{pesos.format(p.salarioMinimoVacante)} – {pesos.format(p.salarioMaximoVacante)}</dd>
        </dl>
      )}
      <h3>Revisión por reglas</h3>
      <AlertasContrato alertas={alertas} cargando={revision.isFetching} />

      <h3>Persona</h3>
      <dl className="vin-datos">
        <dt>Celular</dt><dd className="mono">{p.persona.celular || '—'}</dd>
        <dt>Correo</dt><dd>{p.persona.correo || '—'}</dd>
        <dt>Ciudad</dt><dd>{p.persona.ciudad || '—'}</dd>
      </dl>
      <Link to={`/consola/candidatos/${p.candidatoId}`}>Ver la ficha del candidato</Link>

      {aprueba && p.etapa !== 'Cancelado' && p.etapa !== 'Vinculado' && (
        motivo === null
          ? <button type="button" className="enlace-boton vin-cancelar" onClick={() => setMotivo('')}>Cancelar el proceso</button>
          : (
            <form className="vin-cancelacion" onSubmit={(e) => { e.preventDefault(); cancelar.mutate(motivo.trim()) }}>
              <label>¿Por qué se cancela?<input className="entrada" value={motivo} onChange={(e) => setMotivo(e.target.value)} placeholder="El candidato aceptó otra oferta" autoFocus /></label>
              <div className="vin-acciones">
                <Boton type="submit" disabled={!motivo.trim()} cargando={cancelar.isPending} textoCargando="Cancelando…">Cancelar el proceso</Boton>
                <Boton onClick={() => setMotivo(null)}>Volver</Boton>
              </div>
              <span className="ayuda">La postulación vuelve a la terna y queda en la bitácora.</span>
              {cancelar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{cancelar.error.message}</p>}
            </form>
          )
      )}
    </section>
  )
}

function Oferta({ p }: { p: Proceso }) {
  const o = p.oferta
  const aprueba = usePuede('AprobarBandeja')
  const redactar = useRedactarOferta(p.id)
  const aprobar = useAprobarOferta(p.id)
  const renovar = useRenovarEnlace(p.id)
  const [editando, setEditando] = useState<{ carta: string; clausulas: Clausula[] } | null>(null)
  const editar = useEditarOferta(p.id)
  const [token, setToken] = useState<string | null>(null)
  const [copiado, setCopiado] = useState(false)
  const deIa = !o.editadaPor
  const bloquea = p.alertas.some((a) => a.nivel === 'Bloquea')
  const enlace = token ? enlaceCandidato(token) : null

  if (o.estado === 'Redactando') {
    return (
      <section className="tarjeta vin-panel vin-ia" aria-label="Oferta" aria-busy="true">
        <header><h2>Oferta y contrato</h2><span className="pill pill-ia"><span className="orbe trabajando" /> La IA está redactando</span></header>
        <p className="vin-sub">El redactor de ofertas escribe la carta y el contrato con las condiciones aprobadas. Suele tardar menos de un minuto.</p>
        {Array.from({ length: 6 }, (_, i) => <Esqueleto key={i} alto={i === 0 ? 90 : 48} />)}
      </section>
    )
  }

  if (o.estado === 'FallaIa') {
    return (
      <section className="tarjeta vin-panel" aria-label="Oferta">
        <header><h2>Oferta y contrato</h2><span className="pill pill-error">La IA no respondió</span></header>
        <p className="aviso aviso-error"><IconoAlerta />{o.errorIa}</p>
        <Boton variante="ia" icono={<Chispa />} cargando={redactar.isPending} textoCargando="Pidiendo…" onClick={() => redactar.mutate()}>Pedir de nuevo a la IA</Boton>
      </section>
    )
  }

  return (
    <section className={`tarjeta vin-panel ${deIa && o.estado === 'PorAprobar' ? 'vin-ia' : ''}`} aria-label="Oferta">
      <header>
        <h2>Oferta y contrato</h2>
        <span className={`pill ${estadosOferta[o.estado].clase}`}>{estadosOferta[o.estado].texto}</span>
      </header>
      <p className="vin-sub">
        {deIa ? <><Chispa width={13} height={13} /> Redactado por la IA{o.modelo ? ` (${o.modelo})` : ''}{o.redactadaEn ? ` · ${fechaHora.format(new Date(o.redactadaEn))}` : ''}</> : <>Editado por {o.editadaPor}</>}
        {o.aprobadaPor && <> · aprobado por {o.aprobadaPor}{o.aprobadaEn ? ` el ${fechaHora.format(new Date(o.aprobadaEn))}` : ''}</>}
      </p>

      {o.razones.length > 0 && (
        <div className="vin-razones">
          {o.razones.map((r) => <p key={r.texto} className="razon"><span className={`signo ${r.signo === '+' ? 'mas' : 'menos'}`}>{r.signo}</span><span className="texto">{r.texto}</span><span className="cita">{r.fuente}</span></p>)}
        </div>
      )}
      {o.alertasIa.length > 0 && o.estado === 'PorAprobar' && (
        <ul className="vin-alertas">{o.alertasIa.map((a) => <li key={a} className="vin-alerta revisar"><IconoAlerta /><span>{a}</span><span className="cita">Redactor</span></li>)}</ul>
      )}

      {editando ? (
        <div className="formulario">
          <label>Carta oferta<textarea className="area-texto" rows={8} value={editando.carta} onChange={(e) => setEditando({ ...editando, carta: e.target.value })} /></label>
          {editando.clausulas.map((c, i) => (
            <label key={i}>
              <input className="entrada" value={c.titulo} aria-label={`Título de la cláusula ${i + 1}`}
                onChange={(e) => setEditando({ ...editando, clausulas: editando.clausulas.map((x, j) => (j === i ? { ...x, titulo: e.target.value } : x)) })} />
              <textarea className="area-texto" rows={3} value={c.texto} aria-label={`Texto de la cláusula ${i + 1}`}
                onChange={(e) => setEditando({ ...editando, clausulas: editando.clausulas.map((x, j) => (j === i ? { ...x, texto: e.target.value } : x)) })} />
            </label>
          ))}
          <div className="vin-acciones">
            <Boton variante="primario" cargando={editar.isPending} textoCargando="Guardando…" onClick={() => editar.mutate(editando, { onSuccess: () => setEditando(null) })}>Guardar cambios</Boton>
            <Boton onClick={() => setEditando(null)}>Cancelar</Boton>
          </div>
          {editar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{editar.error.message}</p>}
        </div>
      ) : (
        <article className="vin-documento">
          <div className="vin-carta">{o.carta}</div>
          <h3>Contrato</h3>
          {o.clausulas.map((c) => <div key={c.titulo} className="vin-clausula"><b>{c.titulo}</b><p>{c.texto}</p></div>)}
        </article>
      )}

      {o.estado === 'PorAprobar' && !editando && (
        <>
          {(aprobar.error ?? redactar.error) && <p role="alert" className="aviso aviso-error"><IconoAlerta />{(aprobar.error ?? redactar.error)!.message}</p>}
          <div className="decision">
            {aprueba && (
              <Boton variante="primario" disabled={bloquea} cargando={aprobar.isPending} textoCargando="Aprobando…"
                onClick={() => aprobar.mutate(undefined, { onSuccess: (r) => setToken(r.token) })}>Aprobar y generar enlace</Boton>
            )}
            <Boton onClick={() => setEditando({ carta: o.carta, clausulas: o.clausulas })}>Ajustar el texto</Boton>
            <Boton variante="ia" icono={<Chispa />} cargando={redactar.isPending} textoCargando="Pidiendo…" onClick={() => redactar.mutate()}>Otra versión</Boton>
            <span className="registro">{bloquea ? 'Corrija las alertas en rojo de las condiciones' : 'Se registran la sugerencia y tu decisión'}</span>
          </div>
        </>
      )}

      {o.estado === 'Enviada' && (
        <div className="vin-enlace">
          {enlace ? (
            <>
              <p><b>Enlace del candidato.</b> Compártalo por WhatsApp o correo; solo se muestra una vez.</p>
              <div className="vin-enlace-fila">
                <input className="entrada mono" readOnly value={enlace} onFocus={(e) => e.target.select()} aria-label="Enlace de la oferta" />
                <Boton icono={copiado ? <IconoCheck /> : <IconoCompartir />} onClick={() => { void navigator.clipboard?.writeText(enlace); setCopiado(true) }}>{copiado ? 'Copiado' : 'Copiar'}</Boton>
              </div>
            </>
          ) : (
            <>
              <p>Esperando la respuesta del candidato{o.enviadaEn ? ` desde el ${fechaHora.format(new Date(o.enviadaEn))}` : ''}. Por seguridad el enlace no se guarda en claro; si el candidato lo perdió, genere uno nuevo.</p>
              <Boton icono={<IconoCompartir />} cargando={renovar.isPending} textoCargando="Generando…" onClick={() => renovar.mutate(undefined, { onSuccess: (r) => setToken(r.token) })}>Generar enlace nuevo</Boton>
            </>
          )}
        </div>
      )}

      {o.respuesta && (
        <div className={`vin-respuesta ${o.respuesta.acepta ? 'acepta' : 'rechaza'}`}>
          <b>{o.respuesta.acepta ? <><IconoCheck /> Aceptó la oferta</> : <><IconoAlerta /> No aceptó la oferta</>}</b>
          {o.respuesta.motivo && <p>Motivo: «{o.respuesta.motivo}»</p>}
          <dl className="vin-datos">
            {o.respuesta.acepta && <><dt>Firmó como</dt><dd>{o.respuesta.nombreEscrito}</dd></>}
            <dt>Fecha</dt><dd className="mono">{fechaHora.format(new Date(o.respuesta.en))}</dd>
            <dt>IP</dt><dd className="mono">{o.respuesta.ip || '—'}</dd>
            <dt>Navegador</dt><dd className="vin-navegador">{o.respuesta.navegador || '—'}</dd>
            <dt>Huella SHA-256</dt><dd className="mono vin-huella">{o.respuesta.huella}</dd>
          </dl>
          <p className={`vin-integridad ${o.respuesta.integra ? '' : 'mal'}`}><IconoEscudo />
            {o.respuesta.integra ? 'La evidencia coincide con el texto que vio el candidato.' : 'La evidencia NO coincide con el texto actual: alguien cambió la oferta después de la respuesta.'}</p>
          {!o.respuesta.acepta && <p className="ayuda">Puede cambiar las condiciones y enviar otra oferta, o cancelar el proceso para que la postulación vuelva a la terna.</p>}
        </div>
      )}
    </section>
  )
}
