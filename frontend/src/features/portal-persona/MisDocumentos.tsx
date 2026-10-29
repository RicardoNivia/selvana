import { type CSSProperties, type FormEvent, useState } from 'react'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoAlerta, IconoCheck, IconoDocumento, IconoEscudo, IconoFlecha, IconoSubir, IconoVacantes } from '../../shared/ui/Iconos'
import {
  type Autoevaluacion, type DatosPropuestos, type DocumentoPedido, type Empleo, type PulsoPendiente, type MiDocumento, type MisDocumentos, type Vigencia, tiposSoporte, urlDocumentoEmpleo,
  urlMiDocumento, useConfirmarMisDatos, useEnviarMiDocumento, useMisDocumentos, useProponerMisDatos, useQuitarMiDocumento, useSubirMiDocumento,
  useAutoevaluarme, useCompartirHojaDeVida, useHojaDeVidaCompartida, usePendientesEmpleo, useResponderPulso, useSubirSoporteEmpleo,
} from './api'

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' })
const pesos = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 })
const dia = (iso: string) => fecha.format(new Date(`${iso}T12:00:00`))

const vigencia: Record<Vigencia, { texto: string; clase: string }> = {
  Vigente: { texto: 'Vigente', clase: 'pill-ok' },
  PorVencer: { texto: 'Por vencer', clase: 'pill-alerta' },
  Vencido: { texto: 'Vencido', clase: 'pill-error' },
  SinVencimiento: { texto: 'Sin vencimiento', clase: 'pill-neutra' },
}

const estadoPedido: Record<DocumentoPedido['estado'], { texto: string; clase: string }> = {
  Pendiente: { texto: 'Te lo piden', clase: 'pill-alerta' },
  Rechazado: { texto: 'Hay que corregirlo', clase: 'pill-error' },
  Leyendo: { texto: 'En revisión', clase: 'pill-info' },
  PorRevisar: { texto: 'En revisión', clase: 'pill-info' },
  Aprobado: { texto: 'Aprobado', clase: 'pill-ok' },
}

/* ---------- P4 · Mis documentos ---------- */

export function MisDocumentosPanel() {
  const docs = useMisDocumentos()
  if (docs.isPending) return <div className="mp-docs">{[1, 2, 3].map((i) => <Esqueleto key={i} alto={96} />)}</div>
  if (docs.isError) return <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{docs.error.message}</p>
  const d = docs.data

  return (
    <div className="mp-docs">
      <p className="mp-bloque-ayuda">
        Súbelos una vez y entrégalos con un clic a las empresas que te los pidan. Se guardan cifrados y solo los recibe la empresa a la que tú los envías.
      </p>
      {d.pedidos.length > 0 && <Pedidos pedidos={d.pedidos} documentos={d.documentos} />}
      <section className="mp-docs-bloque">
        <h3>Tus documentos</h3>
        <ul className="mp-docs-lista">
          {d.tipos.map((t, i) => (
            <FilaDocumento key={t.clave} tipo={t} documento={d.documentos.find((x) => x.tipo === t.clave)} indice={i} />
          ))}
        </ul>
      </section>
      <p className="mp-nota"><IconoEscudo /> Tus documentos se tratan según la Ley 1581 de 2012. Puedes quitarlos cuando quieras.</p>
    </div>
  )
}

