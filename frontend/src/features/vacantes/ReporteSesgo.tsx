import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoAlerta, IconoEscudo } from '../../shared/ui/Iconos'
import { type GrupoSesgo, useReporteSesgo } from './api'

function Grupos({ titulo, grupos, brecha }: { titulo: string; grupos: GrupoSesgo[]; brecha: number | null }) {
  const max = Math.max(100, ...grupos.map((g) => g.promedio))
  return (
    <section className="tarjeta sesgo-bloque">
      <header><b>{titulo}</b>{brecha !== null && <span className={`mono sesgo-brecha ${brecha > 15 ? 'alta' : ''}`}>Brecha {brecha} pts</span>}</header>
      {grupos.length === 0 ? <p className="hv-vacio">Aún no hay candidatos calificados.</p> : (
        <ul className="sesgo-barras">
          {grupos.map((g) => (
            <li key={g.grupo}>
              <span className="sesgo-grupo">{g.grupo}<small className="mono"> · {g.calificados}</small></span>
              <span className="sesgo-barra" aria-hidden="true"><span style={{ width: `${(g.promedio / max) * 100}%` }} /></span>
              <b className="mono">{g.promedio}</b>
            </li>
          ))}
        </ul>
      )}
      <small>Promedio del puntaje que dio la IA. Los grupos con menos de 3 calificados no se comparan.</small>
    </section>
  )
}

/** Reporte de sesgo de la vacante: lo que se controla por diseño y cómo se reparten los puntajes. */
export function ReporteSesgo({ vacanteId }: { vacanteId: string }) {
  const r = useReporteSesgo(vacanteId)
  if (r.isPending) return <div className="rejilla">{Array.from({ length: 4 }, (_, i) => <div key={i} className="cifra"><Esqueleto alto={12} ancho="60%" /><Esqueleto alto={26} ancho="40%" style={{ marginTop: 8 }} /></div>)}</div>
  if (r.isError) return <p role="alert" className="aviso aviso-error"><IconoAlerta />No pudimos armar el reporte. Intente de nuevo.</p>
  const d = r.data

  return (
    <div className="sesgo">
      <section className="rejilla" aria-label="Resumen">
        <div className="cifra"><span>Calificados por la IA</span><b>{d.calificadas}</b><small>de {d.postulaciones} postulaciones</small></div>
        <div className="cifra"><span>Puntaje promedio de la IA</span><b>{d.promedio ?? '—'}</b><small>sobre 100</small></div>
        <div className="cifra"><span>Ajustes de su equipo</span><b>{d.ajustes}</b><small>{d.diferenciaPromedioAjuste === null ? 'Sin ajustes' : `${d.diferenciaPromedioAjuste > 0 ? '+' : ''}${d.diferenciaPromedioAjuste} pts en promedio`}</small></div>
        <div className="cifra"><span>Aceptación de la terna</span><b>{d.aceptacionTerna === null ? '—' : `${d.aceptacionTerna} %`}</b><small>{d.enTerna} en terna · meta: 7 de cada 10</small></div>
      </section>

      {d.observaciones.length > 0 && (
        <div className="sesgo-observaciones">
          {d.observaciones.map((o) => <p key={o} className="aviso aviso-alerta"><IconoAlerta />{o}</p>)}
        </div>
      )}

      <div className="dos-columnas">
        <section className="tarjeta sesgo-bloque" style={{ flex: '1 1 320px' }}>
          <header><IconoEscudo /><b>Lo que el agente nunca ve</b></header>
          <div className="hv-chips">{d.noVeElAgente.map((x) => <span key={x} className="hv-chip">{x}</span>)}</div>
          <p className="sesgo-texto">
            Califica con un perfil anónimo. {d.razonesRetiradas === 0
              ? 'Ninguna de sus razones mencionó variables protegidas.'
              : `${d.razonesRetiradas} ${d.razonesRetiradas === 1 ? 'razón se retiró' : 'razones se retiraron'} antes de guardarse por mencionar variables protegidas.`}
          </p>
          <p className="sesgo-texto">Ningún descarte, ajuste ni nota de su equipo se puede guardar si el motivo menciona edad, género, estado civil, hijos, apariencia, religión, barrio o estrato.</p>
          <small>Selvana no recoge género ni edad, a propósito: por eso no se mide el impacto por esos grupos. Se mide por ciudad y por canal, que sí se conocen.</small>
        </section>
        <div style={{ flex: '1.4 1 420px', minWidth: 0, display: 'flex', flexDirection: 'column', gap: 'var(--espacio-4)' }}>
          <Grupos titulo="Puntaje por ciudad" grupos={d.porCiudad} brecha={d.brechaCiudad} />
          <Grupos titulo="Puntaje por canal" grupos={d.porCanal} brecha={d.brechaCanal} />
        </div>
      </div>

      {d.descartesPorFiltro && d.descartesPorFiltro.length > 0 && (
        <section className="tarjeta sesgo-bloque">
          <header><b>Descartes aprobados por filtro ATS</b></header>
          <p className="sesgo-texto">Si un filtro descarta a muchos, revise si de verdad es excluyente o si deja por fuera a buenos candidatos.</p>
          <div className="hv-chips">{d.descartesPorFiltro.map((g) => <span key={g.grupo} className="hv-chip">{g.grupo} · <b className="mono">{g.calificados}</b></span>)}</div>
        </section>
      )}

      <section className="tarjeta sesgo-bloque">
        <header><b>Decisiones de su equipo</b></header>
        <p className="sesgo-texto">
          <b className="mono">{d.buenos}</b> marcados como buen candidato · <b className="mono">{d.noEncajan}</b> como «no encaja» · <b className="mono">{d.descartadas}</b> descartados con motivo.
          Estas decisiones calibran al agente de ranking de su empresa en las próximas calificaciones.
        </p>
      </section>
    </div>
  )
}
