import { useRef, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoAlerta, IconoCheck, IconoDocumento, IconoSubir } from '../../shared/ui/Iconos'
import { usePuede } from '../sesion/api'
import { CamposAfiliaciones, CamposSituacion } from './Empleados'
import { LecturaDocumento } from './LecturaDocumento'
import {
  abrirArchivo, type Afiliaciones, legiblePorIa, type Documento, type Empleado, fechaCorta, nombreNovedad, novedadesRegistrables, pesos, type Situacion, type TipoNovedad,
  tiposDocumento, useActualizarDatos, useEmpleado, useGuardarDocumento, useRegistrarNovedad, useRetirar,
} from './api'
import './empleados.css'

type Pestana = 'resumen' | 'historial' | 'carpeta'
const pestanas: [Pestana, string][] = [['resumen', 'Resumen'], ['historial', 'Historial'], ['carpeta', 'Carpeta']]
const fechaHora = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const hoyIso = () => new Date().toISOString().slice(0, 10)

/** Ficha del empleado: resumen con alertas, historial de novedades y carpeta de documentos con versiones. */
export function FichaEmpleado() {
  const { id = '' } = useParams()
  const empleado = useEmpleado(id)
  const [parametros, setParametros] = useSearchParams()
  const pestana = (pestanas.some(([p]) => p === parametros.get('pestana')) ? parametros.get('pestana') : 'resumen') as Pestana

  if (empleado.isPending) {
    return <><header className="encabezado"><Esqueleto alto={32} ancho="40%" /><Esqueleto alto={14} ancho="60%" /></header><Esqueleto alto={360} /></>
  }
  if (empleado.isError) return <p role="alert" className="aviso aviso-error"><IconoAlerta />{empleado.error.message}</p>

  const e = empleado.data
  const s = e.situacion
  return (
    <>
      <header className="encabezado">
        <p className="vin-migas"><Link to="/consola/empleados">Empleados</Link> · <span className="mono">{e.codigo}</span></p>
        <h1>{e.nombre} {e.estado === 'Retirado' && <span className="pill pill-neutra">Retirado</span>}</h1>
        <p>{s.cargo}{s.area ? ` · ${s.area}` : ''}{s.sede ? ` · ${s.sede}` : ''}{s.empresaUsuaria ? ` · ${s.empresaUsuaria}` : ''} · <span className="mono">CC {e.documento}</span>
          {' '}· ingresó el <span className="mono">{fechaCorta(e.fechaIngreso)}</span>
          {e.procesoVinculacionId && <> · <Link to={`/consola/vinculacion/${e.procesoVinculacionId}`}>ver la vinculación</Link></>}</p>
      </header>

      <div role="tablist" aria-label="Expediente" className="pestanas">
        {pestanas.map(([clave, texto]) => (
          <button key={clave} type="button" role="tab" className="pestana" aria-selected={pestana === clave}
            onClick={() => setParametros(clave === 'resumen' ? {} : { pestana: clave }, { replace: true })}>
            {texto}
            {clave === 'resumen' && e.alertas.length > 0 && <span className="nav-contador">{e.alertas.length}</span>}
            {clave === 'carpeta' && <span className="emp-cuenta mono">{e.documentos.length}</span>}
          </button>
        ))}
      </div>

      <div role="tabpanel" className="emp-panel">
        {pestana === 'resumen' && <Resumen e={e} />}
        {pestana === 'historial' && <Historial e={e} />}
        {pestana === 'carpeta' && <Carpeta e={e} />}
      </div>
    </>
  )
}