function Pedidos({ pedidos, documentos }: { pedidos: DocumentoPedido[]; documentos: MiDocumento[] }) {
  const enviar = useEnviarMiDocumento()
  const [enviando, setEnviando] = useState<string>()
  const porProceso = Object.values(pedidos.reduce<Record<string, DocumentoPedido[]>>((g, p) => ({ ...g, [p.procesoId]: [...(g[p.procesoId] ?? []), p] }), {}))

  return (
    <section className="mp-docs-bloque">
      <h3>Te los piden</h3>
      {enviar.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{enviar.error.message}</p>}
      {porProceso.map((lista) => (
        <article key={lista[0].procesoId} className="mp-docs-proceso">
          <header><IconoVacantes /><div><b>{lista[0].empresa}</b><span>{lista[0].cargo}</span></div></header>
          <ul>
            {lista.map((p) => {
              const clave = `${p.procesoId}-${p.tipo}`
              const tengo = documentos.some((x) => x.tipo === p.tipo)
              return (
                <li key={clave}>
                  <div>
                    <span>{p.nombre}{!p.obligatorio && <span className="mp-nota"> · opcional</span>}</span>
                    {p.motivoRechazo && p.estado === 'Rechazado' && <span className="mp-docs-motivo">{p.motivoRechazo}</span>}
                  </div>
                  <span className={`pill ${estadoPedido[p.estado].clase}`}>{estadoPedido[p.estado].texto}</span>
                  {p.puedeEnviar ? (
                    <Boton pequeno variante="primario" cargando={enviar.isPending && enviando === clave} textoCargando="Enviando…" disabled={enviar.isPending}
                      onClick={() => { setEnviando(clave); enviar.mutate({ procesoId: p.procesoId, tipo: p.tipo }) }}>
                      Enviar el mío <IconoFlecha />
                    </Boton>
                  ) : (p.estado === 'Pendiente' || p.estado === 'Rechazado') && (
                    <span className="mp-nota">{tengo ? 'El tuyo venció: sube uno nuevo abajo' : 'Súbelo abajo y luego envíalo'}</span>
                  )}
                </li>
              )
            })}
          </ul>
        </article>
      ))}
    </section>
  )
}

function FilaDocumento({ tipo, documento, indice }: { tipo: MisDocumentos['tipos'][number]; documento?: MiDocumento; indice: number }) {
  const subir = useSubirMiDocumento()
  const quitar = useQuitarMiDocumento()
  const [abierto, setAbierto] = useState(false)
  const [archivo, setArchivo] = useState<File>()
  const [vence, setVence] = useState('')

  const guardar = (e: FormEvent) => {
    e.preventDefault()
    if (!archivo) return
    subir.mutate({ tipo: tipo.clave, venceEn: vence || undefined, archivo }, { onSuccess: () => { setAbierto(false); setArchivo(undefined); setVence('') } })
  }

  return (
    <li className="mp-docs-fila" style={{ '--i': indice } as CSSProperties}>
      <span className={`mp-docs-icono${documento ? ' listo' : ''}`}>{documento ? <IconoCheck /> : <IconoDocumento />}</span>
      <div className="mp-docs-texto">
        <b>{tipo.nombre}</b>
        {documento ? (
          <span>
            <a href={urlMiDocumento(documento.id)} target="_blank" rel="noreferrer">{documento.nombreArchivo}</a>
            {' · '}subido el {fecha.format(new Date(documento.subidoEn))}
            {documento.venceEn && <> · vence el <span className="mono">{dia(documento.venceEn)}</span></>}
            {documento.envios.length > 0 && <> · enviado a {documento.envios.map((x) => x.empresa).filter((x, i, a) => a.indexOf(x) === i).join(', ')}</>}
          </span>
        ) : <span>Aún no lo has subido</span>}
      </div>
      {documento && <span className={`pill ${vigencia[documento.vigencia].clase}`}>{vigencia[documento.vigencia].texto}</span>}
      <div className="mp-docs-acciones">
        <Boton pequeno onClick={() => setAbierto((x) => !x)} aria-expanded={abierto}>{documento ? 'Reemplazar' : 'Subir'}</Boton>
        {documento && (
          <Boton pequeno className="mp-docs-quitar" cargando={quitar.isPending} textoCargando="Quitando…" onClick={() => quitar.mutate(documento.id)}>Quitar</Boton>
        )}
      </div>
      {abierto && (
        <form className="mp-docs-form" onSubmit={guardar}>
          <label className="postular-campo">Archivo (PDF o foto, hasta 10 MB)
            <input className="entrada" type="file" accept="application/pdf,image/jpeg,image/png,image/webp" required onChange={(e) => setArchivo(e.target.files?.[0])} />
          </label>
          {tipo.vence && (
            <label className="postular-campo">Vence el <span className="mp-nota">opcional</span>
              <input className="entrada mono" type="date" value={vence} onChange={(e) => setVence(e.target.value)} />
            </label>
          )}
          {subir.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{subir.error.message}</p>}
          <div className="mp-docs-form-acciones">
            <Boton onClick={() => setAbierto(false)} disabled={subir.isPending}>Cancelar</Boton>
            <Boton type="submit" variante="primario" icono={<IconoSubir />} cargando={subir.isPending} textoCargando="Subiendo…" disabled={!archivo}>Guardar</Boton>
          </div>
        </form>
      )}
    </li>
  )
}

