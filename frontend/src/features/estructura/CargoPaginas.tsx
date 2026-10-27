import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto, EsqueletoPanel } from '../../shared/ui/Esqueleto'
import { Chispa, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { PruebasDelCargo } from '../pruebas/PruebasDelCargo'
import { type Cargo, type Perfil, useAprobarPerfil, useCargo, useCrearCargo, useEditarCargo, useResumenEstructura, useSolicitarPerfil } from './api'

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const numero = new Intl.NumberFormat('es-CO')
const clasesArl = ['Clase I', 'Clase II', 'Clase III', 'Clase IV', 'Clase V', 'Por definir']
const lineas = (t: string) => t.split('\n').map((l) => l.trim()).filter(Boolean)
const rutaLista = '/consola/empresa/estructura'
const rutaCargo = (id: string) => `${rutaLista}/cargos/${id}`

export function estadoDe(c: Cargo) {
  if (!c.activo) return { texto: 'Inactivo', clase: 'pill-neutra' }
  if (c.estadoPerfil === 'Aprobado') return { texto: 'Aprobado', clase: 'pill-ok' }
  if (c.estadoPerfil === 'BorradorIa') return { texto: 'Borrador IA', clase: 'pill-ia' }
  return c.redactandoPerfil ? { texto: 'Redactando…', clase: 'pill-ia' } : { texto: 'Sin perfil', clase: 'pill-alerta' }
}

function Migas({ actual }: { actual: string }) {
  return <p style={{ margin: '0 0 8px', fontSize: 13 }}><Link to={rutaLista}>Estructura y cargos</Link> · {actual}</p>
}

/** Campos del perfil del cargo: los mismos al crear a mano y al editar. */
function CamposPerfil({ mision, setMision, funciones, setFunciones, requisitos, setRequisitos, riesgo, setRiesgo }: {
  mision: string; setMision: (v: string) => void
  funciones: string; setFunciones: (v: string) => void
  requisitos: string; setRequisitos: (v: string) => void
  riesgo: string; setRiesgo: (v: string) => void
}) {
  return (
    <>
      <label>Misión<textarea className="area-texto" rows={2} value={mision} onChange={(e) => setMision(e.target.value)} placeholder="Para qué existe el cargo, en una frase" /></label>
      <div className="dos-campos">
        <label>Funciones <span className="ayuda">Una por línea</span><textarea className="area-texto" rows={6} value={funciones} onChange={(e) => setFunciones(e.target.value)} /></label>
        <label>Requisitos <span className="ayuda">Uno por línea</span><textarea className="area-texto" rows={6} value={requisitos} onChange={(e) => setRequisitos(e.target.value)} /></label>
      </div>
      <label style={{ maxWidth: 220 }}>
        Riesgo ARL
        <select className="selector" value={riesgo} onChange={(e) => setRiesgo(e.target.value)}>{clasesArl.map((c) => <option key={c}>{c}</option>)}</select>
      </label>
    </>
  )
}

/* ---------- Nuevo cargo ---------- */

/**
 * Crear un cargo: nombre y área, y luego una de dos rutas. La IA redacta el perfil a partir de lo que RRHH le cuente
 * (llega como borrador para aprobar), o la persona lo escribe en los campos y queda aprobado por ella.
 */
export function CargoNuevo() {
  const navegar = useNavigate()
  const resumen = useResumenEstructura()
  const crear = useCrearCargo()
  const [nombre, setNombre] = useState('')
  const [area, setArea] = useState('')
  const [descripcion, setDescripcion] = useState('')
  const [mision, setMision] = useState('')
  const [funciones, setFunciones] = useState('')
  const [requisitos, setRequisitos] = useState('')
  const [riesgo, setRiesgo] = useState('Por definir')
  const [ruta, setRuta] = useState<'ia' | 'manual'>()
  const [falta, setFalta] = useState<string>()
  const areas = resumen.data?.nombresAreas ?? []
  const sinAreas = resumen.isSuccess && areas.length === 0

  const enviar = (conIa: boolean) => {
    if (!nombre.trim() || !area) return setFalta('Escriba el nombre del cargo y elija su área.')
    setFalta(undefined)
    setRuta(conIa ? 'ia' : 'manual')
    const datos = conIa
      ? { nombre, area, descripcion: descripcion.trim() || undefined }
      : { nombre, area, perfil: { mision, funciones: lineas(funciones), requisitos: lineas(requisitos), riesgoArl: riesgo } satisfies Perfil }
    crear.mutate(datos, { onSuccess: (r) => navegar(rutaCargo(r.id)) })
  }

  return (
    <>
      <header className="encabezado">
        <Migas actual="Nuevo cargo" />
        <h1>Nuevo cargo</h1>
        <p>Pídale el perfil a la IA contándole del cargo, o escríbalo usted en el formulario.</p>
      </header>

      <form className="tarjeta formulario" style={{ padding: 24, display: 'flex', flexDirection: 'column', gap: 20, maxWidth: 880 }}
        onSubmit={(e) => { e.preventDefault(); enviar(false) }}>
        <div className="dos-campos">
          <label>
            Nombre del cargo
            <input className="entrada" value={nombre} onChange={(e) => setNombre(e.target.value)} placeholder="Ej.: Analista de datos" maxLength={200} />
          </label>
          <label>
            Área
            <select className="selector" value={area} onChange={(e) => setArea(e.target.value)} disabled={sinAreas}>
              <option value="">{resumen.isPending ? 'Cargando áreas…' : sinAreas ? 'No hay áreas creadas' : 'Elija el área'}</option>
              {areas.map((a) => <option key={a}>{a}</option>)}
            </select>
          </label>
        </div>
        {sinAreas && (
          <div className="aviso aviso-alerta" style={{ alignItems: 'center' }}>
            <span style={{ flex: 1 }}>Cada cargo se asocia a un área. Cree primero el área y vuelva aquí.</span>
            <Link to={`${rutaLista}?pestana=areas`} className="boton boton-secundario boton-pequeno">Crear un área</Link>
          </div>
        )}

        <div className="bloque-ia">
          <div className="bloque-ia-cabeza"><Chispa />Generar el perfil con IA</div>
          <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 12 }}>
            <label>
              Cuéntele a la IA sobre el cargo <span className="ayuda">Opcional</span>
              <textarea className="area-texto" rows={4} value={descripcion} onChange={(e) => setDescripcion(e.target.value)} maxLength={2000}
                placeholder="Ej.: Arma tableros de ventas en Power BI para la gerencia comercial, cruza datos del ERP y del CRM. Profesional en estadística o ingeniería con 2 años de experiencia." />
            </label>
            <span className="ayuda">La IA redacta misión, funciones, requisitos y riesgo ARL. Usted revisa el borrador y lo aprueba.</span>
            <div>
              <Boton variante="ia" icono={<Chispa />} cargando={crear.isPending && ruta === 'ia'} textoCargando="Creando…"
                disabled={crear.isPending} onClick={() => enviar(true)}>
                Generar con IA
              </Boton>
            </div>
          </div>
        </div>

        <fieldset style={{ border: 0, padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: 12 }}>
          <legend style={{ fontWeight: 600, fontSize: 15, marginBottom: 12 }}>O escriba el perfil usted</legend>
          <CamposPerfil {...{ mision, setMision, funciones, setFunciones, requisitos, setRequisitos, riesgo, setRiesgo }} />
        </fieldset>

        {(falta ?? crear.error) && <span role="alert" className="error-formulario">{falta ?? crear.error?.message}</span>}
        <div className="decision" style={{ padding: '8px 0 0' }}>
          <Boton type="submit" variante="primario" icono={<IconoCheck />} cargando={crear.isPending && ruta === 'manual'} textoCargando="Creando…" disabled={crear.isPending}>
            Crear cargo
          </Boton>
          <Boton onClick={() => navegar(rutaLista)} disabled={crear.isPending}>Cancelar</Boton>
          <span className="registro">Escrito por usted, el perfil queda aprobado a su nombre</span>
        </div>
      </form>
    </>
  )
}