function Resumen({ e }: { e: Empleado }) {
  const [editando, setEditando] = useState(false)
  const s = e.situacion
  const a = e.afiliaciones
  return (
    <div className="emp-resumen">
      {e.alertas.length > 0 && (
        <ul className="emp-alertas" aria-label="Por atender">
          {e.alertas.map((x) => (
            <li key={x.texto} className={x.nivel === 'Vencido' ? 'vencido' : ''}><IconoAlerta /><span>{x.texto}</span><span className="cita">{x.fuente}</span></li>
          ))}
        </ul>
      )}
      {e.estado === 'Retirado' && <p className="aviso aviso-neutro">Retirado el <span className="mono">{fechaCorta(e.fechaRetiro)}</span>: «{e.motivoRetiro}».</p>}

      <div className="emp-grilla">
        <section className="tarjeta emp-bloque">
          <h2>Situación laboral</h2>
          <dl>
            <dt>Cargo</dt><dd>{s.cargo}</dd>
            <dt>Área</dt><dd>{s.area || '—'}</dd>
            <dt>Centro de costo</dt><dd>{s.centroCosto || '—'}</dd>
            <dt>Sede</dt><dd>{s.sede || '—'}</dd>
            {s.empresaUsuaria && <><dt>Empresa usuaria</dt><dd>{s.empresaUsuaria}</dd></>}
            <dt>Contrato</dt><dd>{s.tipoContrato}{s.fechaFinContrato && <> · hasta <span className="mono">{fechaCorta(s.fechaFinContrato)}</span></>}</dd>
            <dt>Salario</dt><dd className="mono">{pesos.format(s.salarioCop)}</dd>
          </dl>
          <p className="ayuda">Para cambiar el cargo, el salario, la sede o el contrato, registre una novedad en «Historial».</p>
        </section>

        <section className="tarjeta emp-bloque">
          <header><h2>Datos y afiliaciones</h2>{!editando && e.estado === 'Activo' && <Boton pequeno onClick={() => setEditando(true)}>Editar</Boton>}</header>
          {editando ? <EditarDatos e={e} onListo={() => setEditando(false)} /> : (
            <dl>
              <dt>Correo</dt><dd>{e.correo || '—'}</dd>
              <dt>Celular</dt><dd className="mono">{e.celular || '—'}</dd>
              <dt>Ciudad</dt><dd>{e.ciudad || '—'}</dd>
              <dt>EPS</dt><dd>{a.eps || '—'}</dd>
              <dt>Pensión</dt><dd>{a.fondoPension || '—'}</dd>
              <dt>ARL</dt><dd>{a.arl || '—'}{a.claseRiesgoArl ? ` · clase ${a.claseRiesgoArl}` : ''}</dd>
              <dt>Caja</dt><dd>{a.cajaCompensacion || '—'}</dd>
              <dt>Origen</dt><dd>{e.origen}</dd>
            </dl>
          )}
        </section>
      </div>
      {e.estado === 'Activo' && <Retiro e={e} />}
    </div>
  )
}

function EditarDatos({ e, onListo }: { e: Empleado; onListo: () => void }) {
  const guardar = useActualizarDatos(e.id)
  const [f, setF] = useState({ nombre: e.nombre, correo: e.correo, celular: e.celular, ciudad: e.ciudad })
  const [afiliaciones, setAfiliaciones] = useState<Afiliaciones>(e.afiliaciones)
  return (
    <form className="formulario emp-editor" onSubmit={(ev) => { ev.preventDefault(); guardar.mutate({ ...f, afiliaciones }, { onSuccess: onListo }) }}>
      <div className="emp-campos">
        <label>Nombre completo<input className="entrada" value={f.nombre} onChange={(x) => setF({ ...f, nombre: x.target.value })} required /></label>
        <label>Correo<input className="entrada" type="email" value={f.correo} onChange={(x) => setF({ ...f, correo: x.target.value })} /></label>
        <label>Celular<input className="entrada mono" value={f.celular} onChange={(x) => setF({ ...f, celular: x.target.value })} /></label>
        <label>Ciudad<input className="entrada" value={f.ciudad} onChange={(x) => setF({ ...f, ciudad: x.target.value })} /></label>
      </div>
      <CamposAfiliaciones valor={afiliaciones} onCambiar={setAfiliaciones} />
      {guardar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{guardar.error.message}</p>}
      <div className="vin-acciones">
        <Boton type="submit" variante="primario" cargando={guardar.isPending} textoCargando="Guardando…">Guardar</Boton>
        <Boton onClick={onListo} disabled={guardar.isPending}>Cancelar</Boton>
      </div>
    </form>
  )
}

