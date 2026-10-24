import { type DragEvent, useState } from 'react'
import { Link } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoCheck } from '../../shared/ui/Iconos'
import { usePuede } from '../sesion/api'
import { AprobarContratacion } from '../vinculacion/AprobarContratacion'
import './filtros.css'
import { type Etapa, type Tarjeta, type Vacante, useMoverPostulacion, useProponerTerna, useTablero } from './api'

const columnas: { etapa: Etapa; titulo: string; ayuda: string }[] = [
  { etapa: 'Recibida', titulo: 'Recibidas', ayuda: 'Llegaron por el portal' },
  { etapa: 'Preseleccion', titulo: 'Preselección', ayuda: 'Cumplen lo básico' },
  { etapa: 'Pruebas', titulo: 'Pruebas', ayuda: 'Presentan las pruebas' },
  { etapa: 'Terna', titulo: 'Terna', ayuda: 'Aprobada por una persona' },
  { etapa: 'Descartada', titulo: 'Descartadas', ayuda: 'Con motivo registrado' },
]
const nombre = Object.fromEntries(columnas.map((c) => [c.etapa, c.titulo])) as Record<Etapa, string>

/** Tablero de la vacante: una persona mueve a cada candidato; la IA solo propone la terna, que se aprueba en la bandeja. */
export function TableroVacante({ vacante }: { vacante: Vacante }) {
  const [porColumna, setPorColumna] = useState(20)
  const tablero = useTablero(vacante.id, porColumna)
  const mover = useMoverPostulacion()
  const proponer = useProponerTerna(vacante.id)
  const [descartando, setDescartando] = useState<Tarjeta>()
  const [motivo, setMotivo] = useState('')
  const [sobre, setSobre] = useState<Etapa>()
  const [contratando, setContratando] = useState<string>()
  const apruebaContratacion = usePuede('AprobarBandeja')

  const moverA = (t: Tarjeta, etapa: Etapa) => {
    if (etapa === 'Descartada') {
      setDescartando(t)
      setMotivo('')
      return
    }
    mover.mutate({ postulacionId: t.postulacionId, etapa, motivo: null })
  }
  const soltar = (e: DragEvent, etapa: Etapa) => {
    e.preventDefault()
    setSobre(undefined)
    const id = e.dataTransfer.getData('text/plain')
    const t = tablero.data?.columnas.flatMap((c) => c.tarjetas).find((x) => x.postulacionId === id)
    if (t) moverA(t, etapa)
  }

  return (
    <section className="tablero-vacante" aria-label="Tablero de la vacante">
      <div className="tablero-barra">
        <p>Arrastre a cada candidato a su etapa o use «Mover a». Descartar pide el motivo y todo queda en la auditoría.</p>
        <Boton variante="ia" icono={<Chispa />} cargando={proponer.isPending} textoCargando="Armando la terna…" onClick={() => proponer.mutate()}>Proponer terna con IA</Boton>
      </div>
      {proponer.data && (
        <p className="aviso aviso-ok" role="status"><IconoCheck />La terna propuesta ({proponer.data.candidatos} {proponer.data.candidatos === 1 ? 'candidato' : 'candidatos'}) quedó en la <Link to="/consola/bandeja">bandeja</Link>: una persona con permiso la aprueba y pasan a la columna Terna.</p>
      )}
      {(proponer.error ?? mover.error) && <p role="alert" className="aviso aviso-error"><IconoAlerta />{(proponer.error ?? mover.error)!.message}</p>}

      {descartando && (
        <form className="tarjeta tablero-descarte panel-decision" onSubmit={(e) => {
          e.preventDefault()
          mover.mutate({ postulacionId: descartando.postulacionId, etapa: 'Descartada', motivo: motivo.trim() }, { onSuccess: () => setDescartando(undefined) })
        }}>
          <b>Descartar a {descartando.nombre}</b>
          <label>¿Por qué lo descarta?<input className="entrada" value={motivo} onChange={(e) => setMotivo(e.target.value)} placeholder="No tiene la licencia C1 que pide la vacante" autoFocus /></label>
          <div className="decision" style={{ padding: 0, borderTop: 0 }}>
            <Boton type="submit" variante="primario" disabled={!motivo.trim()} cargando={mover.isPending} textoCargando="Descartando…">Descartar con motivo</Boton>
            <Boton onClick={() => setDescartando(undefined)}>Cancelar</Boton>
            <span className="registro">Se registran la sugerencia y tu decisión</span>
          </div>
        </form>
      )}

      {contratando && <AprobarContratacion postulacionId={contratando} onCerrar={() => setContratando(undefined)} />}

      <div className="tablero-columnas">
        {columnas.map((col) => {
          const datos = tablero.data?.columnas.find((c) => c.etapa === col.etapa)
          return (
            <div key={col.etapa} className={`tablero-columna ${sobre === col.etapa ? 'sobre' : ''} ${col.etapa === 'Descartada' ? 'descartadas' : ''}`}
              onDragOver={(e) => { e.preventDefault(); setSobre(col.etapa) }} onDragLeave={() => setSobre(undefined)} onDrop={(e) => soltar(e, col.etapa)}>
              <header>
                <b>{col.titulo}</b>
                <span className="mono">{datos?.total ?? '…'}</span>
                <small>{col.ayuda}</small>
              </header>
              <div className="tablero-tarjetas">
                {tablero.isPending && Array.from({ length: 3 }, (_, i) => <div key={i} className="tablero-tarjeta"><Esqueleto alto={14} ancho="70%" /><Esqueleto alto={11} ancho="40%" /></div>)}
                {datos?.tarjetas.map((t) => (
                  <article key={t.postulacionId} className="tablero-tarjeta" draggable onDragStart={(e) => e.dataTransfer.setData('text/plain', t.postulacionId)}>
                    <div className="tablero-tarjeta-cabeza">
                      <Link to={`/consola/candidatos/${t.candidatoId}`}>{t.nombre}</Link>
                      {t.puntaje !== null
                        ? <span className={`tablero-puntaje mono ${t.ajustado ? 'ajustado' : 'ia'}`} title={t.ajustado ? 'Puntaje ajustado por una persona' : 'Sugerencia de la IA'}>{t.puntaje}</span>
                        : t.estadoAfinidad === 'Calculando' && <span className="afinidad-calculando"><span className="orbe trabajando" /></span>}
                    </div>
                    <span className="tablero-sub">{t.ciudad || 'Sin ciudad'} · <span className="mono">{t.numero}</span></span>
                    {t.razonPrincipal && <span className="tablero-razon">+ {t.razonPrincipal}</span>}
                    {t.respuestasNegativas > 0 && <span className="tablero-alerta"><IconoAlerta />{t.respuestasNegativas} {t.respuestasNegativas === 1 ? 'respuesta negativa' : 'respuestas negativas'} en el filtro</span>}
                    {t.motivoDescarte && <span className="tablero-motivo">Motivo: «{t.motivoDescarte}»</span>}
                    {col.etapa !== 'Descartada' && (t.resultadoFiltros === 'NoCumple' || t.resultadoFiltros === 'PorRevisar') && (
                      <span className={`tablero-filtro ${t.resultadoFiltros.toLowerCase()}`} title={t.motivoFiltro ?? undefined}>
                        <IconoAlerta /><span>{t.resultadoFiltros === 'NoCumple' ? `No cumple · ${t.motivoFiltro ?? ''}` : `Sin evidencia · ${t.motivoFiltro ?? ''}`}</span>
                      </span>
                    )}
                    {col.etapa === 'Terna' && (t.procesoVinculacionId
                      ? <Link className="pill pill-ok" to={`/consola/vinculacion/${t.procesoVinculacionId}`}>Contratación aprobada</Link>
                      : apruebaContratacion && <Boton pequeno variante="primario" onClick={() => setContratando(t.postulacionId)}>Aprobar contratación</Boton>)}
                    <label className="tablero-mover">
                      <span className="sr-only">Mover a {t.nombre}</span>
                      <select className="selector" value="" disabled={mover.isPending} onChange={(e) => e.target.value && moverA(t, e.target.value as Etapa)}>
                        <option value="">Mover a…</option>
                        {columnas.filter((c) => c.etapa !== col.etapa).map((c) => <option key={c.etapa} value={c.etapa}>{nombre[c.etapa]}</option>)}
                      </select>
                    </label>
                  </article>
                ))}
                {datos && datos.total === 0 && <p className="tablero-vacia">Arrastre aquí</p>}
                {datos && datos.total > datos.tarjetas.length && (
                  <button type="button" className="enlace-boton" onClick={() => setPorColumna((n) => n + 20)}>Ver {Math.min(20, datos.total - datos.tarjetas.length)} más de {datos.total}</button>
                )}
              </div>
            </div>
          )
        })}
      </div>
    </section>
  )
}
