import { type CSSProperties, type ReactNode, useState } from 'react'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { HojasCayendo } from '../../shared/ui/HojasCayendo'
import {
  Chispa, IconoAlerta, IconoBuscar, IconoCandado, IconoChat, IconoCheck, IconoDocumento, IconoEscudo, IconoFlecha, IconoInicio,
  IconoPrueba, IconoSalir, IconoSubir, IconoUbicacion, IconoUsuarios, IconoVacantes,
} from '../../shared/ui/Iconos'
import { useContador } from '../../shared/ui/animaciones'
import { rangoSalario } from '../portal/api'
import { HojaDeVidaPdf } from './HojaDeVidaPdf'
import { MiHojaDeVida } from './MiHojaDeVida'
import { type SeccionHv, faltantesPerfil, totalPasosPerfil } from './perfil'
import {
  type MiCuenta, type MiPostulacion, type OfertaParaTi, type Pendiente, useAbrirPendiente, useChatDePostulacion, useMiCuenta, useMiPortal,
  useOfertasParaTi, usePostularmeConMiPerfil, useSalirPortal,
} from './api'
import './portal-persona.css'

export { IngresarPortal, RegistroPortal } from './Acceso'

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' })
const fechaHora = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const relativo = new Intl.RelativeTimeFormat('es-CO', { numeric: 'auto' })

type Seccion = 'inicio' | 'procesos' | 'ofertas' | 'hv' | 'cuenta'
const secciones: { clave: Seccion; texto: string; corto: string; icono: ReactNode }[] = [
  { clave: 'inicio', texto: 'Inicio', corto: 'Inicio', icono: <IconoInicio /> },
  { clave: 'procesos', texto: 'Mis procesos', corto: 'Procesos', icono: <IconoVacantes /> },
  { clave: 'ofertas', texto: 'Ofertas para ti', corto: 'Ofertas', icono: <IconoBuscar /> },
  { clave: 'hv', texto: 'Mi hoja de vida', corto: 'Hoja de vida', icono: <IconoDocumento /> },
  { clave: 'cuenta', texto: 'Mi cuenta', corto: 'Cuenta', icono: <IconoUsuarios /> },
]

const saludo = () => {
  const h = new Date().getHours()
  return h < 12 ? 'Buenos días' : h < 19 ? 'Buenas tardes' : 'Buenas noches'
}

/* ---------- Mi portal ---------- */