/** Retiro: decisión humana con fecha y motivo (lo hacen quienes aprueban en la bandeja). */
function Retiro({ e }: { e: Empleado }) {
  const puede = usePuede('AprobarBandeja')
  const retirar = useRetirar(e.id)
  const [abierto, setAbierto] = useState(false)
  const [fecha, setFecha] = useState(hoyIso())
  const [motivo, setMotivo] = useState('')
  if (!puede) return null
  if (!abierto) return <div className="emp-retiro"><button type="button" className="enlace-boton" onClick={() => setAbierto(true)}>Registrar el retiro…</button></div>
  return (
    <section className="tarjeta emp-bloque emp-retiro-form">
      <h2>Registrar el retiro</h2>
      <div className="emp-campos">
        <label>Fecha de retiro<input className="entrada" type="date" value={fecha} onChange={(x) => setFecha(x.target.value)} /></label>
        <label className="emp-ancho">Motivo<input className="entrada" value={motivo} onChange={(x) => setMotivo(x.target.value)} placeholder="Renuncia voluntaria, terminación del contrato…" /></label>
      </div>
      {retirar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{retirar.error.message}</p>}
      <div className="decision">
        <Boton variante="primario" disabled={!motivo.trim()} cargando={retirar.isPending} textoCargando="Registrando…" onClick={() => retirar.mutate({ fecha, motivo })}>Registrar el retiro</Boton>
        <Boton onClick={() => setAbierto(false)} disabled={retirar.isPending}>Cancelar</Boton>
        <span className="registro">Decisión sobre una persona · queda en la bitácora</span>
      </div>
    </section>
  )
}

function Historial({ e }: { e: Empleado }) {
  const [registrando, setRegistrando] = useState(false)
  return (
    <div className="emp-historial">
      {e.estado === 'Activo' && (registrando
        ? <NuevaNovedad e={e} onListo={() => setRegistrando(false)} />
        : <Boton variante="primario" onClick={() => setRegistrando(true)}>Registrar novedad</Boton>)}
      <ol className="emp-linea">
        {e.novedades.map((n) => (
          <li key={n.id} className={`emp-novedad emp-novedad-${n.tipo.toLowerCase()}`}>
            <span className="emp-linea-punto" aria-hidden="true" />
            <div>
              <header><b>{nombreNovedad[n.tipo]}</b><span className="mono">desde {fechaCorta(n.fechaEfectiva)}</span></header>
              <ul>{n.cambios.map((c) => <li key={c}>{c}</li>)}</ul>
              {n.motivo && <p className="emp-motivo">«{n.motivo}»</p>}
              <span className="emp-sub">Registró {n.por} · <span className="mono">{fechaHora.format(new Date(n.en))}</span></span>
            </div>
          </li>
        ))}
      </ol>
    </div>
  )
}

