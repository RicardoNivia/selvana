import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoAlerta, IconoEscudo } from '../../shared/ui/Iconos'
import { aRequest, type ContratacionSugerida, type CondicionesRequest, pesos, useAprobarContratacion, useContratacionSugerida, useRevisarCondiciones } from './api'
import { AlertasContrato, CondicionesFormulario } from './CondicionesFormulario'
import './vinculacion.css'

/**
 * Decisión humana: contratar a un candidato de la terna. Las condiciones llegan prellenadas por reglas (con su razón);
 * la persona las ajusta y, al aprobar, la IA redacta la oferta y el contrato.
 */
export function AprobarContratacion({ postulacionId, onCerrar }: { postulacionId: string; onCerrar: () => void }) {
  const sugerida = useContratacionSugerida(postulacionId)

  useEffect(() => {
    const cerrar = (e: KeyboardEvent) => e.key === 'Escape' && onCerrar()
    window.addEventListener('keydown', cerrar)
    return () => window.removeEventListener('keydown', cerrar)
  }, [onCerrar])

  const s = sugerida.data
  return (
    <div className="vin-capa" role="presentation" onClick={(e) => e.target === e.currentTarget && onCerrar()}>
      <section className="vin-dialogo tarjeta panel-decision" role="dialog" aria-modal="true" aria-labelledby="vin-titulo">
        <header className="vin-dialogo-cabeza">
          <div>
            <h2 id="vin-titulo">Aprobar contratación</h2>
            {s ? <p>{s.nombre} · <span className="mono">CC {s.documento}</span> · {s.vacante}{s.empresaUsuaria ? ` · ${s.empresaUsuaria}` : ''}</p> : <Esqueleto alto={14} ancho="60%" />}
          </div>
          <button type="button" className="enlace-boton" onClick={onCerrar} aria-label="Cerrar">Cerrar</button>
        </header>
        {sugerida.isError && <p role="alert" className="aviso aviso-error"><IconoAlerta />{sugerida.error.message}</p>}
        {s ? <Formulario s={s} onCerrar={onCerrar} /> : !sugerida.isError && <div className="vin-condiciones">{Array.from({ length: 5 }, (_, i) => <Esqueleto key={i} alto={58} />)}</div>}
      </section>
    </div>
  )
}

function Formulario({ s, onCerrar }: { s: ContratacionSugerida; onCerrar: () => void }) {
  const [condiciones, setCondiciones] = useState<CondicionesRequest>(() => aRequest(s.condiciones))
  const revision = useRevisarCondiciones(s.postulacionId, condiciones)
  const aprobar = useAprobarContratacion()
  const navegar = useNavigate()
  const alertas = revision.data ?? s.alertas
  const bloquea = alertas.some((a) => a.nivel === 'Bloquea')

  return (
    <>
      {s.procesoId && (
        <p className="aviso aviso-alerta"><IconoAlerta />Este candidato ya tiene un proceso de vinculación. <Link to={`/consola/vinculacion/${s.procesoId}`}>Abrirlo</Link></p>
      )}
      <div className="vin-sugerencia">
        <span className="vin-etiqueta">Prellenado por reglas · ajústelo</span>
        {s.razones.map((r) => (
          <p key={r.texto} className="razon"><span className={`signo ${r.signo === '+' ? 'mas' : 'menos'}`}>{r.signo}</span><span className="texto">{r.texto}</span><span className="cita">{r.fuente}</span></p>
        ))}
        <p className="vin-rango">Rango de la vacante <b className="mono">{pesos.format(s.salarioMinimo)} – {pesos.format(s.salarioMaximo)}</b>
          {s.aspiracionSalarialCop !== null && <> · aspiración en la preselección <b className="mono">{pesos.format(s.aspiracionSalarialCop)}</b></>}</p>
      </div>
      <CondicionesFormulario valor={condiciones} onCambiar={setCondiciones} empresaUsuaria={s.empresaUsuaria} />
      <AlertasContrato alertas={alertas} cargando={revision.isFetching} />

      {aprobar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{aprobar.error.message}</p>}
      <div className="decision">
        <Boton variante="primario" disabled={bloquea || Boolean(s.procesoId)} cargando={aprobar.isPending} textoCargando="Aprobando…"
          onClick={() => aprobar.mutate({ postulacionId: s.postulacionId, condiciones }, { onSuccess: (r) => navegar(`/consola/vinculacion/${r.id}`) })}>
          Aprobar contratación
        </Boton>
        <Boton onClick={onCerrar}>Cancelar</Boton>
        <span className="registro">Contratar afecta a una persona: queda en la bitácora con su nombre <IconoEscudo /></span>
      </div>
    </>
  )
}
