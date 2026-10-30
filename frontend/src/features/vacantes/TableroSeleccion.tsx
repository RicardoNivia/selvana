import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { type CSSProperties, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiGet } from '../../shared/api/client'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoAlerta } from '../../shared/ui/Iconos'
import { type Pagina, Paginacion } from '../../shared/ui/Paginacion'
import { usePaginacion } from '../../shared/ui/paginado'
import type { Etapa } from './api'
import { nombreEtapa } from './procesoApi'
import './proceso.css'
import './tablero.css'

interface TableroSeleccion {
  indicadores: {
    vacantesAbiertas: number; vacantesEnPreparacion: number; vacantesCerradas: number; postulaciones: number; enProceso: number; enTerna: number
    contratados: number; diasPromedioATerna: number | null; costoIaUsd: number; costoIaPorContratadoUsd: number | null
  }
  tiempos: { etapa: Etapa; diasPromedio: number | null; muestras: number; hoy: number }[]
  fuentes: { canal: string; postulaciones: number; preseleccionadas: number; enTerna: number; contratados: number }[]
  vacantes: Pagina<{
    id: string; codigo: string; titulo: string; ciudad: string; estado: string; diasAbierta: number; cupos: number; postulaciones: number; enProceso: number
    enTerna: number; contratados: number; diasPromedioATerna: number | null; costoIaUsd: number; fuentePrincipal: string | null
  }>
}

const periodos: [number | null, string][] = [[30, '30 días'], [90, '90 días'], [365, 'Un año'], [null, 'Todo']]
const estados: [string, string][] = [['Abiertas', 'Abiertas'], ['Cerradas', 'Cerradas'], ['Todas', 'Todas']]
const usd = (n: number) => `US$ ${n.toLocaleString('es-CO', { minimumFractionDigits: 2, maximumFractionDigits: n < 1 ? 4 : 2 })}`
const dias = (n: number | null) => (n === null ? '—' : `${n.toLocaleString('es-CO', { maximumFractionDigits: 1 })} d`)
const pct = (a: number, b: number) => (b === 0 ? 0 : Math.round((100 * a) / b))
const nombreCanal: Record<string, string> = { Portal: 'Portal de empleo', LinkedIn: 'LinkedIn', Computrabajo: 'Computrabajo', Referido: 'Referidos', Otro: 'Otro', Banco: 'Banco de talento' }

function useTableroSeleccion(f: { dias: number | null; estado: string; pagina: number; tamano: number }) {
  const q = new URLSearchParams({ estado: f.estado, pagina: String(f.pagina), tamano: String(f.tamano) })
  if (f.dias) q.set('dias', String(f.dias))
  return useQuery({
    queryKey: ['vacantes', 'tablero-seleccion', f],
    queryFn: ({ signal }) => apiGet<TableroSeleccion>(`/api/v1/reclutamiento/tablero-seleccion?${q}`, signal),
    placeholderData: keepPreviousData,
  })
}

/** Sprint 10 · Tablero de selección: cuántas vacantes hay abiertas, cuánto se demora cada etapa, de dónde llega la gente y cuánto cuesta la IA. */
export function TableroSeleccion() {
  const [periodo, setPeriodo] = useState<number | null>(90)
  const [estado, setEstado] = useState('Abiertas')
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const tablero = useTableroSeleccion({ dias: periodo, estado, pagina, tamano })

  return (
    <div className="tsel">
      <header className="encabezado tsel-cabeza">
        <div>
          <Link to="/consola/vacantes" style={{ fontSize: 13 }}>← Vacantes</Link>
          <h1>Tablero de selección</h1>
          <p>Cómo va la selección: vacantes abiertas, cuánto se demora cada etapa, qué fuentes traen a quienes llegan lejos y cuánto cuesta la IA.</p>
        </div>
        <div className="segmentado" role="group" aria-label="Periodo">
          {periodos.map(([v, t]) => <button key={t} type="button" aria-pressed={periodo === v} onClick={() => { setPeriodo(v); setPagina(1) }}>{t}</button>)}
        </div>
      </header>

      {tablero.isPending ? <EsqueletoTablero /> : tablero.isError ? <p role="alert" className="aviso aviso-error"><IconoAlerta />{tablero.error.message}</p> : (
        <Contenido d={tablero.data} estado={estado} cargando={tablero.isPlaceholderData}
          onEstado={(e) => { setEstado(e); setPagina(1) }} onPagina={setPagina} onTamano={setTamano} />
      )}
    </div>
  )
}

