import { useRef, useState } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { AutocompletarMunicipio } from '../../shared/ui/AutocompletarMunicipio'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoCheck, IconoDocumento, IconoEscudo, IconoFlecha, IconoSubir } from '../../shared/ui/Iconos'
import { rangoSalario } from '../portal/api'
import {
  type Pendiente, type Registro, type TipoDocumento, tiposDocumento, useAbrirPendiente, useActualizarMiHojaDeVida, useHojaDeVidaPerfil, useIngresarPortal,
  type PerfilEditable, useActualizarMiPerfil, useChatDePostulacion, useMiCuenta, useMiPortal, useOfertasParaTi, usePostularmeConMiPerfil, useRegistrarme, useSalirPortal, useSubirHojaDeVidaPerfil,
} from './api'
import './portal-persona.css'

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' })
const fechaHora = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const tiposArchivo = '.pdf,.docx,image/jpeg,image/png,image/webp'

/* ---------- Ingresar ---------- */

/** A dónde volver después de entrar o registrarse (por ejemplo, la vacante a la que se iba a postular). Solo rutas internas. */
function useVolver() {
  const v = new URLSearchParams(useLocation().search).get('volver')
  return v && v.startsWith('/') && !v.startsWith('//') ? v : '/mi-portal'
}

export function IngresarPortal() {
  const navegar = useNavigate()
  const volver = useVolver()
  const ingresar = useIngresarPortal()
  const [correo, setCorreo] = useState('')
  const [contrasena, setContrasena] = useState('')

  return (
    <main className="portal-ancho mp-acceso">
      <form className="postular-cuerpo" onSubmit={(e) => { e.preventDefault(); ingresar.mutate({ correo, contrasena }, { onSuccess: () => navegar(volver) }) }}>
        <h1>Entra a tu portal</h1>
        <p className="postular-ayuda">Tus postulaciones, el chat con las empresas, tus pruebas y tu contrato, en un solo lugar.</p>
        <label className="postular-campo">Correo<input className="entrada" type="email" autoComplete="email" value={correo} onChange={(e) => setCorreo(e.target.value)} required /></label>
        <label className="postular-campo">Contraseña<input className="entrada" type="password" autoComplete="current-password" value={contrasena} onChange={(e) => setContrasena(e.target.value)} required /></label>
        {ingresar.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{ingresar.error.message}</p>}
        <Boton type="submit" variante="primario" cargando={ingresar.isPending} textoCargando="Entrando…">Entrar <IconoFlecha /></Boton>
        <p className="postular-ayuda">¿Primera vez? <Link to={volver === '/mi-portal' ? '/mi-portal/registro' : `/mi-portal/registro?volver=${encodeURIComponent(volver)}`}>Crea tu cuenta</Link></p>
      </form>
    </main>
  )
}

/* ---------- Registro: lo mínimo; la hoja de vida se sube después en el portal ---------- */

const textoAutorizacion =
  'Autorizo a Selvana a tratar mis datos personales (hoja de vida y datos de contacto) para crear mi perfil, mostrarme vacantes que encajen conmigo, ' +
  'permitirme postularme y ver el estado de mis procesos, según la Ley 1581 de 2012. Al postularme a una vacante autorizo también a la empresa que la ' +
  'publica. Puedo consultar, actualizar o pedir que se supriman mis datos en cualquier momento.'

