import { type CSSProperties, useEffect, useState } from 'react'
import { Link, Navigate, useParams, useSearchParams } from 'react-router-dom'
import { AutocompletarMunicipio, resolverMunicipio } from '../../shared/ui/AutocompletarMunicipio'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoCheck, IconoDocumento, IconoFlecha } from '../../shared/ui/Iconos'
import { useMiCuenta, useMiPortal, usePostularmeConMiPerfil } from '../portal-persona/api'
import { type BorradorPostulacion, esLinkedIn, rangoSalario, useBorrador, useEnviarPostulacion, useVacantePublica } from './api'

const v = (i: number) => ({ '--i': i }) as CSSProperties
const miles = new Intl.NumberFormat('es-CO')
const pasos = ['Confirmar', 'Tus datos', 'Enviada']

/** Flujo de postulación con la cuenta del portal: confirma que quiere postularse con su perfil → responde y acepta → envía. */
export function Postular() {
  const { codigo = '' } = useParams()
  const [busqueda] = useSearchParams()
  const cuenta = useMiCuenta()
  const portal = useMiPortal()
  // Desde «Ofertas para ti» llega con el borrador ya listo (su hoja de vida del perfil): empieza en «Tus datos»
  const [borradorId, setBorradorId] = useState<string | undefined>(busqueda.get('borrador') ?? undefined)
  const [enviada, setEnviada] = useState<string>()
  const vacante = useVacantePublica(codigo)
  const borrador = useBorrador(borradorId)

  // Solo se postula con cuenta del portal (ADR 0013): sin sesión, a ingresar o crear la cuenta y volver aquí
  if (cuenta.isError) return <Navigate to={`/mi-portal/ingresar?volver=${encodeURIComponent(`/empleos/${codigo}/postular`)}`} replace />

  const yaPostulado = !enviada && portal.data?.postulaciones.some((p) => p.vacanteCodigo === codigo)
  const paso = enviada ? 4 : !borradorId ? 1 : borrador.data?.estado === 'Leyendo' || borrador.isPending ? 2 : 3
  const indice = paso <= 2 ? 0 : paso === 3 ? 1 : 2
  const canal = busqueda.get('utm_source') ?? 'Portal'

  return (
    <main className="portal-ancho postular">
      <div className="postular-cabeza">
        <Link to={`/empleos/${codigo}`} aria-label="Volver a la vacante" className="postular-volver"><IconoFlecha /></Link>
        <div style={{ minWidth: 0, flex: 1 }}>
          {vacante.data ? (
            <>
              <b className="postular-titulo">{vacante.data.titulo}</b>
              <span className="postular-sub">{vacante.data.empresa} · {vacante.data.ciudad} · <span className="mono">{rangoSalario(vacante.data.salarioMinimo, vacante.data.salarioMaximo)}</span></span>
            </>
          ) : (
            <><Esqueleto alto={16} ancho="70%" /><Esqueleto alto={12} ancho="50%" style={{ marginTop: 6 }} /></>
          )}
        </div>
      </div>

      <ol className="postular-pasos" aria-label="Avance de la postulación">
        {pasos.map((p, i) => (
          <li key={p} className={i < indice || paso === 4 ? 'hecho' : i === indice ? 'actual' : ''} aria-current={i === indice ? 'step' : undefined}>
            <span className="postular-pasos-circulo">{i < indice || paso === 4 ? <IconoCheck /> : i + 1}</span>
            <span className="postular-pasos-texto">{p}</span>
          </li>
        ))}
      </ol>

      <div key={paso} className="postular-escena">
        {cuenta.isPending || portal.isPending ? (
          <section className="postular-cuerpo"><Esqueleto alto={120} /></section>
        ) : yaPostulado ? (
          <section className="postular-cuerpo postular-final" role="status">
            <h1>Ya te postulaste a esta vacante</h1>
            <p className="postular-ayuda">Mira cómo va, el chat con la empresa y tus pendientes en tu portal.</p>
            <Link to="/mi-portal" className="boton boton-primario">Ir a mi portal</Link>
          </section>
        ) : paso === 1 ? (
          <ConfirmarPostularme codigo={codigo} onListo={setBorradorId} />
        ) : paso === 2 ? (
          <Leyendo nombre={borrador.data?.nombreArchivo} />
        ) : paso === 3 && borrador.data ? (
          <Confirmar borrador={borrador.data} canal={canal} linkedInDado="" onEnviada={setEnviada} />
        ) : paso === 4 ? (
          <Enviada numero={enviada!} empresa={vacante.data?.empresa} />
        ) : null}
      </div>
    </main>
  )
}