/* ---------- P5 · Mi empleo ---------- */

export function MiEmpleoPanel({ empleos }: { empleos: Empleo[] }) {
  const pendientes = usePendientesEmpleo()
  const pulsos = pendientes.data?.pulsos.filter((p) => !p.respondido) ?? []
  const autoevaluaciones = pendientes.data?.autoevaluaciones ?? []
  return (
    <div className="mp-empleos">
      {pulsos.map((p) => <PulsoEmpleado key={p.pulsoId} pulso={p} />)}
      {autoevaluaciones.map((a) => <AutoevaluacionEmpleado key={a.evaluacionId} a={a} />)}
      {empleos.map((e) => <TarjetaEmpleo key={e.empleadoId} empleo={e} />)}
    </div>
  )
}

type Pulso = { satisfaccion?: number; claridad?: number; apoyo?: number; recomendaria?: number }

/** C3 · Pulso de ingreso: tres preguntas rápidas y la recomendación. Lo lee Recursos Humanos para acompañarte. */
function PulsoEmpleado({ pulso }: { pulso: PulsoPendiente }) {
  const responder = useResponderPulso()
  const [r, setR] = useState<Pulso>({})
  const [comentario, setComentario] = useState('')
  const preguntas: ['satisfaccion' | 'claridad' | 'apoyo', string][] = [
    ['satisfaccion', 'Me siento a gusto en mi trabajo'], ['claridad', 'Tengo claro lo que se espera de mí'], ['apoyo', 'Mi líder y mi equipo me apoyan'],
  ]
  const completo = preguntas.every(([k]) => r[k]) && r.recomendaria !== undefined
  return (
    <section className="mp-empleo">
      <header className="mp-empleo-cabeza">
        <div><span className="mp-ceja">Pulso de {pulso.hito} días</span><h2>¿Cómo te ha ido?</h2><p>Dos minutos. Recursos Humanos lo lee para acompañarte.</p></div>
      </header>
      {preguntas.map(([k, t]) => (
        <div key={k} className="mp-escala-fila"><span>{t}</span><Escala valor={r[k]} desde={1} hasta={5} onElegir={(v) => setR((x) => ({ ...x, [k]: v }))} etiqueta={t} /></div>
      ))}
      <div className="mp-escala-fila"><span>¿Recomendarías esta empresa a un amigo? (0 a 10)</span>
        <Escala valor={r.recomendaria} desde={0} hasta={10} onElegir={(v) => setR((x) => ({ ...x, recomendaria: v }))} etiqueta="Recomendación" /></div>
      <label className="postular-campo">Algo más que quieras contar <span className="mp-nota">opcional</span>
        <textarea className="entrada" rows={3} value={comentario} onChange={(e) => setComentario(e.target.value)} /></label>
      {responder.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{responder.error.message}</p>}
      <div>
        <Boton variante="primario" disabled={!completo} cargando={responder.isPending} textoCargando="Enviando…"
          onClick={() => responder.mutate({ pulsoId: pulso.pulsoId, satisfaccion: r.satisfaccion!, claridad: r.claridad!, apoyo: r.apoyo!, recomendaria: r.recomendaria!, comentario })}>
          Enviar
        </Boton>
      </div>
    </section>
  )
}