export function RegistroPortal() {
  const navegar = useNavigate()
  const volver = useVolver()
  const registrar = useRegistrarme()
  const [f, setF] = useState<Registro>({ correo: '', contrasena: '', nombre: '', tipoDocumento: 'CC', numeroDocumento: '', aceptaAutorizacion: false })
  const poner = (cambio: Partial<Registro>) => setF((x) => ({ ...x, ...cambio }))

  return (
    <main className="portal-ancho mp-acceso">
      <form className="postular-cuerpo" onSubmit={(e) => { e.preventDefault(); registrar.mutate(f, { onSuccess: () => navegar(volver) }) }}>
        <h1>Crea tu cuenta</h1>
        <p className="postular-ayuda">Con tu cuenta sigues tus postulaciones, respondes el chat de las empresas y firmas tu contrato. Después subes tu hoja de vida y la IA llena tu perfil.</p>
        <label className="postular-campo">Correo<input className="entrada" type="email" autoComplete="email" value={f.correo} onChange={(e) => poner({ correo: e.target.value })} required /></label>
        <label className="postular-campo">Nombres y apellidos<input className="entrada" autoComplete="name" value={f.nombre} onChange={(e) => poner({ nombre: e.target.value })} required /></label>
        <div className="postular-grupo mp-identificacion">
          <label className="postular-campo">Tipo de identificación
            <select className="selector" value={f.tipoDocumento} onChange={(e) => poner({ tipoDocumento: e.target.value as TipoDocumento })}>
              {tiposDocumento.map(([v, t]) => <option key={v} value={v}>{t}</option>)}
            </select>
          </label>
          <label className="postular-campo">Número<input className="entrada mono" inputMode={f.tipoDocumento === 'PA' ? 'text' : 'numeric'} value={f.numeroDocumento} onChange={(e) => poner({ numeroDocumento: e.target.value })} required /></label>
        </div>
        <label className="postular-campo">Contraseña <span className="postular-opcional">mínimo 8 caracteres</span>
          <input className="entrada" type="password" autoComplete="new-password" minLength={8} value={f.contrasena} onChange={(e) => poner({ contrasena: e.target.value })} required />
        </label>
        <p className="postular-nota-suave">Con tu correo y tu identificación unimos a tu cuenta las postulaciones que ya hiciste: usa los mismos que diste al postularte.</p>
        <label className={`postular-autorizacion ${f.aceptaAutorizacion ? 'aceptada' : ''}`}>
          <input type="checkbox" checked={f.aceptaAutorizacion} onChange={(e) => poner({ aceptaAutorizacion: e.target.checked })} />
          <span>{textoAutorizacion}</span>
        </label>
        {registrar.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{registrar.error.message}</p>}
        <Boton type="submit" variante="primario" cargando={registrar.isPending} textoCargando="Creando tu cuenta…" disabled={!f.aceptaAutorizacion}>
          Crear mi cuenta <IconoFlecha />
        </Boton>
        <p className="postular-ayuda">¿Ya tienes cuenta? <Link to="/mi-portal/ingresar">Entra aquí</Link></p>
      </form>
    </main>
  )
}

function SubirHojaDeVida({ titulo, ayuda, onSubida }: { titulo: string; ayuda: string; onSubida: (id: string) => void }) {
  const subir = useSubirHojaDeVidaPerfil()
  const archivo = useRef<HTMLInputElement>(null)
  const enviar = (f?: File) => f && subir.mutate(f, { onSuccess: (r) => onSubida(r.id) })

  return (
    <section className="postular-cuerpo">
      {titulo && <h1>{titulo}</h1>}
      <p className="postular-ayuda">{ayuda}</p>
      <input ref={archivo} type="file" accept={tiposArchivo} hidden onChange={(e) => enviar(e.target.files?.[0])} />
      <button type="button" className={`postular-subir ${subir.isPending ? 'subiendo' : ''}`} disabled={subir.isPending} aria-busy={subir.isPending || undefined}
        onClick={() => archivo.current?.click()} onDragOver={(e) => e.preventDefault()} onDrop={(e) => { e.preventDefault(); enviar(e.dataTransfer.files[0]) }}>
        <span className="postular-subir-icono">{subir.isPending ? <span className="indicador" aria-hidden="true" /> : <IconoSubir />}</span>
        <b>{subir.isPending ? 'Subiendo…' : 'Toca para subir tu hoja de vida'}</b>
        <span>o arrástrala aquí · PDF, Word o una foto clara</span>
        <span className="pill pill-ia postular-subir-ia"><Chispa />La IA la lee por ti</span>
      </button>
      {subir.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{subir.error.message}</p>}
      <p className="postular-confianza"><IconoEscudo />Tu hoja de vida se guarda cifrada. No usamos tu edad, género, estado civil ni foto para evaluarte.</p>
    </section>
  )
}