/** Antes de postular: «¿Quieres postularte con tu perfil?», con los datos y la hoja de vida que tiene guardados en el portal. */
function ConfirmarPostularme({ codigo, onListo }: { codigo: string; onListo: (borradorId: string) => void }) {
  const cuenta = useMiCuenta()
  const postular = usePostularmeConMiPerfil()
  const c = cuenta.data!

  if (!c.nombreArchivoHojaDeVida) {
    return (
      <section className="postular-cuerpo">
        <h1>Primero sube tu hoja de vida</h1>
        <p className="postular-ayuda">Te postulas con la hoja de vida de tu portal. Súbela allí (o el PDF de tu LinkedIn) y la IA llena tu perfil.</p>
        <Link to="/mi-portal" className="boton boton-primario">Ir a mi hoja de vida</Link>
      </section>
    )
  }

  return (
    <section className="postular-cuerpo">
      <h1>¿Quieres postularte?</h1>
      <p className="postular-ayuda">Te postulas con los datos y la hoja de vida de tu portal. Después respondes las preguntas de la empresa y aceptas su autorización de datos.</p>
      <div className="postular-grupo">
        <span><b>{c.datos.nombre}</b></span>
        <span className="postular-nota-suave">{c.correo} · {c.tipoDocumento} <span className="mono">{c.datos.documento}</span></span>
        <span className="postular-nota-suave">{c.datos.celular || 'Sin celular'} · {c.datos.ciudad || 'Sin ciudad'}</span>
        <span className="postular-nota-suave"><IconoDocumento /> {c.nombreArchivoHojaDeVida}</span>
      </div>
      {postular.error && <p role="alert" className="aviso aviso-alerta postular-error"><IconoAlerta />{postular.error.message}</p>}
      <div className="postular-enviar-barra">
        <Boton variante="primario" cargando={postular.isPending} textoCargando="Preparando…" onClick={() => postular.mutate(codigo, { onSuccess: (r) => onListo(r.id) })} className="postular-enviar">
          Sí, quiero postularme <IconoFlecha />
        </Boton>
        <Link to="/mi-portal" className="postular-ayuda">Corregir mis datos en el portal</Link>
      </div>
    </section>
  )
}

function Leyendo({ nombre }: { nombre?: string }) {
  const secciones = ['Datos de contacto', 'Experiencia', 'Formación', 'Cursos y certificaciones', 'Habilidades']
  const [avance, setAvance] = useState(0)
  // El avance por sección es orientativo: la IA lee todo el documento de una vez
  useEffect(() => {
    const t = setInterval(() => setAvance((a) => Math.min(a + 1, secciones.length - 1)), 1800)
    return () => clearInterval(t)
  }, [secciones.length])

  return (
    <section className="postular-cuerpo postular-leyendo" role="status">
      <div className="postular-documento" aria-hidden="true">
        <IconoDocumento />
        <span className="postular-documento-lineas"><i /><i /><i /><i /><i /></span>
        <span className="postular-escaner" />
      </div>
      <h1>Estamos leyendo tu hoja de vida</h1>
      <span className="postular-ayuda">{nombre ?? 'Tu documento'} · sección <span className="mono">{avance + 1} de {secciones.length}</span></span>
      <ul className="postular-secciones">
        {secciones.map((s, i) => (
          <li key={s} className={i < avance ? 'leida' : i === avance ? 'leyendo' : ''} style={v(i)}>
            <span className="postular-seccion-marca">{i < avance ? <IconoCheck /> : i === avance ? <span className="orbe trabajando" /> : null}</span>
            <span>{s}</span>
            <span className="postular-seccion-estado">{i < avance ? 'Leído' : i === avance ? 'Leyendo…' : 'En cola'}</span>
          </li>
        ))}
      </ul>
    </section>
  )
}

