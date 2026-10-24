import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Buscador, Paginacion } from '../../shared/ui/Paginacion'
import { usePaginacion } from '../../shared/ui/paginado'
import { type EtapaVinculacion, estadosOferta, etapas, fechaCorta, pesos, useProcesos } from './api'
import './vinculacion.css'

const filtros: EtapaVinculacion[] = ['Oferta', 'Documentos', 'Afiliaciones', 'Onboarding', 'Vinculado', 'Cancelado']

/** Procesos de vinculación: del candidato aprobado al empleado creado. */
export function ProcesosVinculacion() {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const [buscar, setBuscar] = useState('')
  const [etapa, setEtapa] = useState('')
  const procesos = useProcesos({ pagina, tamano, buscar, etapa })
  const alBuscar = useCallback((t: string) => { setBuscar(t); setPagina(1) }, [setPagina])
  const conteo = procesos.data?.porEtapa ?? {}

  return (
    <>
      <header className="encabezado">
        <h1>Vinculación</h1>
        <p>Cuando aprueba contratar a un candidato de la terna, la IA redacta la oferta y el contrato; usted los aprueba y el candidato acepta por su enlace. Luego siguen documentos, afiliaciones y onboarding.</p>
      </header>

      <section className="tarjeta" aria-label="Procesos de vinculación">
        <div className="barra-filtros">
          <Buscador etiqueta="Buscar por nombre, documento, cargo o código" onBuscar={alBuscar} />
          <div className="segmentado" role="group" aria-label="Etapa">
            <button type="button" aria-pressed={etapa === ''} onClick={() => { setEtapa(''); setPagina(1) }}>Todos</button>
            {filtros.map((e) => (
              <button key={e} type="button" aria-pressed={etapa === e} onClick={() => { setEtapa(e); setPagina(1) }}>
                {etapas[e]}{conteo[e] ? <span className="mono"> · {conteo[e]}</span> : null}
              </button>
            ))}
          </div>
        </div>
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead>
              <tr><th>Persona</th><th>Cargo</th><th>Contrato</th><th style={{ textAlign: 'right' }}>Salario</th><th>Ingreso</th><th>Etapa</th><th>Oferta</th></tr>
            </thead>
            <tbody>
              {procesos.isPending
                ? Array.from({ length: 5 }, (_, i) => <tr key={i}>{[60, 50, 40, 30, 25, 25, 35].map((a, j) => <td key={j}><Esqueleto ancho={`${a}%`} /></td>)}</tr>)
                : procesos.data?.procesos.items.map((p) => (
                    <tr key={p.id}>
                      <td><Link to={`/consola/vinculacion/${p.id}`} style={{ fontWeight: 600 }}>{p.nombre}</Link><span className="vin-sub mono">{p.codigo} · CC {p.documento}</span></td>
                      <td>{p.cargo}{p.empresaUsuaria && <span className="vin-sub">{p.empresaUsuaria}</span>}</td>
                      <td style={{ color: 'var(--color-texto-suave)' }}>{p.tipoContrato}</td>
                      <td className="num">{pesos.format(p.salarioCop)}</td>
                      <td className="mono" style={{ fontSize: 13 }}>{fechaCorta(p.fechaIngreso)}</td>
                      <td><span className="pill pill-neutra">{etapas[p.etapa]}</span></td>
                      <td><span className={`pill ${estadosOferta[p.estadoOferta].clase}`}>{estadosOferta[p.estadoOferta].texto}</span></td>
                    </tr>
                  ))}
            </tbody>
          </table>
        </div>
        {procesos.data && procesos.data.procesos.total === 0 && (
          <div className="vacio" style={{ padding: 32 }}><p>Aún no hay procesos. Abra una vacante, vaya a la columna Terna del tablero y apruebe la contratación de un candidato.</p></div>
        )}
        {procesos.data && <Paginacion pagina={procesos.data.procesos} cargando={procesos.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
      </section>
    </>
  )
}