function Contenido({ d, estado, cargando, onEstado, onPagina, onTamano }: {
  d: TableroSeleccion; estado: string; cargando: boolean; onEstado: (e: string) => void; onPagina: (n: number) => void; onTamano: (n: number) => void
}) {
  const i = d.indicadores
  const cifras: [string, string, string?][] = [
    ['Vacantes abiertas', String(i.vacantesAbiertas), `${i.vacantesEnPreparacion} en preparación · ${i.vacantesCerradas} cerradas`],
    ['Postulaciones', i.postulaciones.toLocaleString('es-CO'), `${i.enProceso} en proceso`],
    ['En terna', String(i.enTerna), `${i.contratados} contratados`],
    ['Días hasta la terna', dias(i.diasPromedioATerna), 'Promedio desde que llega'],
    ['Costo de IA', usd(i.costoIaUsd), i.costoIaPorContratadoUsd !== null ? `${usd(i.costoIaPorContratadoUsd)} por contratado` : 'Sin contratados en el periodo'],
  ]
  const maxDias = Math.max(1, ...d.tiempos.map((t) => t.diasPromedio ?? 0))
  const maxFuente = Math.max(1, ...d.fuentes.map((f) => f.postulaciones))

  return (
    <>
      <section className="tsel-cifras" aria-label="Indicadores">
        {cifras.map(([t, v, s], n) => <div key={t} className="tsel-cifra" style={{ '--i': n } as CSSProperties}><span>{t}</span><b>{v}</b>{s && <small>{s}</small>}</div>)}
      </section>

      <div className="tsel-grilla">
        <section className="tarjeta tsel-bloque" aria-label="Tiempo por etapa">
          <h2>Tiempo por etapa</h2>
          <p className="ayuda">Días promedio que pasa una persona en cada etapa antes de seguir o salir; entre paréntesis, cuántas pasaron por ella.</p>
          <ul className="tsel-barras">
            {d.tiempos.map((t) => (
              <li key={t.etapa} className={`etapa-${t.etapa.toLowerCase()}`} title={`${nombreEtapa[t.etapa]}: ${dias(t.diasPromedio)} de promedio (${t.muestras} personas) · hoy hay ${t.hoy}`}>
                <span className="tsel-barra-nombre">{nombreEtapa[t.etapa]}</span>
                <span className="tsel-barra"><span style={{ width: `${((t.diasPromedio ?? 0) / maxDias) * 100}%` }} /></span>
                <span className="tsel-barra-valor mono">{dias(t.diasPromedio)} <small>({t.muestras})</small></span>
                <span className="tsel-barra-hoy">{t.hoy} hoy</span>
              </li>
            ))}
          </ul>
        </section>

        <section className="tarjeta tsel-bloque" aria-label="Fuentes">
          <h2>Fuentes</h2>
          <p className="ayuda">De dónde llegan las postulaciones del periodo y qué tan lejos avanzan.</p>
          {d.fuentes.length === 0 ? <p className="ayuda">Sin postulaciones en el periodo.</p> : (
            <table className="tabla tsel-fuentes">
              <thead><tr><th>Fuente</th><th>Postulaciones</th><th className="num">Preselección</th><th className="num">Terna</th><th className="num">Contratados</th></tr></thead>
              <tbody>
                {d.fuentes.map((f) => (
                  <tr key={f.canal} title={`${nombreCanal[f.canal] ?? f.canal}: ${f.postulaciones} postulaciones, ${pct(f.enTerna, f.postulaciones)} % llegó a la terna`}>
                    <td>{nombreCanal[f.canal] ?? f.canal}</td>
                    <td><span className="tsel-fuente"><span className="tsel-barra"><span style={{ width: `${(f.postulaciones / maxFuente) * 100}%` }} /></span><span className="mono">{f.postulaciones}</span></span></td>
                    <td className="num mono">{pct(f.preseleccionadas, f.postulaciones)} %</td>
                    <td className="num mono">{pct(f.enTerna, f.postulaciones)} %</td>
                    <td className="num mono">{f.contratados}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      </div>

      <section className="tarjeta" aria-label="Vacantes">
        <div className="barra-filtros">
          <h2 className="tsel-titulo">Por vacante</h2>
          <div className="segmentado" role="group" aria-label="Estado">
            {estados.map(([v, t]) => <button key={v} type="button" aria-pressed={estado === v} onClick={() => onEstado(v)}>{t}</button>)}
          </div>
        </div>
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead>
              <tr><th>Vacante</th><th className="num">Días abierta</th><th className="num">Postulaciones</th><th className="num">En proceso</th><th className="num">Terna</th>
                <th className="num">Contratados</th><th className="num">Días a terna</th><th>Fuente principal</th><th className="num">Costo de IA</th></tr>
            </thead>
            <tbody>
              {d.vacantes.items.map((v) => (
                <tr key={v.id}>
                  <td><Link to={`/consola/vacantes/${v.id}?pestana=proceso`} style={{ fontWeight: 600 }}>{v.titulo || 'Sin título'}</Link>
                    <span className="mono" style={{ display: 'block', fontSize: 12, color: 'var(--color-texto-tenue)' }}>{v.codigo} · {v.ciudad} · {v.cupos} {v.cupos === 1 ? 'cupo' : 'cupos'}</span></td>
                  <td className="num mono">{v.diasAbierta}</td>
                  <td className="num mono">{v.postulaciones}</td>
                  <td className="num mono">{v.enProceso}</td>
                  <td className="num mono">{v.enTerna}</td>
                  <td className="num mono">{v.contratados}</td>
                  <td className="num mono">{dias(v.diasPromedioATerna)}</td>
                  <td>{v.fuentePrincipal ? nombreCanal[v.fuentePrincipal] ?? v.fuentePrincipal : '—'}</td>
                  <td className="num mono">{usd(v.costoIaUsd)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {d.vacantes.total === 0 && <div className="vacio" style={{ padding: 32 }}><p>No hay vacantes {estado === 'Abiertas' ? 'abiertas' : estado === 'Cerradas' ? 'cerradas' : ''}.</p></div>}
        <Paginacion pagina={d.vacantes} cargando={cargando} onCambiar={onPagina} onCambiarTamano={onTamano} />
      </section>
      <p className="ayuda">El costo de IA suma la redacción de la vacante, el ranking de cada postulación y la preselección conversacional. Los datos de ejemplo no usan IA.</p>
    </>
  )
}

function EsqueletoTablero() {
  return (
    <>
      <div className="tsel-cifras">{Array.from({ length: 5 }, (_, i) => <Esqueleto key={i} alto={92} />)}</div>
      <div className="tsel-grilla"><Esqueleto alto={240} /><Esqueleto alto={240} /></div>
      <div className="tarjeta" style={{ padding: 16, display: 'grid', gap: 10 }}>{Array.from({ length: 5 }, (_, i) => <Esqueleto key={i} alto={40} />)}</div>
    </>
  )
}
