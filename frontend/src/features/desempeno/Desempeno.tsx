import { type FormEvent, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoAlerta } from '../../shared/ui/Iconos'
import { Paginacion } from '../../shared/ui/Paginacion'
import { usePaginacion } from '../../shared/ui/paginado'
import { type Alcance, type NuevoCiclo, type TipoCiclo, fecha, nombreAlcance, nombreEstadoCiclo, nombreTipo, useCiclos, useCrearCiclo } from './api'
import './desempeno.css'

const claseEstado = { Borrador: 'pill-neutra', Abierto: 'pill-info', Calibracion: 'pill-alerta', Cerrado: 'pill-ok' } as const

/** Desempeño (Etapa 4): ciclos de evaluación con formularios por cargo, objetivos, copiloto, calibración y planes de desarrollo. */
export function Desempeno() {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const ciclos = useCiclos(pagina, tamano)
  const [creando, setCreando] = useState(false)

  return (
    <>
      <header className="encabezado emp-encabezado">
        <div>
          <h1>Desempeño</h1>
          <p>Ciclos de evaluación que se preparan solos: la IA propone el formulario de cada cargo, el copiloto redacta con las evidencias y el líder califica.
            El comité calibra en la matriz 9-box y cada brecha queda con su plan de desarrollo.</p>
        </div>
        {!creando && <Boton variante="primario" onClick={() => setCreando(true)}>Crear ciclo</Boton>}
      </header>

      {creando && <NuevoCicloFormulario onListo={() => setCreando(false)} />}

      <section className="tarjeta" aria-label="Ciclos de evaluación">
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead><tr><th>Ciclo</th><th>Tipo</th><th>Periodo</th><th>Formularios</th><th>Avance</th><th>Estado</th></tr></thead>
            <tbody>
              {ciclos.isPending
                ? Array.from({ length: 3 }, (_, i) => <tr key={i}>{[50, 25, 30, 20, 30, 20].map((a, j) => <td key={j}><Esqueleto ancho={`${a}%`} /></td>)}</tr>)
                : ciclos.data?.items.map((c) => (
                    <tr key={c.id}>
                      <td><Link to={`/consola/desempeno/${c.id}`} style={{ fontWeight: 600 }}>{c.nombre}</Link><span className="emp-sub">{nombreAlcance[c.alcance]}</span></td>
                      <td>{nombreTipo[c.tipo]}</td>
                      <td className="mono" style={{ fontSize: 13 }}>{fecha(c.desde)} – {fecha(c.hasta)}</td>
                      <td className="mono">{c.formulariosAprobados}/{c.formularios}</td>
                      <td>
                        <div className="des-avance" aria-label={`${c.calificadas} de ${c.evaluaciones} calificadas`}>
                          <span style={{ width: `${c.evaluaciones ? (100 * c.calificadas) / c.evaluaciones : 0}%` }} />
                        </div>
                        <span className="emp-sub mono">{c.calificadas} de {c.evaluaciones} calificadas</span>
                      </td>
                      <td><span className={`pill ${claseEstado[c.estado]}`}>{nombreEstadoCiclo[c.estado]}</span></td>
                    </tr>
                  ))}
            </tbody>
          </table>
        </div>
        {ciclos.data && ciclos.data.total === 0 && (
          <div className="vacio" style={{ padding: 32 }}>
            <h2>Aún no hay ciclos</h2>
            <p>Cree el primero: Selvana arma un formulario por cada cargo de su planta activa.</p>
          </div>
        )}
        {ciclos.data && <Paginacion pagina={ciclos.data} cargando={ciclos.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
      </section>
    </>
  )
}

function NuevoCicloFormulario({ onListo }: { onListo: () => void }) {
  const crear = useCrearCiclo()
  const navegar = useNavigate()
  const anio = new Date().getFullYear()
  const [f, setF] = useState<NuevoCiclo>({ nombre: `Evaluación de desempeño ${anio}`, tipo: 'Anual', alcance: 'Grados180', desde: `${anio}-01-01`, hasta: `${anio}-12-31`, pesoObjetivos: 40 })
  const poner = (c: Partial<NuevoCiclo>) => setF((x) => ({ ...x, ...c }))

  const enviar = (e: FormEvent) => {
    e.preventDefault()
    crear.mutate(f, { onSuccess: (r) => navegar(`/consola/desempeno/${r.id}`) })
  }

  return (
    <form className="tarjeta emp-formulario" onSubmit={enviar}>
      <h2>Nuevo ciclo de evaluación</h2>
      <p className="ayuda">Selvana crea un formulario por cada cargo de la planta activa. Después la IA o la plantilla base los llenan y usted los aprueba.</p>
      <div className="des-campos">
        <label className="campo">Nombre<input className="entrada" value={f.nombre} onChange={(e) => poner({ nombre: e.target.value })} required /></label>
        <label className="campo">Tipo
          <select className="selector" value={f.tipo} onChange={(e) => poner({ tipo: e.target.value as TipoCiclo })}>
            {Object.entries(nombreTipo).map(([v, t]) => <option key={v} value={v}>{t}</option>)}
          </select>
        </label>
        <label className="campo">Alcance
          <select className="selector" value={f.alcance} onChange={(e) => poner({ alcance: e.target.value as Alcance })}>
            {Object.entries(nombreAlcance).map(([v, t]) => <option key={v} value={v}>{t}</option>)}
          </select>
        </label>
        <label className="campo">Desde<input className="entrada mono" type="date" value={f.desde} onChange={(e) => poner({ desde: e.target.value })} required /></label>
        <label className="campo">Hasta<input className="entrada mono" type="date" value={f.hasta} onChange={(e) => poner({ hasta: e.target.value })} required /></label>
        <label className="campo">Peso de los objetivos
          <span className="des-peso"><input type="range" min={0} max={100} step={5} value={f.pesoObjetivos} onChange={(e) => poner({ pesoObjetivos: Number(e.target.value) })} />
            <span className="mono">{f.pesoObjetivos} % objetivos · {100 - f.pesoObjetivos} % competencias</span></span>
        </label>
      </div>
      {crear.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{crear.error.message}</p>}
      <div className="acciones-formulario">
        <Boton onClick={onListo} disabled={crear.isPending}>Cancelar</Boton>
        <Boton type="submit" variante="primario" cargando={crear.isPending} textoCargando="Creando…">Crear ciclo</Boton>
      </div>
    </form>
  )
}