function NuevaNovedad({ e, onListo }: { e: Empleado; onListo: () => void }) {
  const registrar = useRegistrarNovedad(e.id)
  const [tipo, setTipo] = useState<TipoNovedad>('CambioCargo')
  const [situacion, setSituacion] = useState<Situacion>(e.situacion)
  const [fecha, setFecha] = useState(hoyIso())
  const [motivo, setMotivo] = useState('')
  return (
    <form className="tarjeta emp-formulario formulario" onSubmit={(ev) => { ev.preventDefault(); registrar.mutate({ tipo, situacion, fechaEfectiva: fecha, motivo }, { onSuccess: onListo }) }}>
      <h2>Registrar novedad</h2>
      <div className="emp-campos">
        <label>Tipo de novedad
          <select className="selector" value={tipo} onChange={(x) => setTipo(x.target.value as TipoNovedad)}>
            {novedadesRegistrables.map((t) => <option key={t} value={t}>{nombreNovedad[t]}</option>)}
          </select>
        </label>
        <label>Desde<input className="entrada" type="date" value={fecha} onChange={(x) => setFecha(x.target.value)} required /></label>
        <label className="emp-ancho">Motivo<input className="entrada" value={motivo} onChange={(x) => setMotivo(x.target.value)} placeholder="Ascenso, ajuste salarial anual, traslado a la sede Bogotá…" required /></label>
      </div>
      <h3>Cómo queda</h3>
      <CamposSituacion valor={situacion} onCambiar={setSituacion} />
      <span className="ayuda">Solo se guarda lo que cambia; el historial muestra antes y después.</span>
      {registrar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{registrar.error.message}</p>}
      <div className="vin-acciones emp-acciones">
        <Boton onClick={onListo} disabled={registrar.isPending}>Cancelar</Boton>
        <Boton type="submit" variante="primario" cargando={registrar.isPending} textoCargando="Registrando…">Registrar</Boton>
      </div>
    </form>
  )
}

const vigencia: Record<Documento['estadoVigencia'], [string, string]> = {
  SinVencimiento: ['pill-neutra', 'Sin vencimiento'], Vigente: ['pill-ok', 'Vigente'], PorVencer: ['pill-alerta', 'Por vencer'], Vencido: ['pill-error', 'Vencido'],
}

function Carpeta({ e }: { e: Empleado }) {
  const [agregando, setAgregando] = useState(false)
  return (
    <div className="emp-carpeta">
      {e.estado === 'Activo' && (agregando ? <SubirDocumento e={e} onListo={() => setAgregando(false)} />
        : <Boton variante="primario" icono={<IconoSubir />} onClick={() => setAgregando(true)}>Agregar documento</Boton>)}
      {e.documentos.length === 0 && !agregando && <p className="vacio">La carpeta está vacía. Agregue la cédula, el contrato, certificados y licencias con su fecha de vencimiento.</p>}
      <ul className="emp-documentos">
        {e.documentos.map((d) => <DocumentoFila key={d.id} e={e} d={d} />)}
      </ul>
    </div>
  )
}

function DocumentoFila({ e, d }: { e: Empleado; d: Documento }) {
  const [versiones, setVersiones] = useState(false)
  const [nueva, setNueva] = useState(false)
  const [abriendo, setAbriendo] = useState<string>()
  const ver = async (archivoId: string) => { setAbriendo(archivoId); try { await abrirArchivo(e.id, archivoId) } finally { setAbriendo(undefined) } }
  const vigente = d.versiones[0]
  return (
    <li className="tarjeta emp-documento">
      <div className="emp-documento-fila">
        <span className="emp-documento-icono"><IconoDocumento /></span>
        <div className="emp-documento-texto">
          <b>{d.nombre}</b>
          <span>{d.tipo} · {vigente?.nombreArchivo}{d.venceEn && <> · vence <span className="mono">{fechaCorta(d.venceEn)}</span></>}</span>
        </div>
        <span className={`pill ${vigencia[d.estadoVigencia][0]}`}>{vigencia[d.estadoVigencia][1]}</span>
        {vigente && <Boton pequeno cargando={abriendo === vigente.archivoId} textoCargando="Abriendo…" onClick={() => ver(vigente.archivoId)}>Ver</Boton>}
        {e.estado === 'Activo' && <Boton pequeno icono={<IconoSubir />} onClick={() => setNueva((v) => !v)}>Nueva versión</Boton>}
        {d.versiones.length > 1 && <button type="button" className="enlace-boton" aria-expanded={versiones} onClick={() => setVersiones((v) => !v)}>{d.versiones.length} versiones</button>}
      </div>
      <LecturaDocumento e={e} d={d} />
      {nueva && <SubirDocumento e={e} documento={d} onListo={() => setNueva(false)} />}
      {versiones && (
        <ol className="emp-versiones">
          {d.versiones.map((v, i) => (
            <li key={v.archivoId}>
              <span className="mono">{i === 0 ? 'Vigente' : `v${d.versiones.length - i}`}</span>
              <span>{v.nombreArchivo}{v.venceEn && <> · vence <span className="mono">{fechaCorta(v.venceEn)}</span></>}{v.nota && <> · «{v.nota}»</>}</span>
              <span className="emp-sub">{v.subidoPor} · {fechaHora.format(new Date(v.subidoEn))}</span>
              <Boton pequeno cargando={abriendo === v.archivoId} textoCargando="Abriendo…" onClick={() => ver(v.archivoId)}>Ver</Boton>
            </li>
          ))}
        </ol>
      )}
    </li>
  )
}