type Campo = 'nombre' | 'documento' | 'celular' | 'correo' | 'ciudad'

const etiquetas: Record<Campo, string> = {
  nombre: 'Nombre completo',
  documento: 'Cédula',
  celular: 'Celular (WhatsApp)',
  correo: 'Correo',
  ciudad: 'Ciudad donde vives',
}

function Confirmar({ borrador, canal, linkedInDado, onEnviada }: { borrador: BorradorPostulacion; canal: string; linkedInDado: string; onEnviada: (numero: string) => void }) {
  const enviar = useEnviarPostulacion(borrador.id)
  const leidos = borrador.datos
  const [valores, setValores] = useState<Record<Campo, string>>({
    nombre: leidos?.nombre ?? '', documento: leidos?.documento ?? '', celular: leidos?.celular ?? '', correo: leidos?.correo ?? '', ciudad: leidos?.ciudad ?? '',
  })
  const [editados, setEditados] = useState<Partial<Record<Campo, boolean>>>({})
  // La ciudad que leyó la IA se busca en el DANE; si corresponde a un solo municipio, queda elegida
  const [ciudadCodigo, setCiudadCodigo] = useState<string | null>(null)
  useEffect(() => {
    if (leidos?.ciudad) void resolverMunicipio(leidos.ciudad).then((m) => { if (m) { setValores((x) => ({ ...x, ciudad: m.municipio })); setCiudadCodigo(m.codigo) } })
  }, [leidos?.ciudad])
  const [respuestas, setRespuestas] = useState<(boolean | undefined)[]>(borrador.preguntasFiltro.map(() => undefined))
  // Preguntas de número o de pesos: se responden con la cifra (solo dígitos)
  const tiposPregunta = borrador.preguntasFiltro.map((_, i) => borrador.tiposPreguntasFiltro?.[i] ?? 'SiNo')
  const [cifras, setCifras] = useState<string[]>(borrador.preguntasFiltro.map(() => ''))
  const respondida = (i: number) => (tiposPregunta[i] === 'SiNo' ? respuestas[i] !== undefined : cifras[i] !== '')
  const [acepta, setAcepta] = useState(false)
  // Lo que el candidato pegó manda sobre lo que la IA encontró en el PDF
  const [linkedIn, setLinkedIn] = useState(linkedInDado.trim() || leidos?.linkedIn || '')
  const [linkedInEditado, setLinkedInEditado] = useState(Boolean(linkedInDado.trim()))
  const linkedInMal = linkedIn.trim().length > 0 && !esLinkedIn(linkedIn)
  const completas = tiposPregunta.every((_, i) => respondida(i))
  const respondidas = tiposPregunta.filter((_, i) => respondida(i)).length

  const cambiar = (c: Campo, valor: string) => {
    setValores((x) => ({ ...x, [c]: valor }))
    setEditados((x) => ({ ...x, [c]: true }))
  }

  return (
    <form
      className="postular-cuerpo"
      onSubmit={(e) => {
        e.preventDefault()
        enviar.mutate({ ...valores, linkedIn: linkedIn.trim() || undefined, respuestas: tiposPregunta.map((t, i) => (t === 'SiNo' ? Boolean(respuestas[i]) : true)),
          valores: tiposPregunta.map((t, i) => (t === 'SiNo' ? null : Number(cifras[i]))), aceptaAutorizacion: acepta, canal }, { onSuccess: (r) => onEnviada(r.numero) })
      }}
    >
      <h1>Revisa tus datos</h1>
      {leidos ? (
        <p className="postular-ayuda"><span className="pill pill-ia"><Chispa />En violeta</span> lo que leímos de tu hoja de vida. Toca cualquier campo para corregirlo.</p>
      ) : (
        <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{borrador.mensaje ?? 'No pudimos leer tu hoja de vida. Escribe tus datos y sigue.'}</p>
      )}

      <div className="postular-grupo">
        {(Object.keys(etiquetas) as Campo[]).map((c, i) => {
          const ia = Boolean(leidos && leidos[c]) && !editados[c]
          if (c === 'ciudad') {
            return (
              <label key={c} className="postular-campo" style={v(i)}>
                {etiquetas[c]}
                {/* La ciudad se elige de la DIVIPOLA del DANE */}
                <AutocompletarMunicipio valor={{ codigo: ciudadCodigo, ciudad: valores.ciudad }} ia={ia} requerido
                  onElegir={(m, texto) => { cambiar('ciudad', m?.municipio ?? texto); setCiudadCodigo(m?.codigo ?? null) }} />
                {ia && <span className="postular-nota-ia"><Chispa />Leído de tu hoja de vida</span>}
              </label>
            )
          }
          return (
            <label key={c} className="postular-campo" style={v(i)}>
              {etiquetas[c]}
              <input
                className={`entrada${ia ? ' entrada-ia' : ''}`}
                value={valores[c]}
                onChange={(e) => cambiar(c, e.target.value)}
                inputMode={c === 'documento' || c === 'celular' ? 'numeric' : c === 'correo' ? 'email' : undefined}
                autoComplete={c === 'nombre' ? 'name' : c === 'correo' ? 'email' : c === 'celular' ? 'tel' : undefined}
                required={c !== 'correo'}
                // El correo y la identificación son los de la cuenta del portal: con ellos se une la postulación
                readOnly={c === 'correo' || c === 'documento'}
              />
              {ia && <span className="postular-nota-ia"><Chispa />Leído de tu hoja de vida</span>}
              {editados[c] && leidos?.[c] && <span className="postular-nota-ok"><IconoCheck />Corregido por ti</span>}
            </label>
          )
        })}
        <label className="postular-campo" style={v(5)}>
          <span>Perfil de LinkedIn <span className="postular-opcional">opcional</span></span>
          <input className={`entrada${leidos?.linkedIn && !linkedInEditado ? ' entrada-ia' : ''}`} inputMode="url" placeholder="linkedin.com/in/tu-nombre"
            value={linkedIn} onChange={(e) => { setLinkedIn(e.target.value); setLinkedInEditado(true) }} aria-invalid={linkedInMal || undefined} />
          {leidos?.linkedIn && !linkedInEditado && <span className="postular-nota-ia"><Chispa />Leído de tu hoja de vida</span>}
          {linkedInMal && <span className="postular-nota-error">Pega el enlace de tu perfil, por ejemplo linkedin.com/in/tu-nombre.</span>}
        </label>
      </div>

      {leidos && leidos.experiencia.length > 0 && (
        <div className="postular-experiencia">
          <span className="postular-grupo-titulo">Experiencia que encontramos <span className="pill pill-ia"><Chispa />IA</span></span>
          <ol>
            {leidos.experiencia.map((x, i) => (
              <li key={`${x.cargo}${x.empresa}`} style={v(i)}><b>{x.cargo}</b><span>{x.empresa}</span><span className="mono">{x.desde} – {x.hasta}</span></li>
            ))}
          </ol>
        </div>
      )}

      {borrador.preguntasFiltro.length > 0 && (
        <fieldset className="postular-preguntas">
          <legend>Preguntas de la empresa <span className="mono">{respondidas}/{borrador.preguntasFiltro.length}</span></legend>
          {borrador.preguntasFiltro.map((p, i) => (
            <div key={p} className="postular-pregunta" style={v(i)}>
              <span>{p}</span>
              {tiposPregunta[i] === 'SiNo' ? (
                <div className="postular-sino" role="group" aria-label={p}>
                  {[true, false].map((valor) => (
                    <button key={String(valor)} type="button" aria-pressed={respuestas[i] === valor} onClick={() => setRespuestas((r) => r.map((x, j) => (j === i ? valor : x)))}>
                      {valor ? 'Sí' : 'No'}
                    </button>
                  ))}
                </div>
              ) : (
                <input className="entrada mono" inputMode="numeric" aria-label={p}
                  placeholder={tiposPregunta[i] === 'Pesos' ? '$ 0' : 'Escribe un número'}
                  value={tiposPregunta[i] === 'Pesos' && cifras[i] ? `$ ${miles.format(Number(cifras[i]))}` : cifras[i]}
                  onChange={(e) => { const d = e.target.value.replace(/\D/g, '').slice(0, 12); setCifras((c) => c.map((x, j) => (j === i ? d : x))) }} />
              )}
            </div>
          ))}
        </fieldset>
      )}

      <label className={`postular-autorizacion ${acepta ? 'aceptada' : ''}`}>
        <input type="checkbox" checked={acepta} onChange={(e) => setAcepta(e.target.checked)} />
        <span>{borrador.textoAutorizacion} <span className="mono">({borrador.versionAutorizacion})</span></span>
      </label>

      {enviar.error && <p role="alert" className="aviso aviso-alerta postular-error"><IconoAlerta />{enviar.error.message}</p>}
      <div className="postular-enviar-barra">
        <Boton type="submit" variante="primario" cargando={enviar.isPending} textoCargando="Enviando…" disabled={!acepta || !completas || linkedInMal || !ciudadCodigo} className="postular-enviar">
          Enviar postulación <IconoFlecha />
        </Boton>
        <span className="postular-ayuda">
          {!ciudadCodigo ? 'Elige tu ciudad de la lista.' : !completas ? 'Responde las preguntas de la empresa.' : !acepta ? 'Acepta la autorización de datos para enviar.' : 'Todo listo. Puedes corregir cualquier dato antes de enviar.'}
        </span>
      </div>
    </form>
  )
}

