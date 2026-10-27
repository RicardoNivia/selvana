import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Buscador, Paginacion } from '../../shared/ui/Paginacion'
import { usePaginacion } from '../../shared/ui/paginado'
import { estadoDe } from './CargoPaginas'
import {
  type CentroCosto, type Maestro, type ResumenEstructura, type TipoMaestro,
  useCargos, useEditarMaestro, useMaestros, useResumenEstructura,
} from './api'

type Pestana = 'cargos' | TipoMaestro

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const numero = new Intl.NumberFormat('es-CO')
const rutaNuevoCargo = '/consola/empresa/estructura/cargos/nuevo'

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
        <p>Suba la nómina; la IA ordena sedes, áreas, centros de costo y cargos, y usted aprueba. También puede agregar un cargo a mano.</p>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, justifyContent: 'center' }}>
          <Link to="/consola/empresa/conectar-nomina" className="boton boton-primario">Conectar nómina</Link>
          <Link to={rutaNuevoCargo} className="boton boton-secundario">Agregar cargo</Link>
        </div>
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
            {r.fuente
              ? <>Viene de la nómina ({r.fuente}{r.actualizadaEn ? `, ${fecha.format(new Date(r.actualizadaEn))}` : ''}). </>
              : <>Aún no se ha conectado la nómina. </>}
            Puede editarla como datos maestros: la siguiente sincronización no pisa lo que usted cambie.
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

      {pestana === 'cargos' ? <Cargos /> :<TablaMaestros key={pestana} tipo={pestana} resumen={r} />}
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
      <div className="tarjeta" style={{ padding: 16 }}>
        {Array.from({ length: 8 }, (_, i) => <Esqueleto key={i} alto={20} ancho={`${90 - i * 4}%`} style={{ margin: '14px 0' }} />)}
      </div>
    </>
  )
}

/* ---------- Cargos ---------- */

const filtrosEstado: [string, string][] = [['', 'Todos'], ['BorradorIa', 'Por revisar'], ['Aprobado', 'Aprobados'], ['SinPerfil', 'Sin perfil']]
const rutaCargo = (id: string) => `/consola/empresa/estructura/cargos/${id}`

function Cargos() {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const [buscar, setBuscar] = useState('')
  const [estado, setEstado] = useState('')
  const cargos = useCargos({ pagina, tamano, buscar, estado })
  const alBuscar = useCallback((t: string) => { setBuscar(t); setPagina(1) }, [setPagina])

  return (
    <section className="tarjeta" aria-label="Cargos">
      <div className="barra-filtros">
        <Buscador etiqueta="Buscar cargo o área" onBuscar={alBuscar} />
        <div className="segmentado" role="group" aria-label="Estado del perfil">
          {filtrosEstado.map(([v, t]) => (
            <button key={v} type="button" aria-pressed={estado === v} onClick={() => { setEstado(v); setPagina(1) }}>{t}</button>
          ))}
        </div>
        <Link to={rutaNuevoCargo} className="boton boton-primario" style={{ marginLeft: 'auto' }}>Agregar cargo</Link>
      </div>
      <div className="tabla-envoltura">
        <table className="tabla">
          <thead>
            <tr>
              <th>Cargo</th>
              <th>Área</th>
              <th style={{ textAlign: 'right' }}>Empleados</th>
              <th>Perfil</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {cargos.isPending
              ? Array.from({ length: 6 }, (_, i) => (
                  <tr key={i}>
                    <td><Esqueleto ancho={`${50 + i * 7}%`} /></td>
                    <td><Esqueleto ancho={90} /></td>
                    <td><Esqueleto ancho={40} style={{ marginLeft: 'auto' }} /></td>
                    <td><Esqueleto ancho={80} alto={20} redondo /></td>
                    <td><Esqueleto ancho={60} alto={30} /></td>
                  </tr>
                ))
              : cargos.data?.items.length === 0
                ? <tr><td colSpan={5} style={{ padding: 32, textAlign: 'center', color: 'var(--color-texto-suave)' }}>No hay cargos con ese filtro.</td></tr>
                : cargos.data?.items.map((c) => {
                    const e = estadoDe(c)
                    return (
                      <tr key={c.id} className={c.activo ? '' : 'inactivo'}>
                        <td><Link to={rutaCargo(c.id)} style={{ fontWeight: 600 }}>{c.nombre}</Link></td>
                        <td style={{ color: 'var(--color-texto-suave)' }}>{c.area}</td>
                        <td className="num">{numero.format(c.empleados)}</td>
                        <td>
                          <span style={{ display: 'inline-flex', gap: 6 }}>
                            <span className={`pill ${e.clase}`}>{e.texto}</span>
                            {c.editadoManualmente && <span className="pill pill-neutra">Editado</span>}
                          </span>
                        </td>
                        <td className="acciones-fila"><Link to={rutaCargo(c.id)} className="boton boton-secundario boton-pequeno">Ver</Link></td>
                      </tr>
                    )
                  })}
          </tbody>
        </table>
      </div>
      {cargos.data && <Paginacion pagina={cargos.data} cargando={cargos.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
    </section>
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