/** Documento nuevo o versión nueva de uno que ya está. */
function SubirDocumento({ e, documento, onListo }: { e: Empleado; documento?: Documento; onListo: () => void }) {
  const guardar = useGuardarDocumento(e.id)
  const archivo = useRef<HTMLInputElement>(null)
  const [elegido, setElegido] = useState<File>()
  const [tipo, setTipo] = useState(tiposDocumento[0])
  const [nombre, setNombre] = useState('')
  const [vence, setVence] = useState(documento?.venceEn ?? '')
  const [nota, setNota] = useState('')
  const [leerConIa, setLeerConIa] = useState(true)
  const enviar = () => elegido && guardar.mutate(
    { archivo: elegido, documentoId: documento?.id, tipo: documento ? undefined : tipo, nombre: documento ? undefined : (nombre || tipo), venceEn: vence || null, nota,
      leerConIa: leerConIa && legiblePorIa(elegido.name) },
    { onSuccess: onListo })

  return (
    <div className="emp-subir">
      {!documento && <h3>Agregar documento</h3>}
      <div className="emp-campos">
        {!documento && (
          <>
            <label>Tipo
              <select className="selector" value={tipo} onChange={(x) => setTipo(x.target.value)}>{tiposDocumento.map((t) => <option key={t}>{t}</option>)}</select>
            </label>
            <label>Nombre <span className="ayuda">opcional</span><input className="entrada" value={nombre} onChange={(x) => setNombre(x.target.value)} placeholder="Licencia de montacargas" /></label>
          </>
        )}
        <label>Vence <span className="ayuda">si aplica</span><input className="entrada" type="date" value={vence} onChange={(x) => setVence(x.target.value)} /></label>
        {documento && <label>Nota<input className="entrada" value={nota} onChange={(x) => setNota(x.target.value)} placeholder="Renovada" /></label>}
        <label>Archivo
          <span className="emp-archivo">
            <input ref={archivo} type="file" hidden accept=".pdf,.docx,image/jpeg,image/png,image/webp" onChange={(x) => setElegido(x.target.files?.[0])} />
            <Boton pequeno onClick={() => archivo.current?.click()}>{elegido ? 'Cambiar' : 'Elegir archivo'}</Boton>
            <span className="emp-sub">{elegido?.name ?? 'PDF, Word o foto · se guarda cifrado'}</span>
          </span>
        </label>
      </div>
      <label className="emp-leer-ia">
        <input type="checkbox" checked={leerConIa} onChange={(x) => setLeerConIa(x.target.checked)} />
        <span>Leer con IA y proponer cambios al expediente <span className="emp-sub">(PDF o foto; usted decide qué se aplica)</span></span>
      </label>
      {guardar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{guardar.error.message}</p>}
      <div className="vin-acciones">
        <Boton variante="primario" icono={<IconoCheck />} disabled={!elegido} cargando={guardar.isPending} textoCargando="Subiendo…" onClick={enviar}>Guardar</Boton>
        <Boton onClick={onListo} disabled={guardar.isPending}>Cancelar</Boton>
      </div>
    </div>
  )
}