/** 180° y 360° · Autoevaluación: te calificas en cada competencia y cuentas cómo te fue. La calificación final la pone tu líder. */
function AutoevaluacionEmpleado({ a }: { a: Autoevaluacion }) {
  const autoevaluar = useAutoevaluarme()
  const [notas, setNotas] = useState<Record<string, number>>(() => Object.fromEntries(a.competencias.filter((c) => c.miCalificacion).map((c) => [c.nombre, c.miCalificacion!])))
  const [comentario, setComentario] = useState(a.comentario)
  const completo = a.competencias.every((c) => notas[c.nombre])
  return (
    <section className="mp-empleo">
      <header className="mp-empleo-cabeza">
        <div><span className="mp-ceja">{a.ciclo}</span><h2>Tu autoevaluación</h2><p>{a.cargo} · hasta el {dia(a.hasta)}. Tu líder ve lo que respondas; la calificación final la pone tu líder.</p></div>
        {a.respondida && <span className="pill pill-ok"><IconoCheck />Enviada</span>}
      </header>
      {a.objetivos.length > 0 && (
        <div className="mp-docs-bloque">
          <h3>Tus objetivos</h3>
          {a.objetivos.map((o) => <div key={o.descripcion} className="mp-escala-fila"><span>{o.descripcion}<span className="mp-nota"> · meta {o.meta}</span></span><span className="mono">{o.avance} %</span></div>)}
        </div>
      )}
      {a.competencias.map((c) => (
        <div key={c.nombre} className="mp-escala-fila"><span><b>{c.nombre}</b><span className="mp-nota"> · {c.descripcion}</span></span>
          <Escala valor={notas[c.nombre]} desde={1} hasta={5} onElegir={(v) => setNotas((x) => ({ ...x, [c.nombre]: v }))} etiqueta={c.nombre} /></div>
      ))}
      <label className="postular-campo">¿Cómo te fue en este periodo?
        <textarea className="entrada" rows={3} value={comentario} onChange={(e) => setComentario(e.target.value)} placeholder="Lo que lograste, lo que te costó y en qué quieres crecer." />
      </label>
      {autoevaluar.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{autoevaluar.error.message}</p>}
      <div>
        <Boton variante="primario" disabled={!completo} cargando={autoevaluar.isPending} textoCargando="Enviando…"
          onClick={() => autoevaluar.mutate({ evaluacionId: a.evaluacionId, calificaciones: notas, comentario })}>
          {a.respondida ? 'Actualizar mi autoevaluación' : 'Enviar mi autoevaluación'}
        </Boton>
      </div>
    </section>
  )
}

function Escala({ valor, desde, hasta, onElegir, etiqueta }: { valor?: number; desde: number; hasta: number; onElegir: (v: number) => void; etiqueta: string }) {
  return (
    <span className="mp-escala" role="group" aria-label={etiqueta}>
      {Array.from({ length: hasta - desde + 1 }, (_, i) => desde + i).map((n) => (
        <button key={n} type="button" aria-pressed={valor === n} onClick={() => onElegir(n)}>{n}</button>
      ))}
    </span>
  )
}