function Enviada({ numero, empresa }: { numero: string; empresa?: string }) {
  const [copiado, setCopiado] = useState(false)
  return (
    <section className="postular-cuerpo postular-final" role="status">
      <div className="postular-celebracion" aria-hidden="true">
        {Array.from({ length: 14 }, (_, i) => (
          <span key={i} style={{ '--i': i, '--a': `${(360 / 14) * i}deg` } as CSSProperties}>
            <svg viewBox="0 0 24 24"><path d="M2 22C2 10 9 2 22 2C22 14 14 22 2 22Z" /></svg>
          </span>
        ))}
        <svg className="postular-listo" viewBox="0 0 64 64">
          <circle cx="32" cy="32" r="29" />
          <path d="M20 33l8 8 16-17" />
        </svg>
      </div>
      <h1>Listo: tu postulación llegó</h1>
      <p className="postular-ayuda">{empresa ? `${empresa} ya tiene tu hoja de vida.` : 'La empresa ya tiene tu hoja de vida.'} Guarda tu número por si necesitas consultar.</p>
      <button type="button" className="postular-numero mono" onClick={() => { void navigator.clipboard?.writeText(numero); setCopiado(true) }}>
        {numero}<span>{copiado ? 'Copiado' : 'Copiar'}</span>
      </button>
      <ol className="postular-sigue">
        <li style={v(0)}><b>La empresa revisa tu hoja de vida</b><span>Normalmente en los próximos días.</span></li>
        <li style={v(1)}><b>Si sigues en el proceso, te contactan</b><span>Al celular o WhatsApp que confirmaste.</span></li>
        <li style={v(2)}><b>Puedes postularte a más vacantes</b><span>Con la misma hoja de vida, sin volver a llenar nada.</span></li>
      </ol>
      <Link to="/mi-portal/registro" className="boton boton-primario">Sigue tu proceso en tu portal</Link>
      <Link to="/empleos" className="boton boton-secundario">Ver más vacantes</Link>
    </section>
  )
}