/* ---------- Detalle del cargo ---------- */

export function CargoDetalle() {
  const { id = '' } = useParams()
  const cargo = useCargo(id)
  const resumen = useResumenEstructura()
  const [editando, setEditando] = useState(false)

  if (cargo.isPending) {
    return (
      <>
        <div className="encabezado"><Esqueleto alto={14} ancho={220} /><Esqueleto alto={34} ancho={320} style={{ marginTop: 10 }} /></div>
        <div className="tarjeta" style={{ padding: 0 }}><EsqueletoPanel /></div>
      </>
    )
  }
  if (cargo.isError) {
    return (
      <>
        <Migas actual="Cargo" />
        <p role="alert" className="aviso aviso-error">No encontramos ese cargo. Vuelva a la lista y elíjalo otra vez.</p>
      </>
    )
  }

  const c = cargo.data
  return (
    <>
      <header className="encabezado"><Migas actual={c.nombre} /></header>
      {editando
        ? <EditarCargo cargo={c} areas={resumen.data?.nombresAreas ?? []} onCerrar={() => setEditando(false)} />
        : <VerCargo cargo={c} onEditar={() => setEditando(true)} />}
    </>
  )
}

function VerCargo({ cargo, onEditar }: { cargo: Cargo; onEditar: () => void }) {
  const aprobar = useAprobarPerfil()
  const solicitar = useSolicitarPerfil()
  const esIa = cargo.estadoPerfil === 'BorradorIa'
  const p = cargo.perfil
  const e = estadoDe(cargo)

  return (
    <section className="tarjeta detalle-cargo panel-decision" aria-label="Perfil del cargo">
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <div>
          <h2>{cargo.nombre}</h2>
          <p style={{ margin: '4px 0 0', fontSize: 13, color: 'var(--color-texto-suave)' }}>
            {cargo.area} · {numero.format(cargo.empleados)} empleados
            {cargo.nombresNomina.length > 0 && <> · en la nómina como <span className="mono">{cargo.nombresNomina.join(', ')}</span></>}
          </p>
        </div>
        <span style={{ display: 'flex', gap: 6 }}>
          {cargo.editadoManualmente && <span className="pill pill-neutra">Editado</span>}
          <span className={`pill ${e.clase}`}>{e.texto}</span>
        </span>
      </div>

      {!p ? (
        cargo.redactandoPerfil ? (
          <div className="estado-agente"><span className="orbe trabajando" /><span>La IA está redactando el perfil de este cargo.</span></div>
        ) : (
          <div className="aviso aviso-alerta">
            <span style={{ flex: 1 }}>Este cargo no tiene perfil. Pídale a la IA un borrador para revisarlo o escríbalo con «Editar».</span>
            <Boton variante="ia" pequeno icono={<Chispa />} cargando={solicitar.isPending} textoCargando="Pidiendo…" onClick={() => solicitar.mutate(cargo.id)}>
              Pedir perfil a la IA
            </Boton>
          </div>
        )
      ) : (
        <>
          <div className="campo">
            <span>Misión</span>
            <p className={`valor${esIa ? ' ia' : ''}`} style={{ margin: 0 }}>{p.mision}</p>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 12 }}>
            <div className="campo">
              <span>Funciones</span>
              <div className={`valor${esIa ? ' ia' : ''}`}><ul>{p.funciones.map((f) => <li key={f}>{f}</li>)}</ul></div>
            </div>
            <div className="campo">
              <span>Requisitos</span>
              <div className={`valor${esIa ? ' ia' : ''}`}><ul>{p.requisitos.map((r) => <li key={r}>{r}</li>)}</ul></div>
              <span style={{ fontWeight: 400, fontSize: 12, color: 'var(--color-texto-suave)' }}>Nivel de riesgo ARL: <b className="mono">{p.riesgoArl}</b></span>
            </div>
          </div>
          {cargo.razones.length > 0 && esIa && (
            <div className="bloque-ia">
              <div className="bloque-ia-cabeza" style={{ fontSize: 13 }}>
                <Chispa />
                Cómo redactó la IA este perfil
                {cargo.modeloIa && <span className="derecha">{cargo.modeloIa}</span>}
              </div>
              {cargo.razones.map((r) => (
                <div key={r.texto} className="razon">
                  <span className={`signo ${r.signo === '+' ? 'mas' : 'menos'}`}>{r.signo}</span>
                  <span className="texto">{r.texto}</span>
                  <span className="cita">{r.fuente}</span>
                </div>
              ))}
              <div className="protegidas"><IconoEscudo />Los requisitos nunca incluyen edad, género, estado civil, apariencia, foto ni dirección.</div>
            </div>
          )}
        </>
      )}

      <PruebasDelCargo key={cargo.id} cargo={cargo.nombre} />

      {(aprobar.error ?? solicitar.error) && <span role="alert" className="error-formulario">{(aprobar.error ?? solicitar.error)?.message}</span>}
      <div className="decision" style={{ padding: '16px 0 0' }}>
        {esIa && (
          <Boton variante="primario" icono={<IconoCheck />} cargando={aprobar.isPending} textoCargando="Aprobando…" onClick={() => aprobar.mutate(cargo.id)}>
            Aprobar perfil
          </Boton>
        )}
        {cargo.estadoPerfil === 'Aprobado' && (
          <span className="pill pill-ok"><IconoCheck />Aprobado por {cargo.aprobadoPor}{cargo.aprobadoEn ? ` · ${fecha.format(new Date(cargo.aprobadoEn))}` : ''}</span>
        )}
        <Boton onClick={onEditar}>Editar</Boton>
        <span className="registro">Se registran la sugerencia y tu decisión</span>
      </div>
    </section>
  )
}

