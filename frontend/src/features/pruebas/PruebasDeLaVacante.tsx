import { useState } from 'react'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoAlerta, IconoCandado, IconoCheck } from '../../shared/ui/Iconos'
import { usePuede } from '../sesion/api'
import { usePostulantes, type Vacante } from '../vacantes/api'
import { type EnvioPruebas, type ResultadoPostulante, enlacePruebas, useEnviarPruebas, useGuardarBateriaVacante, useResultadosPruebas, useVerResultadoSensible } from './api'
import { EditorBateria } from './EditorBateria'

const fecha = new Intl.DateTimeFormat('es-CO', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })
const estadoTexto: Record<ResultadoPostulante['estado'], string> = { Enviada: 'Enviada', EnCurso: 'Presentando', Terminada: 'Terminó', Rechazada: 'No quiso presentar' }

/** Pestaña «Pruebas» de la vacante: la batería (heredada del cargo), el envío a postulantes y los resultados. */
export function PruebasDeLaVacante({ vacante }: { vacante: Vacante }) {
  const guardar = useGuardarBateriaVacante(vacante.id)
  const veResultados = usePuede('VerHojasDeVida')
  const resultados = useResultadosPruebas(vacante.id, veResultados)

  return (
    <div className="pruebas-vacante">
      <section className="tarjeta pruebas-bloque" aria-label="Batería de la vacante">
        <h2>Pruebas de esta vacante</h2>
        <p className="ayuda-suave">Se heredan del cargo «{vacante.cargo}». Si las cambia aquí, solo cambian para esta vacante; para el cargo, ajústelas en Estructura y cargos.</p>
        <EditorBateria key={vacante.pruebas.map((p) => `${p.codigo}${p.peso}${p.corte}`).join()} inicial={vacante.pruebas} guardando={guardar.isPending}
          error={guardar.error?.message} etiquetaGuardar="Guardar para esta vacante" onGuardar={(p) => guardar.mutate(p)} />
        {guardar.isSuccess && <p className="nota-ok" role="status"><IconoCheck />Pruebas guardadas.</p>}
      </section>

      <Enviar vacante={vacante} resultados={resultados.data ?? []} />

      {veResultados && (
        <section className="tarjeta" aria-label="Resultados">
          <div className="barra-filtros"><b>Resultados</b><span className="ayuda-suave">Se actualizan solos mientras los candidatos presentan.</span></div>
          {resultados.isPending ? <div style={{ padding: 16 }}><Esqueleto alto={80} /></div> : resultados.data?.length === 0 ? (
            <p className="vacio-tabla">Aún no ha enviado pruebas en esta vacante.</p>
          ) : <TablaResultados resultados={resultados.data ?? []} />}
        </section>
      )}
    </div>
  )
}

function Enviar({ vacante, resultados }: { vacante: Vacante; resultados: ResultadoPostulante[] }) {
  const postulantes = usePostulantes(vacante.id, { pagina: 1, buscar: '', canal: '' })
  const enviar = useEnviarPruebas(vacante.id)
  const [elegidos, setElegidos] = useState<string[]>([])
  const [enviados, setEnviados] = useState<EnvioPruebas[]>([])
  const estado = new Map(resultados.map((r) => [r.postulacionId, r]))
  const lista = postulantes.data?.items ?? []

  return (
    <section className="tarjeta pruebas-bloque" aria-label="Enviar pruebas">
      <h2>Enviar a postulantes</h2>
      {vacante.pruebas.length === 0 && <p className="aviso aviso-alerta"><IconoAlerta />Primero elija y guarde las pruebas de la vacante.</p>}
      {postulantes.isPending ? <Esqueleto alto={100} /> : lista.length === 0 ? <p className="ayuda-suave">Aún no hay postulantes en esta vacante.</p> : (
        <ul className="envio-lista">
          {lista.map((p) => {
            const r = estado.get(p.postulacionId)
            const on = elegidos.includes(p.postulacionId)
            return (
              <li key={p.postulacionId}>
                <label className="interruptor">
                  <input type="checkbox" checked={on} onChange={() => setElegidos((x) => (on ? x.filter((y) => y !== p.postulacionId) : [...x, p.postulacionId]))} />
                  <span><b>{p.nombre}</b> <span className="mono sub-celda" style={{ display: 'inline' }}>{p.documento}</span></span>
                </label>
                {r && <span className={`pill ${r.estado === 'Terminada' ? 'pill-ok' : r.vencida || r.estado === 'Rechazada' ? 'pill-error' : 'pill-neutra'}`}>{r.vencida ? 'Enlace vencido' : estadoTexto[r.estado]}</span>}
              </li>
            )
          })}
        </ul>
      )}
      {enviar.error && <span role="alert" className="error-formulario">{enviar.error.message}</span>}
      <span className="enlace-acciones">
        <Boton variante="primario" pequeno cargando={enviar.isPending} textoCargando="Enviando…" disabled={elegidos.length === 0 || vacante.pruebas.length === 0}
          onClick={() => enviar.mutate(elegidos, { onSuccess: (r) => { setEnviados(r); setElegidos([]) } })}>
          Enviar pruebas{elegidos.length > 0 ? ` a ${elegidos.length}` : ''}
        </Boton>
        {lista.length > 0 && <Boton pequeno onClick={() => setElegidos(lista.map((p) => p.postulacionId))}>Elegir todos</Boton>}
      </span>
      <p className="ayuda-suave">A quien ya tenía pruebas se le genera un enlace nuevo (el anterior deja de servir). Cada enlace vence en 72 horas.</p>

      {enviados.length > 0 && (
        <div className="enlace-invitacion" role="status">
          <b><IconoCheck />Enlaces listos para {enviados.length} {enviados.length === 1 ? 'postulante' : 'postulantes'}</b>
          <span className="ayuda">Mientras conectamos WhatsApp y el correo, envíeselos usted. Solo se muestran esta vez.</span>
          {enviados.map((e) => <EnlaceCandidato key={e.postulacionId} envio={e} />)}
        </div>
      )}
    </section>
  )
}

