import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto, EsqueletoFilas, EsqueletoPanel } from '../../shared/ui/Esqueleto'
import { Chispa, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { Buscador, Paginacion } from '../../shared/ui/Paginacion'
import { usePaginacion } from '../../shared/ui/paginado'
import { PruebasDelCargo } from '../pruebas/PruebasDelCargo'
import {
  type Cargo, type CentroCosto, type Maestro, type Perfil, type ResumenEstructura, type TipoMaestro,
  useAprobarPerfil, useCargos, useCrearCargo, useEditarCargo, useEditarMaestro, useMaestros, useResumenEstructura, useSolicitarPerfil,
} from './api'

type Pestana = 'cargos' | TipoMaestro

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const numero = new Intl.NumberFormat('es-CO')
const clasesArl = ['Clase I', 'Clase II', 'Clase III', 'Clase IV', 'Clase V', 'Por definir']

function estadoDe(c: Cargo) {
  if (!c.activo) return { texto: 'Inactivo', clase: 'pill-neutra' }
  if (c.estadoPerfil === 'Aprobado') return { texto: 'Aprobado', clase: 'pill-ok' }
  if (c.estadoPerfil === 'BorradorIa') return { texto: 'Borrador IA', clase: 'pill-ia' }
  return c.redactandoPerfil ? { texto: 'Redactando…', clase: 'pill-ia' } : { texto: 'Sin perfil', clase: 'pill-alerta' }
}

export function EstructuraCargos() {
  const resumen = useResumenEstructura()
  const [pestana, setPestana] = useState<Pestana>('cargos')

  if (resumen.isPending) return <EsqueletoPantalla />
  if (resumen.isError) return <p role="alert" className="aviso aviso-error">No hay conexión con la API. Revise que esté corriendo en el puerto 5000.</p>

  const r = resumen.data
  if (r.cargos === 0 && r.sedes === 0) {
    return (
      <div className="tarjeta vacio">
        <h2>La estructura llega de la nómina</h2>
        <p>Suba la nómina; la IA ordena sedes, áreas, centros de costo y cargos, y usted aprueba.</p>
        <Link to="/consola/empresa/conectar-nomina" className="boton boton-primario">Conectar nómina</Link>
      </div>
    )
  }

  const pestanas: [Pestana, string][] = [
    ['cargos', `Cargos · ${r.cargos}`],
    ['sedes', `Sedes · ${r.sedes}`],
    ['areas', `Áreas · ${r.areas}`],
    ['centros-costo', `Centros de costo · ${r.centrosCosto}`],
  ]

  return (
    <>
      <header className="encabezado" style={{ display: 'flex', flexWrap: 'wrap', gap: 16, justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <div>
          <h1>Estructura y cargos</h1>
          <p>
            Viene de la nómina ({r.fuente ?? 'sin fuente'}{r.actualizadaEn ? `, ${fecha.format(new Date(r.actualizadaEn))}` : ''}). Puede editarla como datos
            maestros: la siguiente sincronización no pisa lo que usted cambie.
          </p>
        </div>
        {r.perfilesPorRevisar > 0 && <span className="pill pill-ia">{r.perfilesPorRevisar} perfiles de cargo por revisar</span>}
      </header>

      {r.perfilesRedactando > 0 && (
        <div className="estado-agente" role="status">
          <span className="orbe trabajando" />
          <div style={{ flex: 1 }}>
            <b>La IA está redactando perfiles: faltan {r.perfilesRedactando} de {r.cargos}</b>
            <div className="barra" style={{ marginTop: 8 }}>
              <span style={{ width: `${((r.cargos - r.perfilesRedactando) / r.cargos) * 100}%` }} />
            </div>
          </div>
        </div>
      )}

      <div role="tablist" aria-label="Estructura" className="pestanas">
        {pestanas.map(([clave, texto]) => (
          <button key={clave} type="button" role="tab" className="pestana" aria-selected={pestana === clave} onClick={() => setPestana(clave)}>
            {texto}
          </button>
        ))}
      </div>

      {pestana === 'cargos' ? <Cargos resumen={r} /> : <TablaMaestros key={pestana} tipo={pestana} resumen={r} />}
    </>
  )
}

function EsqueletoPantalla() {
  return (
    <>
      <div className="encabezado">
        <Esqueleto alto={34} ancho={320} />
        <Esqueleto alto={14} ancho="60%" style={{ marginTop: 10 }} />
      </div>
      <Esqueleto alto={44} ancho="100%" />
      <div className="dos-columnas">
        <div className="tarjeta lista-cargos"><EsqueletoFilas filas={8} /></div>
        <div className="tarjeta detalle-cargo" style={{ padding: 0 }}><EsqueletoPanel /></div>
      </div>
    </>
  )
}

/* ---------- Cargos ---------- */

const filtrosEstado: [string, string][] = [['', 'Todos'], ['BorradorIa', 'Por revisar'], ['Aprobado', 'Aprobados'], ['SinPerfil', 'Sin perfil']]

function Cargos({ resumen }: { resumen: ResumenEstructura }) {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const [buscar, setBuscar] = useState('')
  const [estado, setEstado] = useState('')
  const [seleccion, setSeleccion] = useState<string>()
  const [creando, setCreando] = useState(false)
  const cargos = useCargos({ pagina, tamano, buscar, estado })
  const alBuscar = useCallback((t: string) => { setBuscar(t); setPagina(1) }, [setPagina])

  const items = cargos.data?.items ?? []
  const cargo = items.find((c) => c.id === seleccion) ?? items[0]

  return (
    <>
      {creando ? (
        <NuevoCargo areas={resumen.nombresAreas} onListo={(id) => { setCreando(false); if (id) { setSeleccion(id); setBuscar(''); setPagina(1) } }} />
      ) : (
        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
          <Boton icono={<Chispa />} variante="secundario" onClick={() => setCreando(true)}>Nuevo cargo</Boton>
        </div>
      )}
      <div className="dos-columnas">
        <section className="tarjeta lista-cargos" aria-label="Cargos">
          <div className="barra-filtros">
            <Buscador etiqueta="Buscar cargo o área" onBuscar={alBuscar} />
            <div className="segmentado" role="group" aria-label="Estado del perfil">
              {filtrosEstado.map(([v, t]) => (
                <button key={v} type="button" aria-pressed={estado === v} onClick={() => { setEstado(v); setPagina(1) }}>{t}</button>
              ))}
            </div>
          </div>
          {cargos.isPending ? (
            <EsqueletoFilas filas={8} />
          ) : items.length === 0 ? (
            <div className="vacio" style={{ padding: 32 }}><p>No hay cargos con ese filtro.</p></div>
          ) : (
            items.map((c) => {
              const e = estadoDe(c)
              return (
                <button key={c.id} type="button" className="item-cargo" aria-pressed={c.id === cargo?.id} onClick={() => setSeleccion(c.id)}>
                  <span className="nombre">
                    <b>{c.nombre}</b>
                    <span>{c.area}</span>
                  </span>
                  <span className="mono" style={{ fontWeight: 600, fontSize: 13 }}>{numero.format(c.empleados)}</span>
                  <span className={`pill ${e.clase}`}>{e.texto}</span>
                </button>
              )
            })
          )}
          {cargos.data && <Paginacion pagina={cargos.data} cargando={cargos.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
        </section>
        {cargos.isPending ? (
          <div className="tarjeta detalle-cargo" style={{ padding: 0 }}><EsqueletoPanel /></div>
        ) : cargo ? (
          <DetalleCargo key={cargo.id} cargo={cargo} areas={resumen.nombresAreas} />
        ) : null}
      </div>
    </>
  )
}

function NuevoCargo({ areas, onListo }: { areas: string[]; onListo: (id?: string) => void }) {
  const crear = useCrearCargo()
  const [nombre, setNombre] = useState('')
  const [area, setArea] = useState(areas[0] ?? '')

  return (
    <form
      className="tarjeta formulario"
      style={{ padding: 20 }}
      onSubmit={(e) => { e.preventDefault(); crear.mutate({ nombre, area }, { onSuccess: (r) => onListo(r.id) }) }}
    >
      <b style={{ fontSize: 15 }}>Nuevo cargo</b>
      <div className="dos-campos">
        <label>
          Nombre del cargo
          <input className="entrada" value={nombre} onChange={(e) => setNombre(e.target.value)} placeholder="Ej.: Analista de datos" required />
        </label>
        <label>
          Área
          <select className="selector" value={area} onChange={(e) => setArea(e.target.value)}>
            {areas.map((a) => <option key={a}>{a}</option>)}
          </select>
        </label>
      </div>
      <span className="ayuda" style={{ color: 'var(--color-ia)', fontSize: 12 }}>La IA redactará el perfil del cargo; usted lo revisa y aprueba.</span>
      {crear.error && <span role="alert" className="error-formulario">{crear.error.message}</span>}
      <div style={{ display: 'flex', gap: 10 }}>
        <Boton type="submit" variante="primario" cargando={crear.isPending} textoCargando="Creando…">Crear cargo</Boton>
        <Boton onClick={() => onListo()} disabled={crear.isPending}>Cancelar</Boton>
      </div>
    </form>
  )
}

function DetalleCargo({ cargo, areas }: { cargo: Cargo; areas: string[] }) {
  const [editando, setEditando] = useState(false)
  if (editando) return <EditarCargo cargo={cargo} areas={areas} onCerrar={() => setEditando(false)} />
  return <VerCargo cargo={cargo} onEditar={() => setEditando(true)} />
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
            <span style={{ flex: 1 }}>Este cargo no tiene perfil. Pídale a la IA un borrador para revisarlo.</span>
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

const lineas = (t: string) => t.split('\n').map((l) => l.trim()).filter(Boolean)

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
  const opcionesArea = areas.includes(cargo.area) ? areas : [cargo.area, ...areas]

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
          <select className="selector" value={area} onChange={(e) => setArea(e.target.value)}>{opcionesArea.map((a) => <option key={a}>{a}</option>)}</select>
        </label>
      </div>
      <label className="interruptor"><input type="checkbox" checked={activo} onChange={(e) => setActivo(e.target.checked)} />Cargo activo</label>
      <label>Misión<textarea className="area-texto" rows={2} value={mision} onChange={(e) => setMision(e.target.value)} /></label>
      <div className="dos-campos">
        <label>Funciones <span className="ayuda">Una por línea</span><textarea className="area-texto" rows={6} value={funciones} onChange={(e) => setFunciones(e.target.value)} /></label>
        <label>Requisitos <span className="ayuda">Uno por línea</span><textarea className="area-texto" rows={6} value={requisitos} onChange={(e) => setRequisitos(e.target.value)} /></label>
      </div>
      <label style={{ maxWidth: 220 }}>
        Riesgo ARL
        <select className="selector" value={riesgo} onChange={(e) => setRiesgo(e.target.value)}>{clasesArl.map((c) => <option key={c}>{c}</option>)}</select>
      </label>
      {editar.error && <span role="alert" className="error-formulario">{editar.error.message}</span>}
      <div className="decision" style={{ padding: '8px 0 0', borderTop: 0 }}>
        <Boton type="submit" variante="primario" icono={<IconoCheck />} cargando={editar.isPending} textoCargando="Guardando…">Guardar cambios</Boton>
        <Boton onClick={onCerrar} disabled={editar.isPending}>Cancelar</Boton>
        <span className="registro">Queda registrado quién lo cambió</span>
      </div>
    </form>
  )
}

/* ---------- Sedes, áreas y centros de costo ---------- */

type FilaMaestro = Maestro | CentroCosto

function TablaMaestros({ tipo, resumen }: { tipo: TipoMaestro; resumen: ResumenEstructura }) {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const [buscar, setBuscar] = useState('')
  const [editando, setEditando] = useState<string>()
  const datos = useMaestros<FilaMaestro>(tipo, { pagina, tamano, buscar })
  const alBuscar = useCallback((t: string) => { setBuscar(t); setPagina(1) }, [setPagina])
  const esCentro = tipo === 'centros-costo'
  const etiqueta = tipo === 'sedes' ? 'Buscar sede' : tipo === 'areas' ? 'Buscar área' : 'Buscar código, nombre o sede'

  return (
    <section className="tarjeta" aria-label={tipo}>
      <div className="barra-filtros"><Buscador etiqueta={etiqueta} onBuscar={alBuscar} /></div>
      <div className="tabla-envoltura">
        <table className="tabla">
          <thead>
            <tr>
              {esCentro && <th>Código</th>}
              <th>Nombre</th>
              {esCentro && <th>Sede</th>}
              <th style={{ textAlign: 'right' }}>Empleados</th>
              <th>Estado</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {datos.isPending
              ? Array.from({ length: 6 }, (_, i) => (
                  <tr key={i}>
                    {esCentro && <td><Esqueleto ancho={70} /></td>}
                    <td><Esqueleto ancho={`${50 + i * 7}%`} /></td>
                    {esCentro && <td><Esqueleto ancho={80} /></td>}
                    <td><Esqueleto ancho={40} style={{ marginLeft: 'auto' }} /></td>
                    <td><Esqueleto ancho={70} alto={20} redondo /></td>
                    <td><Esqueleto ancho={60} alto={30} /></td>
                  </tr>
                ))
              : datos.data?.items.map((f) =>
                  editando === f.id ? (
                    <FilaEdicion key={f.id} tipo={tipo} fila={f} sedes={resumen.nombresSedes} onCerrar={() => setEditando(undefined)} />
                  ) : (
                    <tr key={f.id} className={f.activo ? '' : 'inactivo'}>
                      {esCentro && <td className="mono">{(f as CentroCosto).codigo}</td>}
                      <td>
                        <b>{f.nombre}</b>
                        {'nombreNomina' in f && f.nombreNomina !== f.nombre && <span style={{ display: 'block', fontSize: 12, color: 'var(--color-texto-tenue)' }}>En la nómina: {f.nombreNomina}</span>}
                      </td>
                      {esCentro && <td style={{ color: 'var(--color-texto-suave)' }}>{(f as CentroCosto).sede}</td>}
                      <td className="num">{numero.format(f.empleados)}</td>
                      <td>
                        <span style={{ display: 'inline-flex', gap: 6 }}>
                          {!f.activo ? <span className="pill pill-neutra">Inactivo</span> : f.empleados === 0 ? <span className="pill pill-alerta">Sin empleados</span> : <span className="pill pill-ok">Activo</span>}
                          {f.editadoManualmente && <span className="pill pill-neutra">Editado</span>}
                        </span>
                      </td>
                      <td className="acciones-fila"><Boton pequeno onClick={() => setEditando(f.id)}>Editar</Boton></td>
                    </tr>
                  ),
                )}
          </tbody>
        </table>
      </div>
      {datos.data && <Paginacion pagina={datos.data} cargando={datos.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
    </section>
  )
}

function FilaEdicion({ tipo, fila, sedes, onCerrar }: { tipo: TipoMaestro; fila: FilaMaestro; sedes: string[]; onCerrar: () => void }) {
  const editar = useEditarMaestro(tipo)
  const esCentro = tipo === 'centros-costo'
  const [nombre, setNombre] = useState(fila.nombre)
  const [activo, setActivo] = useState(fila.activo)
  const [sede, setSede] = useState(esCentro ? (fila as CentroCosto).sede : '')
  const opcionesSede = sede && !sedes.includes(sede) ? [sede, ...sedes] : sedes

  return (
    <tr>
      {esCentro && <td className="mono">{(fila as CentroCosto).codigo}</td>}
      <td><input className="entrada" aria-label="Nombre" value={nombre} onChange={(e) => setNombre(e.target.value)} /></td>
      {esCentro && (
        <td>
          <select className="selector" aria-label="Sede" value={sede} onChange={(e) => setSede(e.target.value)}>
            <option value="">Sin sede</option>
            {opcionesSede.map((s) => <option key={s}>{s}</option>)}
          </select>
        </td>
      )}
      <td className="num">{numero.format(fila.empleados)}</td>
      <td><label className="interruptor" style={{ fontSize: 13 }}><input type="checkbox" checked={activo} onChange={(e) => setActivo(e.target.checked)} />Activo</label></td>
      <td className="acciones-fila">
        <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
          {editar.error && <span role="alert" className="error-formulario">{editar.error.message}</span>}
          <Boton pequeno variante="primario" cargando={editar.isPending} textoCargando="Guardando…" onClick={() => editar.mutate({ id: fila.id, nombre, activo, sede }, { onSuccess: onCerrar })}>
            Guardar
          </Boton>
          <Boton pequeno onClick={onCerrar} disabled={editar.isPending}>Cancelar</Boton>
        </span>
      </td>
    </tr>
  )
}