function EditarCargo({ cargo, areas, onCerrar }: { cargo: Cargo; areas: string[]; onCerrar: () => void }) {
  const editar = useEditarCargo()
  const [nombre, setNombre] = useState(cargo.nombre)
  const [area, setArea] = useState(cargo.area)
  const [activo, setActivo] = useState(cargo.activo)
  const [mision, setMision] = useState(cargo.perfil?.mision ?? '')
  const [funciones, setFunciones] = useState(cargo.perfil?.funciones.join('\n') ?? '')
  const [requisitos, setRequisitos] = useState(cargo.perfil?.requisitos.join('\n') ?? '')
  const [riesgo, setRiesgo] = useState(cargo.perfil?.riesgoArl ?? 'Por definir')
  const conPerfil = cargo.perfil !== null || mision.trim().length > 0

  const guardar = () => {
    const perfil: Perfil | null = conPerfil ? { mision, funciones: lineas(funciones), requisitos: lineas(requisitos), riesgoArl: riesgo } : null
    editar.mutate({ id: cargo.id, nombre, area, activo, perfil }, { onSuccess: onCerrar })
  }

  return (
    <form className="tarjeta detalle-cargo panel-decision formulario" aria-label="Editar cargo" onSubmit={(e) => { e.preventDefault(); guardar() }}>
      <h2>Editar cargo</h2>
      {cargo.estadoPerfil === 'BorradorIa' && (
        <span className="ayuda" style={{ color: 'var(--color-ia)', fontSize: 12 }}>Al guardar, el perfil que propuso la IA queda confirmado por usted.</span>
      )}
      <div className="dos-campos">
        <label>Nombre<input className="entrada" value={nombre} onChange={(e) => setNombre(e.target.value)} required /></label>
        <label>
          Área
          <select className="selector" value={area} onChange={(e) => setArea(e.target.value)}>
            {(areas.includes(cargo.area) ? areas : [cargo.area, ...areas]).map((a) => <option key={a}>{a}</option>)}
          </select>
        </label>
      </div>
      <label className="interruptor"><input type="checkbox" checked={activo} onChange={(e) => setActivo(e.target.checked)} />Cargo activo</label>
      <CamposPerfil {...{ mision, setMision, funciones, setFunciones, requisitos, setRequisitos, riesgo, setRiesgo }} />
      {editar.error && <span role="alert" className="error-formulario">{editar.error.message}</span>}
      <div className="decision" style={{ padding: '8px 0 0', borderTop: 0 }}>
        <Boton type="submit" variante="primario" icono={<IconoCheck />} cargando={editar.isPending} textoCargando="Guardando…">Guardar cambios</Boton>
        <Boton onClick={onCerrar} disabled={editar.isPending}>Cancelar</Boton>
        <span className="registro">Queda registrado quién lo cambió</span>
      </div>
    </form>
  )
}