function LeyendoHojaDeVida() {
  return (
    <section className="postular-cuerpo postular-leyendo" role="status">
      <div className="estado-agente"><span className="orbe trabajando" /><span>La IA está leyendo tu hoja de vida…</span></div>
      {[60, 40, 80].map((a, i) => <Esqueleto key={i} alto={a} />)}
    </section>
  )
}

/* ---------- Mi portal ---------- */

type Pestana = 'pendientes' | 'postulaciones' | 'ofertas' | 'hv'

export function MiPortal() {
  const cuenta = useMiCuenta()
  const portal = useMiPortal()
  const salir = useSalirPortal()
  const navegar = useNavigate()
  const [pestana, setPestana] = useState<Pestana>('pendientes')

  if (cuenta.isError) return <Navigate to="/mi-portal/ingresar" replace />
  if (cuenta.isPending) return <main className="portal-ancho mp"><Esqueleto alto={40} ancho="50%" /><Esqueleto alto={200} style={{ marginTop: 16 }} /></main>

  const pendientes = portal.data?.pendientes ?? []
  const pestanas: [Pestana, string][] = [
    ['pendientes', `Pendientes${pendientes.length ? ` · ${pendientes.length}` : ''}`],
    ['postulaciones', `Mis postulaciones${portal.data ? ` · ${portal.data.postulaciones.length}` : ''}`],
    ['ofertas', 'Ofertas para ti'],
    ['hv', 'Mi hoja de vida'],
  ]

  return (
    <main className="portal-ancho mp">
      <header className="mp-cabeza">
        <div>
          <h1>Hola, {cuenta.data.datos.nombre.split(' ')[0]}</h1>
          <p className="postular-ayuda">{pendientes.length ? `Tienes ${pendientes.length} ${pendientes.length === 1 ? 'cosa pendiente' : 'cosas pendientes'}.` : 'No tienes nada pendiente.'}</p>
        </div>
        <Boton pequeno cargando={salir.isPending} textoCargando="Saliendo…" onClick={() => salir.mutate(undefined, { onSettled: () => navegar('/mi-portal/ingresar') })}>Salir</Boton>
      </header>

      {!cuenta.data.nombreArchivoHojaDeVida && pestana !== 'hv' && (
        <div className="bloque-ia mp-invitacion">
          <div className="bloque-ia-cabeza"><Chispa />Sube tu hoja de vida y la IA llena tu perfil</div>
          <div className="mp-invitacion-cuerpo">
            <span>Con tu hoja de vida te mostramos ofertas que encajan contigo y te postulas con un clic.</span>
            <Boton variante="ia" icono={<IconoSubir />} onClick={() => setPestana('hv')}>Subir mi hoja de vida</Boton>
          </div>
        </div>
      )}

      <div role="tablist" aria-label="Mi portal" className="pestanas mp-pestanas">
        {pestanas.map(([clave, texto]) => (
          <button key={clave} type="button" role="tab" className="pestana" aria-selected={pestana === clave} onClick={() => setPestana(clave)}>{texto}</button>
        ))}
      </div>

      {pestana === 'pendientes' && <Pendientes pendientes={pendientes} cargando={portal.isPending} />}
      {pestana === 'postulaciones' && <Postulaciones />}
      {pestana === 'ofertas' && <Ofertas />}
      {pestana === 'hv' && <MiHojaDeVida />}
    </main>
  )
}

const iconoPendiente: Record<Pendiente['tipo'], string> = { Chat: 'Chat', Pruebas: 'Pruebas', Oferta: 'Contratación' }

