import { useState } from 'react'
import { Boton } from '../../shared/ui/Boton'
import { Chispa, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { type Afinidad, type Postulante, useAjustarAfinidad, useRetroalimentar } from './api'

/** Puntaje de afinidad en la tabla: violeta mientras sea solo la sugerencia de la IA, tinta cuando una persona lo ajustó. */
export function PuntajeAfinidad({ afinidad, abierto, onAlternar }: { afinidad: Afinidad | null; abierto: boolean; onAlternar: () => void }) {
  if (!afinidad || afinidad.estado === 'SinCalcular') return <span className="afinidad-vacia">Sin calcular</span>
  if (afinidad.estado === 'Calculando') return <span className="afinidad-calculando"><span className="orbe trabajando" />Calculando…</span>
  if (afinidad.estado === 'Fallo') return <span className="afinidad-vacia" title={afinidad.mensaje ?? ''}>No se pudo calcular</span>
  const nivel = (afinidad.puntaje ?? 0) >= 85 ? 'alta' : (afinidad.puntaje ?? 0) >= 60 ? 'media' : 'baja'
  return (
    <button type="button" className={`afinidad ${afinidad.ajustado ? 'ajustada' : 'ia'} ${nivel}`} aria-expanded={abierto} onClick={onAlternar}
      title={afinidad.ajustado ? `Ajustado por ${afinidad.ajustadoPor}` : 'Sugerencia de la IA: abra para ver por qué'}>
      <b className="mono">{afinidad.puntaje}</b>
      <span>{abierto ? 'Ocultar' : 'Por qué'}</span>
    </button>
  )
}

/** Explicación del puntaje: razones con evidencia, requisitos y la corrección de una persona (queda en la auditoría). */
export function ExplicacionAfinidad({ postulante, afinidad }: { postulante: Postulante; afinidad: Afinidad }) {
  const postulacionId = postulante.postulacionId
  const ajustar = useAjustarAfinidad()
  const retro = useRetroalimentar()
  const [nota, setNota] = useState(postulante.notaRetroalimentacion ?? '')
  const [editando, setEditando] = useState(false)
  const [puntaje, setPuntaje] = useState(String(afinidad.puntaje ?? ''))
  const [motivo, setMotivo] = useState('')

  return (
    <div className="explicacion-afinidad">
      <div className={afinidad.ajustado ? 'bloque-confirmado' : 'bloque-ia'}>
        <div className="bloque-ia-cabeza" style={{ fontSize: 13 }}>
          {afinidad.ajustado ? <IconoCheck /> : <Chispa />}
          {afinidad.ajustado
            ? <>Puntaje ajustado por {afinidad.ajustadoPor}: <b className="mono">{afinidad.puntaje}</b> (la IA sugirió <span className="mono">{afinidad.puntajeIa}</span>)</>
            : <>Por qué la IA le da <b className="mono">{afinidad.puntaje}/100</b></>}
          {afinidad.modelo && <span className="cita" style={{ marginLeft: 'auto' }}>{afinidad.modelo}</span>}
        </div>
        {afinidad.ajustado && afinidad.motivoAjuste && <p className="afinidad-motivo">Motivo: «{afinidad.motivoAjuste}»</p>}
        {afinidad.razones.map((r) => (
          <div key={r.texto} className="razon">
            <span className={`signo ${r.signo === '+' ? 'mas' : 'menos'}`}>{r.signo}</span>
            <span className="texto">{r.texto}</span>
            <span className="cita">{r.fuente}</span>
          </div>
        ))}
        <div className="afinidad-requisitos">
          {afinidad.cumplidos.length > 0 && <div><b>Cumple</b>{afinidad.cumplidos.map((x) => <span key={x} className="pill pill-ok">{x}</span>)}</div>}
          {afinidad.faltantes.length > 0 && <div><b>No demuestra</b>{afinidad.faltantes.map((x) => <span key={x} className="pill pill-alerta">{x}</span>)}</div>}
        </div>
        <span className="ficha-nota"><IconoEscudo />No se usaron para el puntaje: nombre, edad, género, estado civil, foto ni dirección exacta. El agente recibe un perfil anónimo.</span>
      </div>

      <div className="retro-afinidad">
        <span>¿Le sirve para esta vacante?</span>
        <div className="segmentado" role="group" aria-label="Retroalimentación del candidato">
          <button type="button" aria-pressed={postulante.retroalimentacion === 'Bueno'} disabled={retro.isPending}
            onClick={() => retro.mutate({ postulacionId, valor: 'Bueno', nota: nota.trim() || null })}>Buen candidato</button>
          <button type="button" aria-pressed={postulante.retroalimentacion === 'NoEncaja'} disabled={retro.isPending}
            onClick={() => retro.mutate({ postulacionId, valor: 'NoEncaja', nota: nota.trim() || null })}>No encaja</button>
        </div>
        <input className="entrada" value={nota} onChange={(e) => setNota(e.target.value)} placeholder="Nota opcional: qué pesó en su decisión" aria-label="Nota de la retroalimentación" />
        {retro.isPending && <span className="indicador indicador-oscuro" aria-label="Guardando" />}
        {retro.error && <span role="alert" className="error-formulario">{retro.error.message}</span>}
        <small>Calibra el ranking de su empresa: el agente aprende de estas decisiones.</small>
      </div>

      {!editando ? (
        <div className="decision" style={{ padding: 0, borderTop: 0 }}>
          <Boton pequeno onClick={() => setEditando(true)}>Ajustar puntaje</Boton>
          <span className="registro">Se registran la sugerencia y tu decisión</span>
        </div>
      ) : (
        <form className="formulario afinidad-ajuste" onSubmit={(e) => { e.preventDefault(); ajustar.mutate({ postulacionId, puntaje: Number(puntaje), motivo: motivo.trim() }, { onSuccess: () => setEditando(false) }) }}>
          <label>Puntaje (0 a 100)<input className="entrada" type="number" min={0} max={100} value={puntaje} onChange={(e) => setPuntaje(e.target.value)} /></label>
          <label style={{ flex: '1 1 320px' }}>¿Por qué lo ajusta?<input className="entrada" value={motivo} onChange={(e) => setMotivo(e.target.value)} placeholder="El curso de montacargas venció en 2025" autoFocus /></label>
          <div className="decision" style={{ padding: 0, borderTop: 0, flexBasis: '100%' }}>
            <Boton type="submit" variante="primario" disabled={!motivo.trim() || puntaje === ''} cargando={ajustar.isPending} textoCargando="Guardando…">Guardar ajuste</Boton>
            <Boton onClick={() => setEditando(false)}>Cancelar</Boton>
            {ajustar.error && <span role="alert" className="error-formulario">{ajustar.error.message}</span>}
          </div>
        </form>
      )}
    </div>
  )
}
