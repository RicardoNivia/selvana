import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto, EsqueletoPanel } from '../../shared/ui/Esqueleto'
import { Chispa, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { type PruebaEnBateria, useBateriaCargo, useGuardarBateriaCargo } from '../pruebas/api'
import { EditorBateria } from '../pruebas/EditorBateria'
import { type Cargo, type Perfil, useCargo, useCrearCargo, useEditarCargo, useResumenEstructura, useSolicitarPerfil } from './api'

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const numero = new Intl.NumberFormat('es-CO')
const clasesArl = ['Clase I', 'Clase II', 'Clase III', 'Clase IV', 'Clase V', 'Por definir']
const lineas = (t: string) => t.split('\n').map((l) => l.trim()).filter(Boolean)
const rutaLista = '/consola/empresa/estructura'

export function estadoDe(c: Cargo) {
  if (!c.activo) return { texto: 'Inactivo', clase: 'pill-neutra' }
  if (c.estadoPerfil === 'Aprobado') return { texto: 'Aprobado', clase: 'pill-ok' }
  if (c.estadoPerfil === 'BorradorIa') return { texto: 'Borrador IA', clase: 'pill-ia' }
  return c.redactandoPerfil ? { texto: 'Redactando…', clase: 'pill-ia' } : { texto: 'Sin perfil', clase: 'pill-alerta' }
}

function Migas({ actual }: { actual: string }) {
  return <p style={{ margin: '0 0 8px', fontSize: 13 }}><Link to={rutaLista}>Estructura y cargos</Link> · {actual}</p>
}

/* ---------- Rutas: nuevo y detalle usan el mismo formulario ---------- */

export function CargoNuevo() {
  return <FormularioCargo />
}

export function CargoDetalle() {
  const { id = '' } = useParams()
  const cargo = useCargo(id)

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
  return <FormularioCargo key={cargo.data.id} cargo={cargo.data} />
}

/* ---------- Formulario del cargo ---------- */

type CampoPerfil = 'mision' | 'funciones' | 'requisitos' | 'riesgo'
const todoIa = { mision: true, funciones: true, requisitos: true, riesgo: true }

/**
 * Una sola pantalla para crear y para ver o editar un cargo: nombre, área, perfil y pruebas, todo editable.
 * «Generar con IA» llena estos mismos campos en violeta (lo que propuso la IA); cada campo que la persona toca deja
 * de ser violeta. Al guardar, el perfil queda aprobado por quien guarda y se vuelve a la lista.
 */
function FormularioCargo({ cargo }: { cargo?: Cargo }) {
  const navegar = useNavigate()
  const resumen = useResumenEstructura()
  const crear = useCrearCargo()
  const editar = useEditarCargo()
  const solicitar = useSolicitarPerfil()
  const guardarBateria = useGuardarBateriaCargo()
  const [cargoId, setCargoId] = useState(cargo?.id)
  const actual = useCargo(cargoId ?? '', Boolean(cargoId)).data ?? cargo
  const esBorrador = cargo?.estadoPerfil === 'BorradorIa'

  const [nombre, setNombre] = useState(cargo?.nombre ?? '')
  const [area, setArea] = useState(cargo?.area ?? '')
  const [activo, setActivo] = useState(cargo?.activo ?? true)
  const [descripcion, setDescripcion] = useState('')
  const [mision, setMision] = useState(cargo?.perfil?.mision ?? '')
  const [funciones, setFunciones] = useState(cargo?.perfil?.funciones.join('\n') ?? '')
  const [requisitos, setRequisitos] = useState(cargo?.perfil?.requisitos.join('\n') ?? '')
  const [riesgo, setRiesgo] = useState(cargo?.perfil?.riesgoArl ?? 'Por definir')
  const [deIa, setDeIa] = useState<Partial<Record<CampoPerfil, boolean>>>(esBorrador ? todoIa : {})
  const [llenadoCon, setLlenadoCon] = useState(cargo?.perfil?.mision)
  const [pidiendo, setPidiendo] = useState(false)
  const [pruebas, setPruebas] = useState<PruebaEnBateria[]>()
  const [falta, setFalta] = useState<string>()
  const areasActivas = resumen.data?.nombresAreas ?? []
  const areas = cargo && !areasActivas.includes(cargo.area) ? [cargo.area, ...areasActivas] : areasActivas
  const sinAreas = resumen.isSuccess && areas.length === 0
  const aprobado = actual?.estadoPerfil === 'Aprobado'

  // La batería sugerida depende del nombre: se pide cuando la persona deja de escribir
  const [nombreBateria, setNombreBateria] = useState(cargo?.nombre ?? '')
  useEffect(() => {
    const t = setTimeout(() => setNombreBateria(nombre.trim()), 600)
    return () => clearTimeout(t)
  }, [nombre])
  const bateria = useBateriaCargo(nombreBateria)

  // Cuando llega un borrador nuevo de la IA, llena los campos que la persona ve y los marca en violeta
  useEffect(() => {
    if (!actual?.perfil || actual.estadoPerfil !== 'BorradorIa' || actual.perfil.mision === llenadoCon) return
    setMision(actual.perfil.mision)
    setFunciones(actual.perfil.funciones.join('\n'))
    setRequisitos(actual.perfil.requisitos.join('\n'))
    setRiesgo(actual.perfil.riesgoArl)
    setDeIa(todoIa)
    setLlenadoCon(actual.perfil.mision)
    setPidiendo(false)
  }, [actual, llenadoCon])

  // Si la IA termina sin borrador nuevo (falló o se agotó la espera), se suelta el estado de «redactando»
  const [vioRedactar, setVioRedactar] = useState(false)
  useEffect(() => {
    if (!pidiendo || !actual) return
    if (actual.redactandoPerfil) setVioRedactar(true)
    else if (vioRedactar) { setPidiendo(false); setVioRedactar(false) }
  }, [pidiendo, actual, vioRedactar])

  const redactando = pidiendo || Boolean(actual?.redactandoPerfil && !actual.perfil)
  const tocar = (campo: CampoPerfil, set: (v: string) => void) => (v: string) => { set(v); setDeIa((d) => ({ ...d, [campo]: false })) }
  const hayIa = Object.values(deIa).some(Boolean)

  const validar = () => {
    if (!nombre.trim() || !area) { setFalta('Escriba el nombre del cargo y elija su área.'); return false }
    setFalta(undefined)
    return true
  }

  const generar = () => {
    if (!validar()) return
    setPidiendo(true)
    const listo = { onError: () => setPidiendo(false) }
    if (cargoId) solicitar.mutate(cargoId, listo)
    else crear.mutate({ nombre, area, descripcion: descripcion.trim() || undefined }, { ...listo, onSuccess: (r) => setCargoId(r.id) })
  }

  const guardar = async () => {
    if (!validar()) return
    const perfil: Perfil | undefined = mision.trim() ? { mision, funciones: lineas(funciones), requisitos: lineas(requisitos), riesgoArl: riesgo } : undefined
    try {
      if (cargoId) await editar.mutateAsync({ id: cargoId, nombre, area, activo, perfil: perfil ?? null })
      else await crear.mutateAsync({ nombre, area, perfil })
      const elegidas = pruebas ?? bateria.data?.pruebas ?? []
      if (elegidas.length > 0) await guardarBateria.mutateAsync({ cargo: nombre.trim(), pruebas: elegidas })
      navegar(rutaLista)
    } catch {
      // El error se muestra en el formulario
    }
  }

  const guardando = editar.isPending || guardarBateria.isPending || (crear.isPending && !pidiendo)
  const error = falta ?? (crear.error ?? editar.error ?? solicitar.error ?? guardarBateria.error)?.message
  const e = actual ? estadoDe(actual) : null

  return (
    <>
      <header className="encabezado" style={{ display: 'flex', flexWrap: 'wrap', gap: 12, justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <div>
          <Migas actual={cargo ? cargo.nombre : 'Nuevo cargo'} />
          <h1>{cargo ? cargo.nombre : 'Nuevo cargo'}</h1>
          <p>
            {actual && actual.empleados > 0 && <>{numero.format(actual.empleados)} empleados · </>}
            {actual && actual.nombresNomina.length > 0 && <>en la nómina como <span className="mono">{actual.nombresNomina.join(', ')}</span> · </>}
            Lo que proponga la IA se ve en violeta hasta que usted lo guarde.
          </p>
        </div>
        {e && (
          <span style={{ display: 'flex', gap: 6 }}>
            {actual?.editadoManualmente && <span className="pill pill-neutra">Editado</span>}
            <span className={`pill ${e.clase}`}>{e.texto}</span>
          </span>
        )}
      </header>

      <form className="tarjeta formulario" style={{ padding: 24, display: 'flex', flexDirection: 'column', gap: 20 }}
        onSubmit={(ev) => { ev.preventDefault(); void guardar() }}>
        <div className="dos-campos">
          <label>
            Nombre del cargo
            <input className="entrada" value={nombre} onChange={(ev) => setNombre(ev.target.value)} placeholder="Ej.: Analista de datos" maxLength={200} />
          </label>
          <label>
            Área
            <select className="selector" value={area} onChange={(ev) => setArea(ev.target.value)} disabled={sinAreas}>
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
        {cargo && <label className="interruptor"><input type="checkbox" checked={activo} onChange={(ev) => setActivo(ev.target.checked)} />Cargo activo</label>}

        {!aprobado && (
          <div className="bloque-ia">
            <div className="bloque-ia-cabeza"><Chispa />Llenar el perfil con IA</div>
            <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 12 }}>
              {!cargoId && (
                <label>
                  Cuéntele a la IA sobre el cargo <span className="ayuda">Opcional</span>
                  <textarea className="area-texto" rows={3} value={descripcion} onChange={(ev) => setDescripcion(ev.target.value)} maxLength={2000}
                    placeholder="Ej.: Arma tableros de ventas en Power BI para la gerencia comercial, cruza datos del ERP y del CRM. Profesional en estadística o ingeniería con 2 años de experiencia." />
                </label>
              )}
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, alignItems: 'center' }}>
                <Boton variante="ia" icono={<Chispa />} cargando={redactando} textoCargando="La IA está redactando…" disabled={guardando} onClick={generar}>
                  {llenadoCon ? 'Pedir otro borrador' : 'Generar con IA'}
                </Boton>
                <span className="ayuda">La IA llena misión, funciones, requisitos y riesgo ARL aquí mismo. Usted los revisa, ajusta y guarda.</span>
              </div>
            </div>
          </div>
        )}

        {redactando && (
          <div className="estado-agente" role="status"><span className="orbe trabajando" /><span>La IA está redactando el perfil. Los campos se llenan aquí apenas termine.</span></div>
        )}

        <label>
          Misión
          <textarea className={`area-texto${deIa.mision ? ' entrada-ia' : ''}`} rows={2} value={mision} onChange={(ev) => tocar('mision', setMision)(ev.target.value)}
            placeholder="Para qué existe el cargo, en una frase" />
        </label>
        <div className="dos-campos">
          <label>
            Funciones <span className="ayuda">Una por línea</span>
            <textarea className={`area-texto${deIa.funciones ? ' entrada-ia' : ''}`} rows={6} value={funciones} onChange={(ev) => tocar('funciones', setFunciones)(ev.target.value)} />
          </label>
          <label>
            Requisitos <span className="ayuda">Uno por línea</span>
            <textarea className={`area-texto${deIa.requisitos ? ' entrada-ia' : ''}`} rows={6} value={requisitos} onChange={(ev) => tocar('requisitos', setRequisitos)(ev.target.value)} />
          </label>
        </div>
        <label style={{ maxWidth: 220 }}>
          Riesgo ARL
          <select className={`selector${deIa.riesgo ? ' entrada-ia' : ''}`} value={riesgo} onChange={(ev) => tocar('riesgo', setRiesgo)(ev.target.value)}>
            {clasesArl.map((c) => <option key={c}>{c}</option>)}
          </select>
        </label>

        {hayIa && actual && actual.razones.length > 0 && (
          <div className="bloque-ia">
            <div className="bloque-ia-cabeza" style={{ fontSize: 13 }}><Chispa />Cómo redactó la IA este perfil{actual.modeloIa && <span className="derecha">{actual.modeloIa}</span>}</div>
            {actual.razones.map((r) => (
              <div key={r.texto} className="razon">
                <span className={`signo ${r.signo === '+' ? 'mas' : 'menos'}`}>{r.signo}</span>
                <span className="texto">{r.texto}</span>
                <span className="cita">{r.fuente}</span>
              </div>
            ))}
            <div className="protegidas"><IconoEscudo />Los requisitos nunca incluyen edad, género, estado civil, apariencia, foto ni dirección.</div>
          </div>
        )}

        <div className="campo pruebas-cargo">
          <span>Pruebas psicotécnicas</span>
          {!nombreBateria ? (
            <span className="sub-celda">Escriba el nombre del cargo y Selvana sugiere las pruebas.</span>
          ) : bateria.isPending ? <Esqueleto alto={120} /> : bateria.data && (
            <>
              {!bateria.data.esSugerida && bateria.data.actualizadaPor && (
                <span className="sub-celda">Configuradas por {bateria.data.actualizadaPor}. Las vacantes nuevas de este cargo las heredan.</span>
              )}
              <EditorBateria key={`${nombreBateria}-${bateria.data.actualizadaEn ?? 'sugerida'}`} inicial={bateria.data.pruebas}
                sugerida={bateria.data.esSugerida} razon={bateria.data.razon} onCambiar={setPruebas} />
            </>
          )}
        </div>

        {error && <span role="alert" className="error-formulario">{error}</span>}
        <div className="decision" style={{ padding: '16px 0 0' }}>
          <Boton type="submit" variante="primario" icono={<IconoCheck />} cargando={guardando} textoCargando="Guardando…" disabled={redactando}>
            {hayIa ? 'Aprobar y guardar' : 'Guardar cargo'}
          </Boton>
          <Boton onClick={() => navegar(rutaLista)} disabled={guardando}>Volver a cargos</Boton>
          {aprobado && actual?.aprobadoPor && (
            <span className="pill pill-ok"><IconoCheck />Aprobado por {actual.aprobadoPor}{actual.aprobadoEn ? ` · ${fecha.format(new Date(actual.aprobadoEn))}` : ''}</span>
          )}
          <span className="registro">Se registran la sugerencia y tu decisión</span>
        </div>
      </form>
    </>
  )
}