export function MiPortal() {
  const cuenta = useMiCuenta()
  const portal = useMiPortal()
  const ofertas = useOfertasParaTi(cuenta.isSuccess)
  const [parametros, setParametros] = useSearchParams()
  const seccion = (secciones.some((s) => s.clave === parametros.get('ver')) ? parametros.get('ver') : 'inicio') as Seccion
  const parteHv = (parametros.get('parte') ?? undefined) as SeccionHv | undefined
  const [direccion, setDireccion] = useState<'adelante' | 'atras'>('adelante')
  const indice = secciones.findIndex((s) => s.clave === seccion)

  const ir = (s: Seccion, parte?: SeccionHv) => {
    setDireccion(secciones.findIndex((x) => x.clave === s) >= indice ? 'adelante' : 'atras')
    setParametros(s === 'inicio' ? {} : parte ? { ver: s, parte } : { ver: s })
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  if (cuenta.isError) return <Navigate to="/mi-portal/ingresar" replace />
  if (cuenta.isPending) return <EsqueletoPortal />

  const c = cuenta.data
  const pendientes = portal.data?.pendientes ?? []
  const postulaciones = portal.data?.postulaciones ?? []
  const insignias: Partial<Record<Seccion, number>> = { inicio: pendientes.length, ofertas: ofertas.data?.length }

  return (
    <main className="mp">
      <Bienvenida cuenta={c} pendientes={pendientes.length} enCurso={postulaciones.filter(enCurso).length} ofertas={ofertas.data?.length} cargando={portal.isPending} ir={ir} />

      <div className="portal-ancho mp-cuerpo">
        <nav className="mp-nav" role="tablist" aria-label="Mi portal" style={{ '--n': secciones.length, '--activa': indice } as CSSProperties}>
          <span className="mp-nav-indicador" aria-hidden="true" />
          {secciones.map((s) => (
            <button key={s.clave} type="button" role="tab" aria-selected={seccion === s.clave} onClick={() => ir(s.clave)}>
              <span className="mp-nav-icono">{s.icono}{insignias[s.clave] ? <span className="mp-nav-insignia mono">{insignias[s.clave]}</span> : null}</span>
              <span className="mp-nav-texto">{s.texto}</span>
              <span className="mp-nav-corto">{s.corto}</span>
            </button>
          ))}
        </nav>

        <div key={seccion} role="tabpanel" className={`mp-panel mp-panel-${direccion}`}>
          {seccion === 'inicio' && <Inicio cuenta={c} pendientes={pendientes} postulaciones={postulaciones} ofertas={ofertas.data} cargando={portal.isPending} ir={ir} />}
          {seccion === 'procesos' && <Procesos postulaciones={postulaciones} cargando={portal.isPending} />}
          {seccion === 'ofertas' && <Ofertas ofertas={ofertas.data} cargando={ofertas.isPending} ir={ir} />}
          {seccion === 'hv' && <MiHojaDeVida cuenta={c} seccionInicial={parteHv} />}
          {seccion === 'cuenta' && <MiCuentaPanel cuenta={c} />}
        </div>
      </div>
    </main>
  )
}

function EsqueletoPortal() {
  return (
    <main className="mp" aria-busy="true">
      <section className="mp-hero"><div className="portal-ancho mp-hero-fila"><div className="mp-hero-texto"><Esqueleto alto={14} ancho={120} /><Esqueleto alto={44} ancho="60%" /><Esqueleto alto={16} ancho="40%" /></div></div></section>
      <div className="portal-ancho mp-cuerpo"><Esqueleto alto={52} /><Esqueleto alto={120} /><Esqueleto alto={120} /></div>
    </main>
  )
}

/* ---------- Bienvenida: saludo, perfil y cifras que llevan a cada sección ---------- */

function Bienvenida({ cuenta, pendientes, enCurso, ofertas, cargando, ir }: {
  cuenta: MiCuenta; pendientes: number; enCurso: number; ofertas?: number; cargando: boolean; ir: (s: Seccion, parte?: SeccionHv) => void
}) {
  const navegar = useNavigate()
  const [buscar, setBuscar] = useState('')
  const nombre = cuenta.datos.nombre.split(' ')[0]
  const completo = Math.round(((totalPasosPerfil - faltantesPerfil(cuenta).length) / totalPasosPerfil) * 100)
  const cifras: { valor?: number; texto: string; seccion: Seccion }[] = [
    { valor: cargando ? undefined : pendientes, texto: pendientes === 1 ? 'pendiente por hacer' : 'pendientes por hacer', seccion: 'inicio' },
    { valor: cargando ? undefined : enCurso, texto: enCurso === 1 ? 'proceso en curso' : 'procesos en curso', seccion: 'procesos' },
    { valor: ofertas, texto: ofertas === 1 ? 'oferta para ti' : 'ofertas para ti', seccion: 'ofertas' },
  ]

  return (
    <section className="mp-hero">
      <HojasCayendo cantidad={7} className="hojas-claras" />
      <div className="portal-ancho mp-hero-fila">
        <div className="mp-hero-texto">
          <span className="mp-hero-ceja">{saludo()}</span>
          <h1>{['Hola,', `${nombre}`].map((p, i) => <span key={i} className="mp-palabra" style={{ '--i': i } as CSSProperties}>{p} </span>)}</h1>
          <p>{pendientes ? `Tienes ${pendientes} ${pendientes === 1 ? 'cosa' : 'cosas'} por hacer hoy.` : 'Estás al día. Mira las ofertas que encajan contigo.'}</p>
        </div>
        <button type="button" className="mp-anillo" onClick={() => ir('hv')} aria-label={`Tu perfil está al ${completo} %. Ir a tu hoja de vida`}>
          <Anillo porcentaje={completo} />
          <span>{completo === 100 ? 'Perfil completo' : 'Tu perfil'}</span>
        </button>
      </div>
      <div className="portal-ancho mp-cifras">
        <form className="mp-hero-buscar" role="search" onSubmit={(e) => { e.preventDefault(); navegar(buscar.trim() ? `/empleos?buscar=${encodeURIComponent(buscar.trim())}` : '/empleos') }}>
          <IconoBuscar />
          <input value={buscar} onChange={(e) => setBuscar(e.target.value)} placeholder="Busca otras vacantes: cargo, empresa o ciudad" aria-label="Buscar vacantes" />
          <button type="submit" aria-label="Buscar"><IconoFlecha /></button>
        </form>
        {cifras.map((x, i) => (
          <button key={x.seccion} type="button" className="mp-cifra" style={{ '--i': i } as CSSProperties} onClick={() => ir(x.seccion)}>
            {x.valor === undefined ? <Esqueleto alto={22} ancho={28} /> : <Cifra valor={x.valor} />}
            <span>{x.texto}</span>
            <IconoFlecha className="mp-cifra-flecha" />
          </button>
        ))}
      </div>
      <svg className="mp-hero-onda" viewBox="0 0 1440 48" preserveAspectRatio="none" aria-hidden="true"><path d="M0 30C240 6 480 6 720 22s480 26 720 4V48H0Z" /></svg>
    </section>
  )
}

function Cifra({ valor }: { valor: number }) {
  return <b className="mp-cifra-numero">{useContador(0, valor, true, 900)}</b>
}

function Anillo({ porcentaje }: { porcentaje: number }) {
  const valor = useContador(0, porcentaje, true, 1200)
  const r = 30
  const largo = 2 * Math.PI * r
  return (
    <span className="mp-anillo-svg">
      <svg viewBox="0 0 72 72" aria-hidden="true">
        <circle cx="36" cy="36" r={r} className="mp-anillo-fondo" />
        <circle cx="36" cy="36" r={r} className="mp-anillo-valor" strokeDasharray={largo} strokeDashoffset={largo * (1 - valor / 100)} />
      </svg>
      <b className="mono">{valor}<small>%</small></b>
    </span>
  )
}

/* ---------- Inicio: lo que hay que hacer hoy primero, luego procesos, perfil y ofertas ---------- */

function Inicio({ cuenta, pendientes, postulaciones, ofertas, cargando, ir }: {
  cuenta: MiCuenta; pendientes: Pendiente[]; postulaciones: MiPostulacion[]; ofertas?: OfertaParaTi[]; cargando: boolean; ir: (s: Seccion, parte?: SeccionHv) => void
}) {
  const faltan = faltantesPerfil(cuenta)
  const activos = postulaciones.filter(enCurso).slice(0, 3)
  return (
    <div className="mp-inicio">
      <div className="mp-col">
        {!cuenta.nombreArchivoHojaDeVida && (
          <div className="bloque-ia mp-invitacion">
            <div className="bloque-ia-cabeza"><Chispa />Sube tu hoja de vida y la IA llena tu perfil</div>
            <div className="mp-invitacion-cuerpo">
              <span>Con tu hoja de vida te mostramos ofertas que encajan contigo y te postulas con un clic.</span>
              <Boton variante="ia" icono={<IconoSubir />} onClick={() => ir('hv')}>Subir mi hoja de vida</Boton>
            </div>
          </div>
        )}

        <Bloque titulo="Para hacer hoy" cuenta={pendientes.length || undefined}>
          <Pendientes pendientes={pendientes} cargando={cargando} />
        </Bloque>

        <Bloque titulo="Tus procesos en curso" accion={postulaciones.length ? <button type="button" className="enlace-boton" onClick={() => ir('procesos')}>Ver todos</button> : undefined}>
          {cargando ? <Esqueleto alto={84} /> : activos.length === 0
            ? <Vacio texto="No tienes procesos en curso." accion={<Link to="/empleos" className="boton boton-secundario">Ver vacantes</Link>} />
            : <ul className="mp-mini-procesos">{activos.map((p, i) => <MiniProceso key={p.postulacionId} p={p} i={i} onAbrir={() => ir('procesos')} />)}</ul>}
        </Bloque>
      </div>

      <aside className="mp-col">
        {faltan.length > 0 && (
          <Bloque titulo="Completa tu perfil" cuenta={`${totalPasosPerfil - faltan.length}/${totalPasosPerfil}`}>
            <p className="mp-bloque-ayuda">Un perfil completo encaja con más ofertas.</p>
            <ul className="mp-faltan">
              {faltan.map((f, i) => (
                <li key={f.texto} style={{ '--i': i } as CSSProperties}>
                  <button type="button" onClick={() => ir('hv', f.seccion === 'archivo' ? undefined : f.seccion)}><span className="mp-faltan-punto" />{f.texto}<IconoFlecha /></button>
                </li>
              ))}
            </ul>
          </Bloque>
        )}
        <Bloque titulo="Ofertas para ti" accion={ofertas?.length ? <button type="button" className="enlace-boton" onClick={() => ir('ofertas')}>Ver todas</button> : undefined}>
          {!ofertas ? <Esqueleto alto={120} /> : ofertas.length === 0
            ? <Vacio texto="Aún no hay ofertas que encajen con tu perfil." accion={<button type="button" className="enlace-boton" onClick={() => ir('hv')}>Completar mi hoja de vida</button>} />
            : (
              <ul className="mp-mini-ofertas">
                {ofertas.slice(0, 3).map((o, i) => (
                  <li key={o.codigo} style={{ '--i': i } as CSSProperties}>
                    <Link to={`/empleos/${o.codigo}`}>
                      <b>{o.titulo}</b>
                      <span>{o.empresa} · {o.ciudad}</span>
                      <span className="mono">{rangoSalario(o.salarioMinimo, o.salarioMaximo)}</span>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
        </Bloque>
      </aside>
    </div>
  )
}

function Bloque({ titulo, cuenta, accion, children }: { titulo: string; cuenta?: number | string; accion?: ReactNode; children: ReactNode }) {
  return (
    <section className="mp-bloque">
      <header><h2>{titulo}{cuenta !== undefined && <span className="mp-bloque-cuenta mono">{cuenta}</span>}</h2>{accion}</header>
      {children}
    </section>
  )
}

function Vacio({ texto, accion, icono }: { texto: string; accion?: ReactNode; icono?: ReactNode }) {
  return <div className="mp-vacio">{icono}<p>{texto}</p>{accion}</div>
}

const tipoPendiente: Record<Pendiente['tipo'], { texto: string; icono: ReactNode }> = {
  Chat: { texto: 'Responder el chat', icono: <IconoChat /> },
  Pruebas: { texto: 'Presentar pruebas', icono: <IconoPrueba /> },
  Oferta: { texto: 'Revisar tu oferta', icono: <IconoDocumento /> },
}

function vence(en: string) {
  const ms = new Date(en).getTime() - Date.now()
  const horas = ms / 36e5
  const texto = Math.abs(horas) < 24 ? relativo.format(Math.round(horas), 'hour') : relativo.format(Math.round(horas / 24), 'day')
  return { texto: `Vence ${texto}`, urgente: horas < 48 }
}

function Pendientes({ pendientes, cargando }: { pendientes: Pendiente[]; cargando: boolean }) {
  const abrir = useAbrirPendiente()
  const navegar = useNavigate()
  const [abriendo, setAbriendo] = useState<string>()

  if (cargando) return <div className="mp-lista">{[1, 2].map((i) => <Esqueleto key={i} alto={92} />)}</div>
  if (pendientes.length === 0) {
    return <Vacio icono={<span className="mp-al-dia"><IconoCheck /></span>} texto="Estás al día. Aquí te aparecen el chat de las empresas, tus pruebas y tu contrato cuando te los envíen." />
  }
  return (
    <div className="mp-lista">
      {abrir.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{abrir.error.message}</p>}
      {pendientes.map((p, i) => {
        const v = p.venceEn ? vence(p.venceEn) : null
        return (
          <article key={`${p.tipo}-${p.id}`} className={`mp-pendiente mp-pendiente-${p.tipo.toLowerCase()}`} style={{ '--i': i } as CSSProperties}>
            <span className="mp-pendiente-icono">{tipoPendiente[p.tipo].icono}</span>
            <div className="mp-pendiente-texto">
              <span className="mp-pendiente-tipo">{tipoPendiente[p.tipo].texto}</span>
              <h3>{p.titulo}</h3>
              <p>{p.vacante} · {p.empresa}</p>
              {v && p.venceEn && <span className={`pill ${v.urgente ? 'pill-alerta' : 'pill-neutra'}`} title={fechaHora.format(new Date(p.venceEn))}>{v.texto}</span>}
            </div>
            <Boton variante="primario" cargando={abrir.isPending && abriendo === p.id} textoCargando="Abriendo…" disabled={abrir.isPending}
              onClick={() => { setAbriendo(p.id); abrir.mutate(p, { onSuccess: (r) => navegar(r.ruta) }) }}>
              Abrir <IconoFlecha />
            </Boton>
          </article>
        )
      })}
    </div>
  )
}

/* ---------- Procesos: avance en palabras de la persona y chat ---------- */

/** Avance del proceso; las postulaciones cerradas o no seleccionadas no muestran avance. */
const etapasAvance = ['Recibida', 'En revisión', 'Preselección', 'Finalista', 'Contratación'] as const
const indiceAvance: Record<string, number> = {
  Recibida: 0, 'En revisión': 1, 'En preselección': 2, Pruebas: 2, Finalista: 3, 'En contratación': 4, Contratada: 5,
}
const enCurso = (p: MiPostulacion) => indiceAvance[p.estado] !== undefined && p.estado !== 'Contratada'
const claseEstado = (estado: string) =>
  estado === 'Contratada' || estado === 'Finalista' ? 'pill-ok' : estado === 'No seleccionada' || estado.includes('cerrad') ? 'pill-neutra' : 'pill-info'

function MiniProceso({ p, i, onAbrir }: { p: MiPostulacion; i: number; onAbrir: () => void }) {
  const avance = indiceAvance[p.estado] ?? 0
  return (
    <li style={{ '--i': i } as CSSProperties}>
      <button type="button" onClick={onAbrir}>
        <span className="mp-mini-titulo"><b>{p.vacante}</b><span className={`pill ${claseEstado(p.estado)}`}>{p.estado}</span></span>
        <span className="mp-mini-meta">{p.empresa}</span>
        <span className="mp-mini-barra" aria-hidden="true"><span style={{ '--avance': `${Math.min(100, (avance / (etapasAvance.length - 1)) * 100)}%` } as CSSProperties} /></span>
      </button>
    </li>
  )
}

type Filtro = 'curso' | 'cerradas' | 'todas'

function Procesos({ postulaciones, cargando }: { postulaciones: MiPostulacion[]; cargando: boolean }) {
  const [filtro, setFiltro] = useState<Filtro>('curso')
  const [abierta, setAbierta] = useState<string>()
  if (cargando) return <div className="mp-lista">{[1, 2].map((i) => <Esqueleto key={i} alto={170} />)}</div>
  if (postulaciones.length === 0) {
    return (
      <Vacio texto="Aún no vemos postulaciones con tu correo y tu identificación. Si te postulaste con otros datos, pídelo desde tus datos personales."
        accion={<div className="mp-vacio-acciones"><Link to="/empleos" className="boton boton-primario">Ver vacantes</Link><Link to="/empleos/mis-datos" className="boton boton-secundario">Tus datos personales</Link></div>} />
    )
  }
  const cuentas: Record<Filtro, number> = {
    curso: postulaciones.filter(enCurso).length, cerradas: postulaciones.filter((p) => !enCurso(p)).length, todas: postulaciones.length,
  }
  const lista = postulaciones.filter((p) => (filtro === 'todas' ? true : filtro === 'curso' ? enCurso(p) : !enCurso(p)))

  return (
    <div className="mp-lista">
      <div className="segmentado mp-filtro" role="group" aria-label="Qué procesos ver">
        {([['curso', 'En curso'], ['cerradas', 'Terminados'], ['todas', 'Todos']] as [Filtro, string][]).map(([f, t]) => (
          <button key={f} type="button" aria-pressed={filtro === f} onClick={() => setFiltro(f)}>{t} <span className="mono">{cuentas[f]}</span></button>
        ))}
      </div>
      {lista.length === 0 && <Vacio texto={filtro === 'curso' ? 'No tienes procesos en curso ahora.' : 'Aún no tienes procesos terminados.'} />}
      {lista.map((p, i) => {
        const avance = indiceAvance[p.estado]
        const chatAbierto = abierta === p.postulacionId
        return (
          <article key={p.postulacionId} className="mp-proceso" style={{ '--i': i } as CSSProperties}>
            <header className="mp-proceso-cabeza">
              <div>
                <h3><Link to={`/empleos/${p.vacanteCodigo}`}>{p.vacante}</Link></h3>
                <p>{p.empresa} · <IconoUbicacion />{p.ciudad} · <span className="mono">{p.numero}</span> · {fecha.format(new Date(p.recibidaEn))}</p>
              </div>
              <span className={`pill ${claseEstado(p.estado)}`}>{p.estado}</span>
            </header>

            {avance !== undefined && (
              <ol className="mp-etapas" aria-label={`Avance: ${p.estado}`} style={{ '--avance': `${Math.min(100, (avance / (etapasAvance.length - 1)) * 100)}%` } as CSSProperties}>
                {etapasAvance.map((e, j) => (
                  <li key={e} className={j < avance ? 'hecho' : j === avance ? 'actual' : ''} style={{ '--j': j } as CSSProperties}>
                    <span className="mp-etapa-punto">{j < avance ? <IconoCheck /> : null}</span>
                    <span className="mp-etapa-texto">{e}</span>
                  </li>
                ))}
              </ol>
            )}

            <p className="mp-proceso-explicacion">{p.explicacion}</p>
            <footer className="mp-proceso-pie">
              <button type="button" className="mp-chat-boton" aria-expanded={chatAbierto} onClick={() => setAbierta(chatAbierto ? undefined : p.postulacionId)}>
                <IconoChat />{chatAbierto ? 'Ocultar el chat' : 'Ver el chat'}
              </button>
            </footer>
            <div className={`mp-plegable ${chatAbierto ? 'abierto' : ''}`}>
              <div>{chatAbierto && <HistorialChat postulacionId={p.postulacionId} />}</div>
            </div>
          </article>
        )
      })}
    </div>
  )
}

/** Lo que se habló en la preselección: el asistente, la persona de la empresa y la persona misma. */
function HistorialChat({ postulacionId }: { postulacionId: string }) {
  const chat = useChatDePostulacion(postulacionId, true)
  if (chat.isPending) return <div className="mp-chat"><Esqueleto alto={48} ancho="70%" /><Esqueleto alto={48} ancho="60%" style={{ alignSelf: 'flex-end' }} /></div>
  const mensajes = chat.data?.mensajes ?? []
  if (mensajes.length === 0) return <p className="mp-nota mp-chat">Todavía no hay chat en esta postulación.</p>
  return (
    <ol className="mp-chat" aria-label="Historial del chat">
      {mensajes.map((m, i) => (
        <li key={i} className={m.esMio ? 'mio' : ''} style={{ '--i': Math.min(i, 8) } as CSSProperties}>
          <span className="mp-chat-autor">{m.autor} · <span className="mono">{fechaHora.format(new Date(m.en))}</span></span>
          <p>{m.texto}</p>
        </li>
      ))}
    </ol>
  )
}

/* ---------- Ofertas para ti: vacantes que encajan, con el porqué (reglas) ---------- */

function Ofertas({ ofertas, cargando, ir }: { ofertas?: OfertaParaTi[]; cargando: boolean; ir: (s: Seccion) => void }) {
  const postular = usePostularmeConMiPerfil()
  const navegar = useNavigate()
  const [eligiendo, setEligiendo] = useState<string>()

  if (cargando || !ofertas) return <div className="mp-ofertas">{[1, 2, 3, 4].map((i) => <Esqueleto key={i} alto={210} />)}</div>
  if (ofertas.length === 0) {
    return (
      <Vacio texto="Aún no encontramos vacantes que encajen con tu hoja de vida. Actualízala con tu experiencia y tus cursos, o mira todas las vacantes."
        accion={<div className="mp-vacio-acciones"><Boton onClick={() => ir('hv')}>Actualizar mi hoja de vida</Boton><Link to="/empleos" className="boton boton-primario">Ver todas las vacantes</Link></div>} />
    )
  }
  return (
    <>
      {postular.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{postular.error.message}</p>}
      <p className="mp-bloque-ayuda">Las elegimos comparando tu hoja de vida con los requisitos de cada vacante.</p>
      <div className="mp-ofertas">
        {ofertas.map((o, i) => (
          <article key={o.codigo} className="mp-oferta" style={{ '--i': i } as CSSProperties}>
            <span className="mp-oferta-empresa">{o.empresa}</span>
            <h3><Link to={`/empleos/${o.codigo}`}>{o.titulo}</Link></h3>
            <div className="mp-oferta-datos">
              <span><IconoUbicacion />{o.ciudad}</span>
              <span>{o.jornada}</span>
              <span className="mono">{rangoSalario(o.salarioMinimo, o.salarioMaximo)}</span>
            </div>
            <div className="mp-oferta-porque">
              <span>Por qué encaja contigo</span>
              <ul>{o.porque.map((r) => <li key={r}><IconoCheck />{r}</li>)}</ul>
            </div>
            <footer>
              <Link to={`/empleos/${o.codigo}`} className="enlace-boton">Ver la vacante</Link>
              <Boton variante="primario" pequeno cargando={postular.isPending && eligiendo === o.codigo} textoCargando="Preparando…" disabled={postular.isPending}
                onClick={() => { setEligiendo(o.codigo); postular.mutate(o.codigo, { onSuccess: (r) => navegar(`/empleos/${o.codigo}/postular?borrador=${r.id}`) }) }}>
                Postularme <IconoFlecha />
              </Boton>
            </footer>
          </article>
        ))}
      </div>
    </>
  )
}

/* ---------- Mi cuenta: hoja de vida en PDF, autorización y derechos sobre los datos, salir ---------- */

function MiCuentaPanel({ cuenta }: { cuenta: MiCuenta }) {
  const salir = useSalirPortal()
  const navegar = useNavigate()
  const [verCv, setVerCv] = useState(false)
  const iniciales = cuenta.datos.nombre.split(' ').filter(Boolean).slice(0, 2).map((x) => x[0]?.toUpperCase()).join('')

  return (
    <div className="mp-cuenta">
      <section className="mp-tarjeta-cuenta mp-perfil-tarjeta">
        <span className="mp-avatar" aria-hidden="true">{iniciales}</span>
        <div>
          <h2>{cuenta.datos.nombre}</h2>
          <p>{cuenta.correo} · <span className="mono">{cuenta.tipoDocumento} {cuenta.datos.documento}</span></p>
          <p className="mp-nota">Con nosotros desde el {fecha.format(new Date(cuenta.creadaEn))}</p>
        </div>
      </section>

      <section className="mp-tarjeta-cuenta">
        <span className="mp-cuenta-icono"><IconoDocumento /></span>
        <div>
          <h3>Tu hoja de vida en PDF</h3>
          <p>Descárgala con un diseño limpio para enviarla por correo o llevarla impresa. Sale de lo que guardaste en tu perfil.</p>
        </div>
        <Boton onClick={() => setVerCv(true)}>Ver y descargar</Boton>
      </section>

      <section className="mp-tarjeta-cuenta">
        <span className="mp-cuenta-icono"><IconoEscudo /></span>
        <div>
          <h3>Tu autorización de datos</h3>
          <p>Autorizaste el tratamiento de tus datos el <b>{fecha.format(new Date(cuenta.autorizadaEn))}</b>, versión <span className="mono">{cuenta.versionAutorizacion}</span>, según la Ley 1581 de 2012.</p>
          <Link to="/legal/tratamiento-de-datos">Leer la política de datos</Link>
        </div>
      </section>

      <section className="mp-tarjeta-cuenta">
        <span className="mp-cuenta-icono"><IconoCandado /></span>
        <div>
          <h3>Tus derechos sobre tus datos</h3>
          <p>Puedes consultar, corregir o pedir que borremos tus datos, y cambiar tu correo o tu identificación. Te respondemos dentro de los plazos de ley.</p>
        </div>
        <Link to="/empleos/mis-datos" className="boton boton-secundario">Hacer una solicitud</Link>
      </section>

      <section className="mp-tarjeta-cuenta mp-salir">
        <span className="mp-cuenta-icono"><IconoSalir /></span>
        <div><h3>Salir de tu portal</h3><p>Cierra la sesión en este dispositivo.</p></div>
        <Boton cargando={salir.isPending} textoCargando="Saliendo…" onClick={() => salir.mutate(undefined, { onSettled: () => navegar('/mi-portal/ingresar') })}>Salir</Boton>
      </section>

      {verCv && <HojaDeVidaPdf cuenta={cuenta} onCerrar={() => setVerCv(false)} />}
    </div>
  )
}
