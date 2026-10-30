import { useCallback, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa } from '../../shared/ui/Iconos'
import { Buscador, Paginacion } from '../../shared/ui/Paginacion'
import { usePaginacion } from '../../shared/ui/paginado'
import { useEmpresaUsuariaActual } from '../sesion/empresaUsuaria'
import { estadoVacante, useVacantes } from './api'
import type { InicioVacante } from './VacanteNueva'

const filtros: [string, string][] = [['', 'Todas'], ['Borrador', 'Borradores'], ['EnAprobacion', 'En aprobación'], ['Publicada', 'Publicadas'], ['Cerrada', 'Cerradas']]
const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' })

export function VacantesLista() {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const [buscar, setBuscar] = useState('')
  const [estado, setEstado] = useState('')
  const actual = useEmpresaUsuariaActual()
  const vacantes = useVacantes({ pagina, tamano, buscar, estado, empresaUsuariaId: actual.id })
  const alBuscar = useCallback((t: string) => { setBuscar(t); setPagina(1) }, [setPagina])

  return (
    <>
      <header className="encabezado" style={{ display: 'flex', flexWrap: 'wrap', gap: 12, alignItems: 'flex-end', justifyContent: 'space-between' }}>
        <div>
          <h1>Vacantes</h1>
          <p>Escriba lo que necesita y la IA llena la vacante, o agréguela a mano. Cada vacante es de un solo cargo, con las posiciones disponibles que necesite.</p>
        </div>
        <Link to="/consola/vacantes/tablero" className="boton boton-secundario">Tablero de selección</Link>
      </header>

      <NuevaVacante />

      <section className="tarjeta" aria-label="Vacantes">
        <div className="barra-filtros">
          <Buscador etiqueta="Buscar por título, ciudad o código" onBuscar={alBuscar} />
          <div className="segmentado" role="group" aria-label="Estado">
            {filtros.map(([v, t]) => <button key={v} type="button" aria-pressed={estado === v} onClick={() => { setEstado(v); setPagina(1) }}>{t}</button>)}
          </div>
        </div>
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead>
              <tr><th>Vacante</th><th>Empresa</th><th style={{ textAlign: 'right' }}>Cupos</th><th style={{ textAlign: 'right' }}>Postulantes</th><th>Estado</th><th>Creada</th></tr>
            </thead>
            <tbody>
              {vacantes.isPending
                ? Array.from({ length: 5 }, (_, i) => (
                    <tr key={i}>{[60, 40, 20, 20, 30, 30].map((a, j) => <td key={j}><Esqueleto ancho={`${a + ((i * 7) % 20)}%`} /></td>)}</tr>
                  ))
                : vacantes.data?.items.map((v) => (
                    <tr key={v.id}>
                      <td><Link to={`/consola/vacantes/${v.id}`} style={{ fontWeight: 600 }}>{v.titulo || 'Redactando…'}</Link><span className="mono" style={{ display: 'block', fontSize: 12, color: 'var(--color-texto-tenue)' }}>{v.codigo} · {v.ciudad}</span></td>
                      <td style={{ color: 'var(--color-texto-suave)' }}>{v.empresa}</td>
                      <td className="num">{v.cupos || '—'}</td>
                      <td className="num">{v.postulantes}</td>
                      <td><span className={`pill ${estadoVacante[v.estado].clase}`}>{estadoVacante[v.estado].texto}</span></td>
                      <td className="mono" style={{ fontSize: 13, color: 'var(--color-texto-suave)' }}>{fecha.format(new Date(v.creadaEn))}</td>
                    </tr>
                  ))}
            </tbody>
          </table>
        </div>
        {vacantes.data && vacantes.data.total === 0 && <div className="vacio" style={{ padding: 32 }}><p>Todavía no hay vacantes. Cree la primera con la IA o a mano.</p></div>}
        {vacantes.data && <Paginacion pagina={vacantes.data} cargando={vacantes.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
      </section>
    </>
  )
}

/**
 * Barra para empezar una vacante: «Redactar con IA» abre el formulario y la IA lo llena con la frase;
 * «Agregar manualmente» lo abre vacío. El cargo del catálogo y la empresa usuaria se eligen en el formulario.
 */
function NuevaVacante() {
  const navegar = useNavigate()
  const [frase, setFrase] = useState('')

  return (
    <form
      className="tarjeta formulario"
      style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 8, borderColor: 'var(--color-ia-borde)' }}
      onSubmit={(e) => {
        e.preventDefault()
        navegar('/consola/vacantes/nueva', { state: { frase, generar: true } satisfies InicioVacante })
      }}
    >
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, alignItems: 'center' }}>
        <input className="entrada" style={{ flex: '1 1 360px' }} value={frase} onChange={(e) => setFrase(e.target.value)} aria-label="Qué necesita"
          placeholder="Ej.: Necesito 25 auxiliares de bodega para Funza, turno noche, con curso de montacargas" />
        <Boton type="submit" variante="ia" icono={<Chispa />} disabled={!frase.trim()}>Redactar con IA</Boton>
        <Boton variante="primario" onClick={() => navegar('/consola/vacantes/nueva')}>Agregar manualmente</Boton>
      </div>
      <span className="ayuda">Diga el cargo, cuántas posiciones, la ciudad y la jornada. La IA llena el formulario de la vacante y usted lo revisa; si la frase pide varios cargos, le pregunta antes.</span>
    </form>
  )
}