function Pendientes({ pendientes, cargando }: { pendientes: Pendiente[]; cargando: boolean }) {
  const abrir = useAbrirPendiente()
  const navegar = useNavigate()
  const [abriendo, setAbriendo] = useState<string>()

  if (cargando) return <div className="mp-lista">{[1, 2].map((i) => <Esqueleto key={i} alto={88} />)}</div>
  if (pendientes.length === 0) return <div className="tarjeta vacio mp-vacio"><IconoCheck /><p>Estás al día. Aquí te aparecen el chat de las empresas, tus pruebas y tu contrato cuando te los envíen.</p></div>

  return (
    <div className="mp-lista">
      {abrir.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{abrir.error.message}</p>}
      {pendientes.map((p) => (
        <article key={`${p.tipo}-${p.id}`} className="tarjeta mp-tarjeta mp-pendiente">
          <div>
            <span className="pill pill-alerta">{iconoPendiente[p.tipo]}</span>
            <h3>{p.titulo}</h3>
            <p>{p.vacante} · {p.empresa}{p.venceEn ? <> · vence el <span className="mono">{fechaHora.format(new Date(p.venceEn))}</span></> : null}</p>
          </div>
          <Boton variante="primario" cargando={abrir.isPending && abriendo === p.id} textoCargando="Abriendo…" disabled={abrir.isPending}
            onClick={() => { setAbriendo(p.id); abrir.mutate(p, { onSuccess: (r) => navegar(r.ruta) }) }}>
            Abrir <IconoFlecha />
          </Boton>
        </article>
      ))}
    </div>
  )
}

const claseEstado = (estado: string) =>
  estado === 'Contratada' || estado === 'Finalista' ? 'pill-ok' : estado === 'No seleccionada' || estado.includes('cerrad') ? 'pill-neutra' : 'pill-info'

function Postulaciones() {
  const portal = useMiPortal()
  const [abierta, setAbierta] = useState<string>()
  if (portal.isPending) return <div className="mp-lista">{[1, 2, 3].map((i) => <Esqueleto key={i} alto={72} />)}</div>
  const lista = portal.data?.postulaciones ?? []
  if (lista.length === 0) {
    return (
      <div className="tarjeta vacio mp-vacio">
        <p>Aún no vemos postulaciones con tu correo y tu identificación. Si te postulaste con otros datos, escríbenos desde <Link to="/empleos/mis-datos">tus datos personales</Link>.</p>
        <Link to="/empleos" className="boton boton-secundario">Ver vacantes</Link>
      </div>
    )
  }
  return (
    <div className="mp-lista">
      {lista.map((p) => (
        <article key={p.postulacionId} className="tarjeta mp-tarjeta mp-postulacion">
          <div className="mp-postulacion-fila">
            <div>
              <h3><Link to={`/empleos/${p.vacanteCodigo}`}>{p.vacante}</Link></h3>
              <p>{p.empresa} · {p.ciudad} · <span className="mono">{p.numero}</span> · {fecha.format(new Date(p.recibidaEn))}</p>
              <p className="mp-explicacion">{p.explicacion}</p>
              <button type="button" className="enlace-boton" aria-expanded={abierta === p.postulacionId}
                onClick={() => setAbierta((x) => (x === p.postulacionId ? undefined : p.postulacionId))}>
                {abierta === p.postulacionId ? 'Ocultar el chat' : 'Ver el chat con la empresa'}
              </button>
            </div>
            <span className={`pill ${claseEstado(p.estado)}`}>{p.estado}</span>
          </div>
          {abierta === p.postulacionId && <HistorialChat postulacionId={p.postulacionId} />}
        </article>
      ))}
    </div>
  )
}

/** Lo que se habló en la preselección: el asistente, la persona de la empresa y la persona misma. */
function HistorialChat({ postulacionId }: { postulacionId: string }) {
  const chat = useChatDePostulacion(postulacionId, true)
  if (chat.isPending) return <div className="mp-chat"><Esqueleto alto={48} /><Esqueleto alto={48} /></div>
  const mensajes = chat.data?.mensajes ?? []
  if (mensajes.length === 0) return <p className="postular-nota-suave mp-chat">Todavía no hay chat en esta postulación.</p>
  return (
    <ol className="mp-chat" aria-label="Historial del chat">
      {mensajes.map((m, i) => (
        <li key={i} className={m.esMio ? 'mio' : ''}>
          <span className="mp-chat-autor">{m.autor} · <span className="mono">{fechaHora.format(new Date(m.en))}</span></span>
          <p>{m.texto}</p>
        </li>
      ))}
    </ol>
  )
}

