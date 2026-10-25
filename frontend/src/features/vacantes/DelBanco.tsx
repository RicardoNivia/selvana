import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { Paginacion } from '../../shared/ui/Paginacion'
import { usePaginacion } from '../../shared/ui/paginado'
import type { Vacante } from './api'
import { type InvitacionBanco, type SugerenciaBanco, useBuscarEnBanco, useInvitarDesdeBanco, useNoEncaja, useSugerenciasBanco } from './bancoApi'
import './banco.css'

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' })

/** WhatsApp con el mensaje escrito (lo envía la persona). Celulares colombianos de 10 dígitos llevan el 57. */
function enlaceWhatsApp(celular: string, texto: string) {
  const digitos = celular.replace(/\D/g, '')
  const numero = digitos.length === 10 && digitos.startsWith('3') ? `57${digitos}` : digitos
  return numero.length >= 10 ? `https://wa.me/${numero}?text=${encodeURIComponent(texto)}` : null
}

/**
 * Recontacto del banco de talento: al publicar, las reglas buscan a quien ya se postuló antes y encaja (cargo, ciudad, requisitos),
 * el agente de ranking los califica sobre su perfil anónimo y una persona decide a quién invitar. Nadie queda postulado solo.
 */
export function DelBanco({ vacante }: { vacante: Vacante }) {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const lista = useSugerenciasBanco(vacante.id, pagina, tamano)
  const buscar = useBuscarEnBanco(vacante.id)
  const [hecho, setHecho] = useState<string | null>(null)
  const [invitaciones, setInvitaciones] = useState<Record<string, InvitacionBanco>>({})
  const publicada = vacante.estado === 'Publicada'
  const calculando = lista.data?.items.filter((s) => s.afinidad?.estado === 'Calculando').length ?? 0

  return (
    <section className="tarjeta ats-panel banco" aria-label="Personas del banco de talento">
      <header>
        <div>
          <h2>Del banco de talento</h2>
          <p>Personas que se postularon antes a su empresa, autorizaron que les ofrezcan vacantes similares y encajan con esta. Usted decide a quién invitar: cada una se postula ella misma.</p>
        </div>
        {publicada && (
          <Boton cargando={buscar.isPending} textoCargando="Buscando…"
            onClick={() => buscar.mutate(undefined, { onSuccess: (r) => setHecho(r.sugeridos === 0 ? 'Nadie nuevo del banco encaja por ahora.' : `${r.sugeridos} ${r.sugeridos === 1 ? 'persona nueva encaja' : 'personas nuevas encajan'}; la IA las está calificando.`) })}>
            Buscar de nuevo en el banco
          </Boton>
        )}
      </header>

      {!publicada && <p className="aviso aviso-alerta"><IconoAlerta />Se busca en el banco cuando la vacante se publica.</p>}
      {hecho && <p className="aviso aviso-ok" role="status"><IconoCheck />{hecho}</p>}
      {buscar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{buscar.error.message}</p>}
      {calculando > 0 && (
        <div className="estado-agente" role="status">
          <span className="orbe trabajando" />
          <span>El agente de ranking está calificando <b className="mono">{calculando}</b> {calculando === 1 ? 'persona' : 'personas'} del banco. Cuando termine, la propuesta llega a la bandeja.</span>
        </div>
      )}

      <div className="ats-filas">
        {lista.isPending && Array.from({ length: 3 }, (_, i) => <div key={i} className="ats-fila"><Esqueleto alto={16} ancho="40%" /><Esqueleto alto={96} /></div>)}
        {lista.isError && <p role="alert" className="aviso aviso-error"><IconoAlerta />{lista.error.message}</p>}
        {lista.data?.items.map((s) => (
          <Sugerencia key={s.id} s={s} vacante={vacante} invitacion={invitaciones[s.id]} onInvitada={(i) => setInvitaciones((x) => ({ ...x, [s.id]: i }))} />
        ))}
        {lista.data && lista.data.total === 0 && (
          <div className="vacio"><p>{publicada ? 'Todavía no hay personas del banco que encajen con esta vacante.' : 'Aún no se ha buscado en el banco.'}</p></div>
        )}
      </div>
      {lista.data && lista.data.total > 0 && <Paginacion pagina={lista.data} cargando={lista.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
      <p className="ficha-nota"><IconoEscudo />Solo aparecen quienes autorizaron recibir vacantes similares (Ley 1581). El contacto se ve cuando usted decide invitar, y todo queda en la bitácora.</p>
    </section>
  )
}

function Sugerencia({ s, vacante, invitacion, onInvitada }: { s: SugerenciaBanco; vacante: Vacante; invitacion?: InvitacionBanco; onInvitada: (i: InvitacionBanco) => void }) {
  const invitar = useInvitarDesdeBanco()
  const noEncaja = useNoEncaja()
  const [descartando, setDescartando] = useState(false)
  const [motivo, setMotivo] = useState('')
  const [copiado, setCopiado] = useState(false)
  const a = s.afinidad
  const puntaje = a?.estado === 'Lista' ? a.puntaje : null

  const enlace = `${window.location.origin}${invitacion?.ruta ?? `/empleos/${vacante.codigo}/postular?utm_source=Banco`}`
  const mensaje = invitacion ? `${invitacion.mensaje} ${enlace}` : enlace
  const whatsapp = s.celular ? enlaceWhatsApp(s.celular, mensaje) : null
  const copiar = async () => {
    try {
      await navigator.clipboard.writeText(mensaje)
      setCopiado(true)
    } catch {
      setCopiado(false)
    }
  }

  return (
    <article className={`ats-fila banco-fila ${s.estado !== 'Sugerida' ? 'decidida' : ''}`}>
      <header>
        <Link to={`/consola/candidatos/${s.candidatoId}`}>{s.nombre}</Link>
        <span className="ats-sub">
          {s.ciudad || 'Sin ciudad'} · <span className="mono">{s.documento}</span> · {s.postulacionesPrevias} {s.postulacionesPrevias === 1 ? 'postulación anterior' : 'postulaciones anteriores'}
        </span>
        {s.yaSePostulo && <span className="pill pill-ok">Ya se postuló</span>}
        {!s.yaSePostulo && s.estado === 'Invitada' && <span className="pill pill-ok">Invitado por {s.decididaPor}</span>}
        {s.estado === 'NoEncaja' && <span className="pill pill-neutra">No encaja · {s.decididaPor}</span>}
        {s.estado === 'Sugerida' && (puntaje !== null
          ? <span className="pill pill-ia banco-puntaje"><Chispa /><b className="mono">{puntaje}/100</b></span>
          : a?.estado === 'Calculando' ? <span className="afinidad-calculando"><span className="orbe trabajando" />Calificando…</span>
          : <span className="pill pill-neutra">Reglas <b className="mono">{s.puntajeReglas}/100</b></span>)}
      </header>

      <ul className="ats-chequeos">
        {s.coincidencias.map((c) => (
          <li key={`${c.tipo}-${c.texto}`} className="cumple">
            <span className="signo">+</span>
            <span><b>{c.tipo}</b> · {c.texto}</span>
            <span className="cita">{c.evidencia}</span>
          </li>
        ))}
      </ul>

      {a?.estado === 'Lista' && s.estado === 'Sugerida' && (
        <div className="bloque-ia">
          <div className="bloque-ia-cabeza"><Chispa />Por qué la IA le da <b className="mono">{a.puntaje}/100</b>{a.modelo && <span className="derecha">{a.modelo}</span>}</div>
          {a.razones.map((r) => (
            <div key={r.texto} className="razon">
              <span className={`signo ${r.signo === '+' ? 'mas' : 'menos'}`}>{r.signo}</span>
              <span className="texto">{r.texto}</span>
              <span className="cita">{r.evidencia}</span>
            </div>
          ))}
          {a.faltantes.length > 0 && (
            <div className="afinidad-requisitos"><div><b>No demuestra</b>{a.faltantes.map((x) => <span key={x} className="pill pill-alerta">{x}</span>)}</div></div>
          )}
          <span className="ficha-nota"><IconoEscudo />No se usaron para el puntaje: nombre, edad, género, estado civil, foto ni dirección exacta. El agente recibe un perfil anónimo.</span>
        </div>
      )}
      {a?.estado === 'Fallo' && s.estado === 'Sugerida' && <span className="ats-sub">La IA no pudo calificarlo: queda con el puntaje de las reglas.</span>}
      {s.estado === 'NoEncaja' && s.motivo && <span className="ats-sub">Motivo: «{s.motivo}»</span>}

      {s.estado === 'Invitada' && !s.yaSePostulo && (
        <div className="banco-invitacion">
          {invitacion && <p>{invitacion.mensaje} <span className="mono">{enlace}</span></p>}
          <div className="banco-contacto">
            {s.celular && <span className="mono">{s.celular}</span>}
            {s.correo && <span className="mono">{s.correo}</span>}
          </div>
          <div className="ats-acciones">
            <Boton pequeno icono={copiado ? <IconoCheck /> : undefined} onClick={copiar}>{copiado ? 'Copiado' : invitacion ? 'Copiar mensaje' : 'Copiar enlace'}</Boton>
            {whatsapp && <a className="boton boton-secundario boton-pequeno" href={whatsapp} target="_blank" rel="noreferrer">Abrir WhatsApp</a>}
          </div>
          <small className="ats-sub">Envíelo usted por WhatsApp o correo. El envío automático llega con las integraciones. Al postularse, entra por el canal «Banco de talento».</small>
        </div>
      )}

      {s.estado === 'Sugerida' && (descartando ? (
        <form className="banco-descartar" onSubmit={(e) => { e.preventDefault(); noEncaja.mutate({ sugerenciaId: s.id, motivo: motivo.trim() }) }}>
          <input className="entrada" value={motivo} onChange={(e) => setMotivo(e.target.value)} placeholder="¿Por qué no encaja? Ej.: no tiene experiencia en cargue" aria-label="Motivo" autoFocus />
          <Boton type="submit" pequeno disabled={!motivo.trim()} cargando={noEncaja.isPending} textoCargando="Guardando…">Guardar</Boton>
          <Boton pequeno onClick={() => setDescartando(false)}>Cancelar</Boton>
        </form>
      ) : (
        <div className="decision">
          <Boton variante="primario" pequeno cargando={invitar.isPending} textoCargando="Invitando…" onClick={() => invitar.mutate(s.id, { onSuccess: onInvitada })}>Invitar a postularse</Boton>
          <Boton pequeno onClick={() => setDescartando(true)}>No encaja</Boton>
          <span className="registro">Se registran la sugerencia y tu decisión</span>
        </div>
      ))}
      {(invitar.error ?? noEncaja.error) && <p role="alert" className="aviso aviso-error"><IconoAlerta />{(invitar.error ?? noEncaja.error)!.message}</p>}
      {s.decididaEn && <span className="ats-sub">Decidido el {fecha.format(new Date(s.decididaEn))}</span>}
    </article>
  )
}