function TarjetaEmpleo({ empleo: e }: { empleo: Empleo }) {
  const [editando, setEditando] = useState(false)
  const [subiendo, setSubiendo] = useState(false)
  const confirmar = useConfirmarMisDatos()
  const activo = e.estado === 'Activo'

  return (
    <section className="mp-empleo">
      <header className="mp-empleo-cabeza">
        <div>
          <span className="mp-ceja">{e.empresa}{e.empresaUsuaria ? ` · en ${e.empresaUsuaria}` : ''}</span>
          <h2>{e.cargo}</h2>
          <p>{[e.area, e.sede].filter(Boolean).join(' · ')} · desde el {dia(e.fechaIngreso)}</p>
        </div>
        <span className={`pill ${activo ? 'pill-ok' : 'pill-neutra'}`}>{activo ? 'Activo' : 'Retirado'}</span>
      </header>

      {activo && e.actualizacionPedida && !e.cambioPendiente && (
        <div className="mp-empleo-campana" role="status">
          <IconoAlerta />
          <div><b>Tu empresa te pide confirmar tus datos</b><span>Revisa tu celular, tu ciudad y tus afiliaciones. Si algo cambió, actualízalo.</span></div>
          <div className="mp-empleo-campana-acciones">
            <Boton pequeno onClick={() => setEditando(true)}>Algo cambió</Boton>
            <Boton pequeno variante="primario" cargando={confirmar.isPending} textoCargando="Confirmando…" onClick={() => confirmar.mutate(e.empleadoId)}>Todo sigue igual</Boton>
          </div>
        </div>
      )}
      {confirmar.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{confirmar.error.message}</p>}
      {e.avisos.map((a) => <p key={a} className="aviso aviso-alerta"><IconoAlerta />{a}</p>)}

      <div className="mp-empleo-datos">
        <Dato etiqueta="Contrato" valor={e.tipoContrato} />
        <Dato etiqueta="Salario" valor={pesos.format(e.salarioCop)} mono />
        {e.fechaFinContrato && <Dato etiqueta="Fin del contrato" valor={dia(e.fechaFinContrato)} mono />}
        <Dato etiqueta="Código" valor={e.codigo} mono />
        <Dato etiqueta="Celular" valor={e.celular} />
        <Dato etiqueta="Ciudad" valor={e.ciudad} />
        <Dato etiqueta="EPS" valor={e.eps} />
        <Dato etiqueta="Pensión" valor={e.fondoPension} />
        <Dato etiqueta="ARL" valor={e.arl} />
        <Dato etiqueta="Caja de compensación" valor={e.cajaCompensacion} />
      </div>

      {e.cambioPendiente && (
        <div className="mp-empleo-pendiente">
          <b>Enviaste estos cambios el {fecha.format(new Date(e.cambioPendiente.en))}. Tu empresa los está revisando.</b>
          <ul>{e.cambioPendiente.cambios.map((c) => <li key={c}>{c}</li>)}</ul>
        </div>
      )}

      {activo && editando && <FormularioDatos empleo={e} onCerrar={() => setEditando(false)} />}
      {activo && !editando && !e.cambioPendiente && (
        <div className="mp-empleo-acciones">
          <Boton onClick={() => setEditando(true)}>Actualizar mis datos</Boton>
          <Boton onClick={() => setSubiendo((x) => !x)} icono={<IconoSubir />} aria-expanded={subiendo}>Subir un soporte</Boton>
        </div>
      )}
      {activo && subiendo && <FormularioSoporte empleadoId={e.empleadoId} onCerrar={() => setSubiendo(false)} />}

      <CompartirHojaDeVida empleo={e} />

      <div className="mp-docs-bloque">
        <h3>Tu carpeta en la empresa</h3>
        {e.documentos.length === 0 ? <p className="mp-nota">Aún no hay documentos en tu carpeta.</p> : (
          <ul className="mp-docs-lista">
            {e.documentos.map((d, i) => (
              <li key={d.id} className="mp-docs-fila" style={{ '--i': i } as CSSProperties}>
                <span className="mp-docs-icono listo"><IconoDocumento /></span>
                <div className="mp-docs-texto">
                  <b><a href={urlDocumentoEmpleo(e.empleadoId, d.id)} target="_blank" rel="noreferrer">{d.nombre}</a></b>
                  <span>{d.tipo}{d.venceEn && <> · vence el <span className="mono">{dia(d.venceEn)}</span></>}</span>
                </div>
                {d.enRevision ? <span className="pill pill-info">En revisión</span> : <span className={`pill ${vigencia[d.vigencia].clase}`}>{vigencia[d.vigencia].texto}</span>}
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}

function Dato({ etiqueta, valor, mono }: { etiqueta: string; valor: string; mono?: boolean }) {
  return <div className="mp-empleo-dato"><span>{etiqueta}</span><b className={mono ? 'mono' : undefined}>{valor || '—'}</b></div>
}

function FormularioDatos({ empleo: e, onCerrar }: { empleo: Empleo; onCerrar: () => void }) {
  const proponer = useProponerMisDatos()
  const [f, setF] = useState<DatosPropuestos>({ celular: e.celular, ciudad: e.ciudad, eps: e.eps, fondoPension: e.fondoPension, cajaCompensacion: e.cajaCompensacion })
  const poner = (c: Partial<DatosPropuestos>) => setF((x) => ({ ...x, ...c }))

  return (
    <form className="mp-docs-form" onSubmit={(ev) => { ev.preventDefault(); proponer.mutate({ ...f, empleadoId: e.empleadoId }, { onSuccess: onCerrar }) }}>
      <p className="mp-bloque-ayuda">Tu empresa revisa los cambios antes de aplicarlos. Para cambiar tu correo, tu nombre o tu identificación, habla con Recursos Humanos.</p>
      <div className="mp-empleo-form">
        <label className="postular-campo">Celular<input className="entrada mono" inputMode="tel" value={f.celular} onChange={(x) => poner({ celular: x.target.value })} /></label>
        <label className="postular-campo">Ciudad<input className="entrada" value={f.ciudad} onChange={(x) => poner({ ciudad: x.target.value })} /></label>
        <label className="postular-campo">EPS<input className="entrada" value={f.eps} onChange={(x) => poner({ eps: x.target.value })} /></label>
        <label className="postular-campo">Fondo de pensiones<input className="entrada" value={f.fondoPension} onChange={(x) => poner({ fondoPension: x.target.value })} /></label>
        <label className="postular-campo">Caja de compensación<input className="entrada" value={f.cajaCompensacion} onChange={(x) => poner({ cajaCompensacion: x.target.value })} /></label>
      </div>
      {proponer.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{proponer.error.message}</p>}
      <div className="mp-docs-form-acciones">
        <Boton onClick={onCerrar} disabled={proponer.isPending}>Cancelar</Boton>
        <Boton type="submit" variante="primario" cargando={proponer.isPending} textoCargando="Enviando…">Enviar a mi empresa</Boton>
      </div>
    </form>
  )
}

function FormularioSoporte({ empleadoId, onCerrar }: { empleadoId: string; onCerrar: () => void }) {
  const subir = useSubirSoporteEmpleo()
  const [tipo, setTipo] = useState(tiposSoporte[0])
  const [nombre, setNombre] = useState('')
  const [vence, setVence] = useState('')
  const [archivo, setArchivo] = useState<File>()

  return (
    <form className="mp-docs-form" onSubmit={(ev) => {
      ev.preventDefault()
      if (archivo) subir.mutate({ empleadoId, tipo, nombre, venceEn: vence || undefined, archivo }, { onSuccess: onCerrar })
    }}>
      <p className="mp-bloque-ayuda">Un diploma, un curso, una licencia o un certificado de afiliación. Lo revisa tu empresa y, si cambia algo, actualiza tu expediente.</p>
      <div className="mp-empleo-form">
        <label className="postular-campo">Qué es
          <select className="selector entrada" value={tipo} onChange={(x) => setTipo(x.target.value)}>{tiposSoporte.map((t) => <option key={t}>{t}</option>)}</select>
        </label>
        <label className="postular-campo">Nombre<input className="entrada" placeholder="Curso de alturas" value={nombre} onChange={(x) => setNombre(x.target.value)} required /></label>
        <label className="postular-campo">Vence el <span className="mp-nota">opcional</span><input className="entrada mono" type="date" value={vence} onChange={(x) => setVence(x.target.value)} /></label>
        <label className="postular-campo">Archivo (PDF o foto)
          <input className="entrada" type="file" accept="application/pdf,image/jpeg,image/png,image/webp" required onChange={(x) => setArchivo(x.target.files?.[0])} />
        </label>
      </div>
      {subir.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{subir.error.message}</p>}
      <div className="mp-docs-form-acciones">
        <Boton onClick={onCerrar} disabled={subir.isPending}>Cancelar</Boton>
        <Boton type="submit" variante="primario" icono={<IconoSubir />} cargando={subir.isPending} textoCargando="Subiendo…" disabled={!archivo}>Enviar</Boton>
      </div>
    </form>
  )
}

/** La empresa solo ve la hoja de vida del portal si la persona decide compartirla (su autorización del portal es con Selvana). */
function CompartirHojaDeVida({ empleo: e }: { empleo: Empleo }) {
  const compartidas = useHojaDeVidaCompartida()
  const compartir = useCompartirHojaDeVida()
  const compartida = compartidas.data?.includes(e.empresaId) ?? false
  return (
    <div className="mp-empleo-pendiente">
      <b>{compartida ? `${e.empresa} puede ver tu hoja de vida del portal` : `¿Compartes tu hoja de vida con ${e.empresa}?`}</b>
      <p className="mp-nota">
        {compartida
          ? 'La ven en tu expediente para tu plan de carrera y vacantes internas. Puedes dejar de compartirla cuando quieras.'
          : 'Así queda en tu expediente con tu experiencia y tus estudios, y te tienen en cuenta para vacantes internas. Si no la compartes, no la ven.'}
      </p>
      {compartir.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{compartir.error.message}</p>}
      <div style={{ marginTop: 8 }}>
        <Boton pequeno variante={compartida ? 'secundario' : 'primario'} disabled={compartidas.isPending} cargando={compartir.isPending}
          textoCargando={compartida ? 'Dejando de compartir…' : 'Compartiendo…'} onClick={() => compartir.mutate({ empresaId: e.empresaId, compartir: !compartida })}>
          {compartida ? 'Dejar de compartir' : 'Compartir mi hoja de vida'}
        </Boton>
      </div>
    </div>
  )
}