function Ofertas() {
  const ofertas = useOfertasParaTi(true)
  const postular = usePostularmeConMiPerfil()
  const navegar = useNavigate()
  const [eligiendo, setEligiendo] = useState<string>()

  if (ofertas.isPending) return <div className="mp-lista">{[1, 2, 3].map((i) => <Esqueleto key={i} alto={110} />)}</div>
  if ((ofertas.data ?? []).length === 0) {
    return (
      <div className="tarjeta vacio mp-vacio">
        <p>Aún no encontramos vacantes que encajen con tu hoja de vida. Actualízala con tu experiencia y tus cursos, o mira todas las vacantes.</p>
        <Link to="/empleos" className="boton boton-secundario">Ver todas las vacantes</Link>
      </div>
    )
  }
  return (
    <div className="mp-lista">
      {postular.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{postular.error.message}</p>}
      {ofertas.data?.map((o) => (
        <article key={o.codigo} className="tarjeta mp-tarjeta">
          <div>
            <h3><Link to={`/empleos/${o.codigo}`}>{o.titulo}</Link></h3>
            <p>{o.empresa} · {o.ciudad} · {o.jornada} · <span className="mono">{rangoSalario(o.salarioMinimo, o.salarioMaximo)}</span></p>
            <ul className="mp-porque">{o.porque.map((r) => <li key={r}><IconoCheck />{r}</li>)}</ul>
          </div>
          <Boton variante="primario" cargando={postular.isPending && eligiendo === o.codigo} textoCargando="Preparando…" disabled={postular.isPending}
            onClick={() => { setEligiendo(o.codigo); postular.mutate(o.codigo, { onSuccess: (r) => navegar(`/empleos/${o.codigo}/postular?borrador=${r.id}`) }) }}>
            Postularme
          </Boton>
        </article>
      ))}
    </div>
  )
}

/**
 * Su hoja de vida: subir una nueva (hoja de vida o el PDF de LinkedIn) para que la IA la lea, o escribirla y corregirla a mano.
 * Lo que leyó la IA llena el formulario en violeta; nada se guarda hasta que la persona pulsa «Guardar mi hoja de vida».
 */
