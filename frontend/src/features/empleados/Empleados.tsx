import { useCallback, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoAlerta } from '../../shared/ui/Iconos'
import { Buscador, Paginacion } from '../../shared/ui/Paginacion'
import { usePaginacion } from '../../shared/ui/paginado'
import { useTiposContrato } from '../vinculacion/api'
import { type Afiliaciones, fechaCorta, type NuevoEmpleado, pesos, type Situacion, useCrearEmpleado, useEmpleados } from './api'
import './empleados.css'

/** Empleados: el expediente vivo de la planta. Llegan solos desde la vinculación o se crean a mano. */
export function Empleados() {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const [buscar, setBuscar] = useState('')
  const [estado, setEstado] = useState('Activo')
  const [alertas, setAlertas] = useState(false)
  const [creando, setCreando] = useState(false)
  const empleados = useEmpleados({ pagina, tamano, buscar, estado, alertas })
  const alBuscar = useCallback((t: string) => { setBuscar(t); setPagina(1) }, [setPagina])
  const c = empleados.data?.conteo

  return (
    <>
      <header className="encabezado emp-encabezado">
        <div>
          <h1>Empleados</h1>
          <p>El expediente de cada persona: contrato, cargo, salario, afiliaciones, historial de novedades y carpeta de documentos con vencimientos. Llegan solos al cerrar la vinculación; la planta que ya existía se crea a mano.</p>
        </div>
        {!creando && <Boton variante="primario" onClick={() => setCreando(true)}>Crear empleado</Boton>}
      </header>

      {creando && <NuevoEmpleadoFormulario onListo={() => setCreando(false)} />}

      <div className="emp-cifras">
        <button type="button" className={`emp-cifra ${estado === 'Activo' && !alertas ? 'activa' : ''}`} onClick={() => { setEstado('Activo'); setAlertas(false); setPagina(1) }}>
          {c ? <b>{c.activos}</b> : <Esqueleto alto={28} ancho={40} />}<span>activos</span>
        </button>
        <button type="button" className={`emp-cifra alerta ${alertas ? 'activa' : ''}`} onClick={() => { setEstado('Activo'); setAlertas(true); setPagina(1) }}>
          {c ? <b>{c.conAlertas}</b> : <Esqueleto alto={28} ancho={40} />}<span>con algo por atender</span>
        </button>
        <button type="button" className={`emp-cifra ${estado === 'Retirado' ? 'activa' : ''}`} onClick={() => { setEstado('Retirado'); setAlertas(false); setPagina(1) }}>
          {c ? <b>{c.retirados}</b> : <Esqueleto alto={28} ancho={40} />}<span>retirados</span>
        </button>
      </div>

      <section className="tarjeta" aria-label="Empleados">
        <div className="barra-filtros">
          <Buscador etiqueta="Buscar por nombre, documento, cargo, área o sede" onBuscar={alBuscar} />
        </div>
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead>
              <tr><th>Persona</th><th>Cargo</th><th>Sede</th><th>Contrato</th><th style={{ textAlign: 'right' }}>Salario</th><th>Ingreso</th><th>Atender</th></tr>
            </thead>
            <tbody>
              {empleados.isPending
                ? Array.from({ length: 5 }, (_, i) => <tr key={i}>{[60, 50, 30, 35, 25, 25, 15].map((a, j) => <td key={j}><Esqueleto ancho={`${a}%`} /></td>)}</tr>)
                : empleados.data?.empleados.items.map((e) => (
                    <tr key={e.id}>
                      <td><Link to={`/consola/empleados/${e.id}`} style={{ fontWeight: 600 }}>{e.nombre}</Link><span className="emp-sub mono">{e.codigo} · CC {e.documento}</span></td>
                      <td>{e.cargo}{e.area && <span className="emp-sub">{e.area}</span>}</td>
                      <td>{e.sede || '—'}{e.empresaUsuaria && <span className="emp-sub">{e.empresaUsuaria}</span>}</td>
                      <td style={{ color: 'var(--color-texto-suave)' }}>{e.tipoContrato}</td>
                      <td className="num">{pesos.format(e.salarioCop)}</td>
                      <td className="mono" style={{ fontSize: 13 }}>{fechaCorta(e.fechaIngreso)}</td>
                      <td>{e.estado === 'Retirado' ? <span className="pill pill-neutra">Retirado</span>
                        : e.alertas > 0 ? <span className="pill pill-alerta"><IconoAlerta />{e.alertas}</span> : <span className="pill pill-ok">Al día</span>}</td>
                    </tr>
                  ))}
            </tbody>
          </table>
        </div>
        {empleados.data && empleados.data.empleados.total === 0 && (
          <div className="vacio" style={{ padding: 32 }}>
            <p>{buscar ? 'No encontramos empleados con esa búsqueda.' : alertas ? 'Nadie tiene documentos por vencer ni afiliaciones pendientes.' : 'Aún no hay empleados aquí. Llegan al cerrar una vinculación, o créelos a mano con «Crear empleado».'}</p>
          </div>
        )}
        {empleados.data && <Paginacion pagina={empleados.data.empleados} cargando={empleados.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
      </section>
    </>
  )
}

const situacionVacia: Situacion = { cargo: '', area: '', centroCosto: '', sede: '', empresaUsuariaId: null, empresaUsuaria: null, tipoContrato: '', salarioCop: 0, fechaFinContrato: null }
const afiliacionesVacias: Afiliaciones = { eps: '', fondoPension: '', arl: '', claseRiesgoArl: null, cajaCompensacion: '' }

/** Camino manual: crear un empleado que no pasó por la vinculación (la planta que ya existía). */
function NuevoEmpleadoFormulario({ onListo }: { onListo: () => void }) {
  const crear = useCrearEmpleado()
  const navegar = useNavigate()
  const [f, setF] = useState<NuevoEmpleado>(() => ({
    documento: '', nombre: '', correo: '', celular: '', ciudad: '', situacion: situacionVacia, fechaIngreso: new Date().toISOString().slice(0, 10), afiliaciones: afiliacionesVacias,
  }))
  const poner = (c: Partial<NuevoEmpleado>) => setF((x) => ({ ...x, ...c }))

  return (
    <form className="tarjeta emp-formulario formulario" onSubmit={(e) => { e.preventDefault(); crear.mutate(f, { onSuccess: (r) => navegar(`/consola/empleados/${r.id}`) }) }}>
      <h2>Crear empleado</h2>
      <h3>Datos de la persona</h3>
      <div className="emp-campos">
        <label>Documento<input className="entrada mono" value={f.documento} onChange={(e) => poner({ documento: e.target.value })} required /></label>
        <label>Nombre completo<input className="entrada" value={f.nombre} onChange={(e) => poner({ nombre: e.target.value })} required /></label>
        <label>Correo<input className="entrada" type="email" value={f.correo} onChange={(e) => poner({ correo: e.target.value })} /></label>
        <label>Celular<input className="entrada mono" inputMode="tel" value={f.celular} onChange={(e) => poner({ celular: e.target.value })} /></label>
        <label>Ciudad<input className="entrada" value={f.ciudad} onChange={(e) => poner({ ciudad: e.target.value })} /></label>
        <label>Fecha de ingreso<input className="entrada" type="date" value={f.fechaIngreso} onChange={(e) => poner({ fechaIngreso: e.target.value })} required /></label>
      </div>
      <h3>Situación laboral</h3>
      <CamposSituacion valor={f.situacion} onCambiar={(s) => poner({ situacion: s })} />
      <h3>Afiliaciones</h3>
      <CamposAfiliaciones valor={f.afiliaciones} onCambiar={(a) => poner({ afiliaciones: a })} />
      {crear.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{crear.error.message}</p>}
      <div className="vin-acciones emp-acciones">
        <Boton onClick={onListo} disabled={crear.isPending}>Cancelar</Boton>
        <Boton type="submit" variante="primario" cargando={crear.isPending} textoCargando="Creando…">Crear empleado</Boton>
      </div>
    </form>
  )
}

export function CamposSituacion({ valor, onCambiar }: { valor: Situacion; onCambiar: (s: Situacion) => void }) {
  const tipos = useTiposContrato(true)
  const poner = (c: Partial<Situacion>) => onCambiar({ ...valor, ...c })
  return (
    <div className="emp-campos">
      <label>Cargo<input className="entrada" value={valor.cargo} onChange={(e) => poner({ cargo: e.target.value })} required /></label>
      <label>Área<input className="entrada" value={valor.area} onChange={(e) => poner({ area: e.target.value })} /></label>
      <label>Centro de costo<input className="entrada" value={valor.centroCosto} onChange={(e) => poner({ centroCosto: e.target.value })} /></label>
      <label>Sede<input className="entrada" value={valor.sede} onChange={(e) => poner({ sede: e.target.value })} /></label>
      <label>Tipo de contrato
        <input className="entrada" list="emp-tipos-contrato" value={valor.tipoContrato} onChange={(e) => poner({ tipoContrato: e.target.value })} required />
        <datalist id="emp-tipos-contrato">{(tipos.data ?? []).map((t) => <option key={t.id} value={t.nombre} />)}</datalist>
      </label>
      <label>Salario mensual
        <input className="entrada mono" inputMode="numeric" value={valor.salarioCop ? pesos.format(valor.salarioCop) : ''}
          onChange={(e) => poner({ salarioCop: Number(e.target.value.replace(/\D/g, '')) || 0 })} required />
      </label>
      <label>Fin del contrato <span className="ayuda">si es a término fijo</span>
        <input className="entrada" type="date" value={valor.fechaFinContrato ?? ''} onChange={(e) => poner({ fechaFinContrato: e.target.value || null })} />
      </label>
    </div>
  )
}

export function CamposAfiliaciones({ valor, onCambiar }: { valor: Afiliaciones; onCambiar: (a: Afiliaciones) => void }) {
  const poner = (c: Partial<Afiliaciones>) => onCambiar({ ...valor, ...c })
  return (
    <div className="emp-campos">
      <label>EPS<input className="entrada" value={valor.eps} onChange={(e) => poner({ eps: e.target.value })} /></label>
      <label>Fondo de pensión<input className="entrada" value={valor.fondoPension} onChange={(e) => poner({ fondoPension: e.target.value })} /></label>
      <label>ARL<input className="entrada" value={valor.arl} onChange={(e) => poner({ arl: e.target.value })} /></label>
      <label>Clase de riesgo
        <select className="selector" value={valor.claseRiesgoArl ?? ''} onChange={(e) => poner({ claseRiesgoArl: e.target.value ? Number(e.target.value) : null })}>
          <option value="">Sin definir</option>
          {[1, 2, 3, 4, 5].map((n) => <option key={n} value={n}>Clase {n}</option>)}
        </select>
      </label>
      <label>Caja de compensación<input className="entrada" value={valor.cajaCompensacion} onChange={(e) => poner({ cajaCompensacion: e.target.value })} /></label>
    </div>
  )
}
