import { useState } from 'react'
import { Boton } from '../../shared/ui/Boton'
import { IconoCheck, IconoReloj } from '../../shared/ui/Iconos'
import { type Vacante, useDevolverRequisicion } from './api'

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const nombreRol: Record<string, string> = { Administrador: 'Administrador', GerenteRrhh: 'Gerente RRHH', Analista: 'Analista', Lider: 'Líder' }

/** Texto del botón de aprobar: el administrador aprueba todas las etapas de una vez; en la última etapa, aprobar publica. */
export function textoAprobar(v: Vacante, rol: string) {
  const a = v.aprobacion
  return rol === 'Administrador' || a.etapaPendiente === a.etapas[a.etapas.length - 1] ? 'Aprobar y publicar' : `Aprobar por ${a.etapaPendiente}`
}

/** Requisición líder → RRHH → gerencia: en qué etapa va, quién decidió y, si le toca al usuario, aprobar o devolver. */
export function AprobacionRequisicion({ vacante: v }: { vacante: Vacante }) {
  const a = v.aprobacion
  const devolver = useDevolverRequisicion(v.id)
  const [devolviendo, setDevolviendo] = useState(false)
  const [motivo, setMotivo] = useState('')
  const enAprobacion = v.estado === 'EnAprobacion'
  if (!enAprobacion && a.decisiones.length === 0 && !a.motivoDevolucion) return null

  const error = devolver.error
  return (
    <section className={`tarjeta requisicion ${a.puedeDecidir ? 'panel-decision' : ''}`} aria-label="Aprobación de la requisición">
      <h3>Aprobación de la requisición</h3>
      {a.pedidaPor && <p className="requisicion-pedida">La pidió <b>{a.pedidaPor}</b></p>}
      <ol className="requisicion-etapas">
        {a.etapas.map((etapa) => {
          const d = [...a.decisiones].reverse().find((x) => x.etapa === etapa)
          const pendiente = enAprobacion && a.etapaPendiente === etapa
          const estado = pendiente ? 'pendiente' : d?.decision === 'Aprobada' ? 'aprobada' : d?.decision === 'Devuelta' ? 'devuelta' : 'futura'
          return (
            <li key={etapa} className={`requisicion-etapa ${estado}`}>
              <span className="requisicion-punto">{estado === 'aprobada' ? <IconoCheck /> : estado === 'pendiente' ? <IconoReloj /> : null}</span>
              <div>
                <b>{etapa}</b>
                {estado === 'aprobada' && d && (
                  <span>{d.automatica ? `Aprobada al pedirla: ${d.por} es ${nombreRol[d.rol] ?? d.rol}` : `Aprobó ${d.por}`} · <span className="mono">{fecha.format(new Date(d.en))}</span></span>
                )}
                {estado === 'devuelta' && d && <span>Devuelta por {d.por}: «{d.motivo}»</span>}
                {pendiente && <span>Espera a {a.quienesAprueban.map((r) => nombreRol[r] ?? r).join(' o ')}</span>}
                {estado === 'futura' && <span>Después de la etapa anterior</span>}
              </div>
            </li>
          )
        })}
      </ol>

      {/* Aprobar está en la barra de la vacante; aquí queda devolver con el motivo */}
      {a.puedeDecidir && !devolviendo && (
        <div className="decision" style={{ padding: 0, borderTop: 0 }}>
          <Boton onClick={() => setDevolviendo(true)}>Devolver</Boton>
        </div>
      )}
      {a.puedeDecidir && devolviendo && (
        <form className="formulario" onSubmit={(e) => { e.preventDefault(); devolver.mutate(motivo.trim()) }}>
          <label>¿Por qué la devuelve?<textarea className="area-texto" rows={3} value={motivo} onChange={(e) => setMotivo(e.target.value)} placeholder="El salario está por encima de la banda del cargo" autoFocus /></label>
          <div className="decision" style={{ padding: 0, borderTop: 0 }}>
            <Boton type="submit" variante="primario" disabled={!motivo.trim()} cargando={devolver.isPending} textoCargando="Devolviendo…">Devolver con el motivo</Boton>
            <Boton onClick={() => setDevolviendo(false)}>Cancelar</Boton>
          </div>
        </form>
      )}
      {error && <span role="alert" className="error-formulario">{error.message}</span>}
      {a.puedeDecidir && <span className="registro">Se registran la sugerencia y tu decisión</span>}
    </section>
  )
}