function MiHojaDeVida() {
  const cuenta = useMiCuenta()
  const guardar = useActualizarMiPerfil()
  const actualizarHv = useActualizarMiHojaDeVida()
  const [nuevaHv, setNuevaHv] = useState<string>()
  const [modo, setModo] = useState<'hv' | 'linkedin'>('hv')
  const hv = useHojaDeVidaPerfil(nuevaHv)
  const c = cuenta.data!
  const [p, setP] = useState<PerfilEditable>(() => ({
    nombre: c.datos.nombre, celular: c.datos.celular, ciudad: c.datos.ciudad, linkedIn: c.datos.linkedIn,
    experiencia: c.datos.experiencia, formacion: c.datos.formacion, cursos: c.datos.cursos, habilidades: c.datos.habilidades,
  }))
  const [deIa, setDeIa] = useState(false)
  const [ciudadCodigo, setCiudadCodigo] = useState<string | null>(c.datos.ciudad ? 'guardada' : null)
  const [guardado, setGuardado] = useState(false)
  const poner = (cambio: Partial<PerfilEditable>) => { setP((x) => ({ ...x, ...cambio })); setGuardado(false) }

  // Cuando la IA termina de leer la hoja de vida nueva, llena el formulario en violeta para revisarlo
  const leida = nuevaHv && hv.data && hv.data.estado !== 'Leyendo' ? hv.data : null
  const leyendo = Boolean(nuevaHv) && !leida
  const [aplicada, setAplicada] = useState<string>()
  if (leida?.datos && aplicada !== leida.id) {
    const d = leida.datos
    setAplicada(leida.id)
    setDeIa(true)
    setP((x) => ({
      nombre: x.nombre || d.nombre, celular: x.celular || d.celular, ciudad: x.ciudad || d.ciudad, linkedIn: x.linkedIn || d.linkedIn,
      experiencia: d.experiencia, formacion: d.formacion, cursos: d.cursos, habilidades: d.habilidades,
    }))
  }

  const enviar = () => {
    // Con una hoja de vida nueva, se guarda el archivo y lo corregido; si no, solo lo escrito a mano
    if (nuevaHv && leida) {
      actualizarHv.mutate({ borradorId: nuevaHv, ...p }, {
        onSuccess: () => guardar.mutate(p, { onSuccess: () => { setNuevaHv(undefined); setDeIa(false); setGuardado(true) } }),
      })
    } else {
      guardar.mutate(p, { onSuccess: () => { setDeIa(false); setGuardado(true) } })
    }
  }

  const error = actualizarHv.error ?? guardar.error
  const ocupado = actualizarHv.isPending || guardar.isPending
  const ia = deIa ? ' entrada-ia' : ''

  return (
    <div className="mp-lista">
      <section className="tarjeta mp-seccion">
        <h3>Tu hoja de vida</h3>
        <p className="postular-ayuda">
          {c.nombreArchivoHojaDeVida
            ? <>Tienes <b>{c.nombreArchivoHojaDeVida}</b>{c.hojaDeVidaActualizadaEn && <>, actualizada el {fecha.format(new Date(c.hojaDeVidaActualizadaEn))}</>}. Puedes subir una nueva o corregirla abajo.</>
            : 'Sube tu hoja de vida o el PDF de tu LinkedIn y la IA llena tu perfil. También puedes escribirla abajo desde cero.'}
        </p>
        {!nuevaHv && (
          <>
            <div className="postular-modos" role="tablist" aria-label="Qué quieres subir">
              <button type="button" role="tab" aria-selected={modo === 'hv'} onClick={() => setModo('hv')}><IconoDocumento />Hoja de vida</button>
              <button type="button" role="tab" aria-selected={modo === 'linkedin'} onClick={() => setModo('linkedin')}><span className="postular-in" aria-hidden="true">in</span>LinkedIn</button>
              <span className="postular-modos-indicador" style={{ translate: modo === 'hv' ? '0 0' : '100% 0' }} aria-hidden="true" />
            </div>
            {modo === 'linkedin' && (
              <ol className="postular-linkedin-pasos">
                <li><b>Abre tu perfil</b> en linkedin.com desde el computador.</li>
                <li>Toca <b>Más</b> y luego <b>Guardar en PDF</b>.</li>
                <li><b>Sube ese PDF aquí</b> y la IA lee tu experiencia.</li>
              </ol>
            )}
            <SubirHojaDeVida titulo="" ayuda={modo === 'linkedin' ? 'LinkedIn no deja leer tu perfil con solo el enlace; por eso usamos el PDF que LinkedIn genera para ti.' : ''} onSubida={setNuevaHv} />
          </>
        )}
        {leyendo && <LeyendoHojaDeVida />}
        {leida?.estado === 'Fallida' && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{leida.mensaje} Escríbela abajo.</p>}
        {leida?.datos && <p className="postular-ayuda"><span className="pill pill-ia"><Chispa />Leída por la IA</span> Revisa lo que está en violeta y guarda.</p>}
      </section>

      <form className="tarjeta mp-seccion" onSubmit={(e) => { e.preventDefault(); enviar() }}>
        <h3>Tus datos</h3>
        <div className="postular-grupo">
          <label className="postular-campo">Nombres y apellidos<input className={`entrada${ia}`} value={p.nombre} onChange={(e) => poner({ nombre: e.target.value })} required /></label>
          <label className="postular-campo">Celular (WhatsApp)<input className={`entrada${ia}`} inputMode="tel" value={p.celular} onChange={(e) => poner({ celular: e.target.value })} /></label>
          <label className="postular-campo">Ciudad donde vives
            <AutocompletarMunicipio valor={{ codigo: ciudadCodigo, ciudad: p.ciudad }}
              onElegir={(m, texto) => { poner({ ciudad: m?.municipio ?? texto }); setCiudadCodigo(m?.codigo ?? null) }} />
          </label>
          <label className="postular-campo">Perfil de LinkedIn <span className="postular-opcional">opcional</span>
            <input className="entrada" inputMode="url" placeholder="linkedin.com/in/tu-nombre" value={p.linkedIn ?? ''} onChange={(e) => poner({ linkedIn: e.target.value })} />
          </label>
        </div>

        <h3>Experiencia</h3>
        {p.experiencia.map((x, i) => (
          <div key={i} className={`mp-fila-editable${ia}`}>
            <input className="entrada" aria-label="Cargo" placeholder="Cargo" value={x.cargo} onChange={(e) => poner({ experiencia: p.experiencia.map((y, j) => (j === i ? { ...y, cargo: e.target.value } : y)) })} />
            <input className="entrada" aria-label="Empresa" placeholder="Empresa" value={x.empresa} onChange={(e) => poner({ experiencia: p.experiencia.map((y, j) => (j === i ? { ...y, empresa: e.target.value } : y)) })} />
            <input className="entrada mono" aria-label="Desde" placeholder="Desde (03/2021)" value={x.desde} onChange={(e) => poner({ experiencia: p.experiencia.map((y, j) => (j === i ? { ...y, desde: e.target.value } : y)) })} />
            <input className="entrada mono" aria-label="Hasta" placeholder="Hasta (o «Actual»)" value={x.hasta} onChange={(e) => poner({ experiencia: p.experiencia.map((y, j) => (j === i ? { ...y, hasta: e.target.value } : y)) })} />
            <button type="button" className="enlace-boton" onClick={() => poner({ experiencia: p.experiencia.filter((_, j) => j !== i) })}>Quitar</button>
          </div>
        ))}
        <button type="button" className="enlace-boton" onClick={() => poner({ experiencia: [...p.experiencia, { cargo: '', empresa: '', desde: '', hasta: '' }] })}>+ Agregar experiencia</button>

        <h3>Estudios</h3>
        {p.formacion.map((x, i) => (
          <div key={i} className={`mp-fila-editable mp-fila-estudio${ia}`}>
            <input className="entrada" aria-label="Título" placeholder="Título" value={x.titulo} onChange={(e) => poner({ formacion: p.formacion.map((y, j) => (j === i ? { ...y, titulo: e.target.value } : y)) })} />
            <input className="entrada" aria-label="Institución" placeholder="Institución" value={x.institucion} onChange={(e) => poner({ formacion: p.formacion.map((y, j) => (j === i ? { ...y, institucion: e.target.value } : y)) })} />
            <input className="entrada mono" aria-label="Año" placeholder="Año" value={x.anio} onChange={(e) => poner({ formacion: p.formacion.map((y, j) => (j === i ? { ...y, anio: e.target.value } : y)) })} />
            <button type="button" className="enlace-boton" onClick={() => poner({ formacion: p.formacion.filter((_, j) => j !== i) })}>Quitar</button>
          </div>
        ))}
        <button type="button" className="enlace-boton" onClick={() => poner({ formacion: [...p.formacion, { titulo: '', institucion: '', anio: '' }] })}>+ Agregar estudio</button>

        <div className="dos-campos">
          <label className="postular-campo">Cursos y certificaciones <span className="postular-opcional">uno por línea</span>
            <textarea className={`area-texto${ia}`} rows={5} value={p.cursos.join('\n')} onChange={(e) => poner({ cursos: e.target.value.split('\n') })} />
          </label>
          <label className="postular-campo">Habilidades <span className="postular-opcional">una por línea</span>
            <textarea className={`area-texto${ia}`} rows={5} value={p.habilidades.join('\n')} onChange={(e) => poner({ habilidades: e.target.value.split('\n') })} />
          </label>
        </div>

        <p className="postular-nota-suave">Tu correo (<b>{c.correo}</b>) y tu identificación (<span className="mono">{c.tipoDocumento} {c.datos.documento}</span>) unen tu cuenta con tus postulaciones; para cambiarlos, pídelo desde <Link to="/empleos/mis-datos">tus datos personales</Link>.</p>
        {error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{error.message}</p>}
        <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
          <Boton type="submit" variante="primario" icono={<IconoCheck />} cargando={ocupado} textoCargando="Guardando…" disabled={leyendo}>Guardar mi hoja de vida</Boton>
          {nuevaHv && <Boton onClick={() => { setNuevaHv(undefined); setDeIa(false) }} disabled={ocupado}>Descartar la hoja de vida nueva</Boton>}
          {guardado && <span className="pill pill-ok"><IconoCheck />Guardada</span>}
        </div>
      </form>
    </div>
  )
}
