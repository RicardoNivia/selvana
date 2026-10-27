import { useCallback, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa } from '../../shared/ui/Iconos'
import { Buscador, Paginacion } from '../../shared/ui/Paginacion'
import { usePaginacion } from '../../shared/ui/paginado'
import { useEmpresaUsuariaActual } from '../sesion/empresaUsuaria'
import { useSesion } from '../sesion/api'
import { ElegirCargo } from './CargoDelCatalogo'
import { type CargoCatalogo, estadoVacante, useEmpresaActual, useVacantes } from './api'
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
      <header className="encabezado">
        <h1>Vacantes</h1>
        <p>Describa lo que necesita en una frase. El agente redacta el aviso, los requisitos, las preguntas filtro y el rango salarial; usted revisa y publica.</p>
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
        {vacantes.data && vacantes.data.total === 0 && <div className="vacio" style={{ padding: 32 }}><p>Todavía no hay vacantes. Cree la primera con una frase.</p></div>}
        {vacantes.data && <Paginacion pagina={vacantes.data} cargando={vacantes.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
      </section>
    </>
  )
}

/** Barra para empezar una vacante: con la frase, «Redactar con IA» abre el formulario y la IA lo llena; «Agregar manualmente» lo abre vacío. */
function NuevaVacante() {
  const empresa = useEmpresaActual()
  const navegar = useNavigate()
  const [frase, setFrase] = useState('')
  const [cargo, setCargo] = useState<CargoCatalogo | null>(null)
  const { data: sesion } = useSesion()
  const actual = useEmpresaUsuariaActual()
  // Arranca en la empresa usuaria elegida en la barra lateral; solo ofrece las que la persona ve
  const [usuaria, setUsuaria] = useState(actual.id)
  const visibles = new Set(sesion?.empresasUsuarias.map((e) => e.id))
  const usuarias = empresa.data?.empresasUsuarias.filter((e) => e.activa && visibles.has(e.id)) ?? []

  return (
    <form
      className="tarjeta formulario"
      style={{ padding: 20, borderColor: 'var(--color-ia-borde)' }}
      onSubmit={(e) => {
        e.preventDefault()
        navegar('/consola/vacantes/nueva', { state: { frase, cargo, empresaUsuariaId: usuaria, generar: true } satisfies InicioVacante })
      }}
    >
      <label style={{ color: 'var(--color-ia)' }}>
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}><Chispa width={16} height={16} />Nueva vacante</span>
        <textarea className="area-texto" rows={2} required value={frase} onChange={(e) => setFrase(e.target.value)}
          placeholder="Ej.: Necesito 25 auxiliares de bodega para Funza, turno noche, con curso de montacargas" style={{ fontSize: 16 }} />
      </label>
      <ElegirCargo elegido={cargo} onElegir={setCargo} />
      <span className="ayuda">{cargo ? 'La IA parte del perfil del cargo y la frase lo ajusta (ciudad, cupos, jornada).' : 'Sin cargo del catálogo, la IA redacta la vacante desde cero.'}</span>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, alignItems: 'flex-end' }}>
        {empresa.data?.esTemporal && (
          <label style={{ flex: '1 1 260px' }}>
            Empresa usuaria
            <select className="selector" value={usuaria} onChange={(e) => setUsuaria(e.target.value)}>
              <option value="">{empresa.data.nombre} (vacante propia)</option>
              {usuarias.map((u) => <option key={u.id} value={u.id}>{u.nombre} · {u.ciudad}</option>)}
            </select>
          </label>
        )}
        <Boton type="submit" variante="ia" icono={<Chispa />} disabled={!frase.trim()}>
          Redactar con IA
        </Boton>
        <Boton onClick={() => navegar('/consola/vacantes/nueva', { state: { cargo, empresaUsuariaId: usuaria } satisfies InicioVacante })}>
          Agregar manualmente
        </Boton>
      </div>
    </form>
  )
}