function EnlaceCandidato({ envio }: { envio: EnvioPruebas }) {
  const [copiado, setCopiado] = useState(false)
  const url = enlacePruebas(envio.token)
  return (
    <span className="enlace-candidato">
      <b>{envio.titular}</b>
      <span className="mono enlace-texto">{url}</span>
      <Boton pequeno onClick={() => { void navigator.clipboard?.writeText(url).then(() => setCopiado(true)) }}>{copiado ? 'Copiado' : 'Copiar'}</Boton>
    </span>
  )
}

function TablaResultados({ resultados }: { resultados: ResultadoPostulante[] }) {
  const puedeSensible = usePuede('VerDatosSensibles')
  return (
    <div className="tabla-envoltura">
      <table className="tabla">
        <thead><tr><th>Postulante</th><th>Estado</th><th>Pruebas</th><th className="num">Total</th></tr></thead>
        <tbody>
          {resultados.map((r) => (
            <tr key={r.aplicacionId}>
              <td><b>{r.titular}</b><span className="sub-celda mono">Enviada {fecha.format(new Date(r.enviadaEn))}</span></td>
              <td><span className={`pill ${r.estado === 'Terminada' ? 'pill-ok' : r.vencida || r.estado === 'Rechazada' ? 'pill-error' : 'pill-neutra'}`}>{r.vencida ? 'Enlace vencido' : estadoTexto[r.estado]}</span></td>
              <td>
                <ul className="resultado-pruebas">
                  {r.pruebas.map((p) => (
                    <li key={p.codigo}>
                      <span className="resultado-nombre">{p.nombre}</span>
                      {p.estado !== 'Terminada' ? <span className="sub-celda" style={{ display: 'inline' }}>{p.estado === 'EnCurso' ? 'presentando' : 'pendiente'}</span>
                        : p.sensible ? <ResultadoSensible aplicacionId={r.aplicacionId} codigo={p.codigo} puede={puedeSensible} />
                        : (
                          <span className="resultado-puntaje">
                            <span className="barra-puntaje"><span style={{ width: `${p.puntaje ?? 0}%` }} className={p.pasaCorte === false ? 'bajo' : ''} /></span>
                            <span className="mono">{p.puntaje}</span>
                            {p.corte !== null && <span className={`pill ${p.pasaCorte ? 'pill-ok' : 'pill-alerta'}`}>{p.pasaCorte ? 'Pasa' : `Bajo el corte ${p.corte}`}</span>}
                          </span>
                        )}
                      {p.alertas.length > 0 && <span className="resultado-alertas"><IconoAlerta />{p.alertas.join(' · ')}</span>}
                    </li>
                  ))}
                </ul>
              </td>
              <td className="num">
                {r.total === null ? '—' : <><b className="mono">{r.total}</b>{r.provisional && <span className="sub-celda">provisional</span>}</>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function ResultadoSensible({ aplicacionId, codigo, puede }: { aplicacionId: string; codigo: string; puede: boolean }) {
  const ver = useVerResultadoSensible()
  if (!puede) return <span className="sub-celda" style={{ display: 'inline-flex', gap: 4 }}><IconoCandado />Dato sensible: lo ve quien tenga permiso</span>
  if (!ver.data) {
    return <Boton pequeno icono={<IconoCandado />} cargando={ver.isPending} textoCargando="Abriendo…" onClick={() => ver.mutate({ aplicacionId, codigo })}>Ver (queda en la bitácora)</Boton>
  }
  return (
    <div className="dimensiones">
      {ver.data.dimensiones.map((d) => (
        <span key={d.nombre} className="dimension">
          <span>{d.nombre}</span>
          <span className="barra-puntaje neutra"><span style={{ width: `${d.puntaje}%` }} /></span>
          <span className="mono">{d.puntaje}</span>
        </span>
      ))}
      <span className="sub-celda">{ver.data.nota}</span>
    </div>
  )
}
