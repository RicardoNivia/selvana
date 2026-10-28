import { type CSSProperties, type FormEvent, type ReactNode, useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { HojasCayendo } from '../../shared/ui/HojasCayendo'
import { IconoBuscar, IconoCheck, IconoCupos, IconoDinero, IconoDocumento, IconoFlecha, IconoReloj, IconoUbicacion } from '../../shared/ui/Iconos'
import { useContador, useMovimientoReducido } from '../../shared/ui/animaciones'
import { Paginacion } from '../../shared/ui/Paginacion'
import { TAMANO_PAGINA } from '../../shared/ui/paginado'
import { useMiCuenta, useMiPortal } from '../portal-persona/api'
import { faltantesPerfil, totalPasosPerfil } from '../portal-persona/perfil'
import { type Conteo, type VacantePublica, rangoSalario, usePortal } from './api'

const relativo = new Intl.RelativeTimeFormat('es-CO', { numeric: 'auto' })
const diasDesde = (iso: string) => Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000))
const haceCuanto = (iso: string) => relativo.format(-diasDesde(iso), 'day')
const iniciales = (nombre: string) => nombre.split(/\s+/).filter((p) => p.length > 2).slice(0, 2).map((p) => p[0]).join('').toUpperCase()
const v = (i: number) => ({ '--i': i }) as CSSProperties

const oficiosRotando = ['auxiliar de bodega', 'asesor de servicio', 'auxiliar de enfermería', 'montacarguista', 'cajero', 'operario']
const busquedasRapidas = ['Bodega', 'Servicio al cliente', 'Enfermería', 'Ventas', 'Conductor', 'Producción']

/** Portal público: todas las vacantes publicadas. Los filtros viven en la URL para poder compartir la búsqueda. */
export function PortalListado() {
  const [parametros, setParametros] = useSearchParams()
  const filtro = {
    pagina: Number(parametros.get('pagina') ?? '1') || 1,
    tamano: Number(parametros.get('tamano') ?? TAMANO_PAGINA) || TAMANO_PAGINA,
    buscar: parametros.get('buscar') ?? '',
    ciudad: parametros.get('ciudad') ?? '',
    jornada: parametros.get('jornada') ?? '',
  }
  const portal = usePortal(filtro)
  const datos = portal.data
  const resultados = useRef<HTMLElement>(null)

  const cambiar = (cambios: Partial<typeof filtro>, irAResultados = false) => {
    const siguiente = { ...filtro, pagina: 1, ...cambios }
    const q = new URLSearchParams()
    if (siguiente.buscar) q.set('buscar', siguiente.buscar)
    if (siguiente.ciudad) q.set('ciudad', siguiente.ciudad)
    if (siguiente.jornada) q.set('jornada', siguiente.jornada)
    if (siguiente.pagina > 1) q.set('pagina', String(siguiente.pagina))
    if (siguiente.tamano !== TAMANO_PAGINA) q.set('tamano', String(siguiente.tamano))
    setParametros(q, { replace: true })
    if (irAResultados) resultados.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }

  const hayFiltros = Boolean(filtro.buscar || filtro.ciudad || filtro.jornada)
  // La clave cambia con cada búsqueda: las tarjetas vuelven a entrar escalonadas
  const claveResultados = `${filtro.buscar}|${filtro.ciudad}|${filtro.jornada}|${filtro.pagina}|${datos ? 'datos' : 'cargando'}`

  return (
    <>
      <Hero total={datos?.vacantes.total} empresas={datos?.empresas} buscar={filtro.buscar} onBuscar={(t) => cambiar({ buscar: t }, true)} />

      <main className="portal-ancho portal-resultados" ref={resultados}>
        {!portal.isError && <div className="portal-barra-filtros" role="toolbar" aria-label="Filtros">
          <Chips titulo="Ciudad" icono={<IconoUbicacion />} opciones={datos?.ciudades} activo={filtro.ciudad} onElegir={(c) => cambiar({ ciudad: c === filtro.ciudad ? '' : c })} />
          <Chips titulo="Jornada" icono={<IconoReloj />} opciones={datos?.jornadas} activo={filtro.jornada} onElegir={(j) => cambiar({ jornada: j === filtro.jornada ? '' : j })} />
        </div>}

        <div className="portal-resumen" aria-live="polite">
          {datos ? (
            <span>
              <b className="mono">{datos.vacantes.total}</b> {datos.vacantes.total === 1 ? 'vacante' : 'vacantes'}
              {filtro.buscar && <> para «{filtro.buscar}»</>}
              {filtro.ciudad && <> en {filtro.ciudad}</>}
            </span>
          ) : !portal.isError && <Esqueleto alto={16} ancho={180} />}
          {hayFiltros && <button type="button" className="portal-limpiar" onClick={() => cambiar({ buscar: '', ciudad: '', jornada: '' })}>Limpiar filtros ×</button>}
          {portal.isFetching && datos && <span className="indicador portal-actualizando" aria-label="Actualizando" />}
        </div>

        {portal.isError && <p role="alert" className="aviso aviso-alerta">No pudimos cargar las vacantes. Intenta de nuevo en un momento.</p>}

        <section aria-label="Vacantes" className={`portal-rejilla ${portal.isPlaceholderData ? 'portal-rejilla-cargando' : ''}`} key={claveResultados}>
          {portal.isPending
            ? [0, 1, 2, 3, 4, 5].map((i) => <TarjetaEsqueleto key={i} />)
            : datos?.vacantes.items.map((vacante, i) => <TarjetaVacante key={vacante.codigo} vacante={vacante} i={i} />)}
        </section>

        {datos && datos.vacantes.total === 0 && (
          <div className="portal-vacio">
            <svg viewBox="0 0 48 48" aria-hidden="true" className="portal-vacio-hoja"><path d="M24 24C24 12 31 5 43 5C43 17 36 24 24 24Z" /><path d="M24 24C24 36 17 43 5 43C5 31 12 24 24 24Z" /></svg>
            <h2>No encontramos vacantes con esa búsqueda</h2>
            <p>Prueba otra palabra o quita algún filtro. Cada semana se publican vacantes nuevas.</p>
            {hayFiltros && <button type="button" className="boton boton-secundario" onClick={() => cambiar({ buscar: '', ciudad: '', jornada: '' })}>Ver todas las vacantes</button>}
          </div>
        )}

        {datos && datos.vacantes.total > TAMANO_PAGINA && (
          <div className="portal-paginacion">
            <Paginacion pagina={datos.vacantes} cargando={portal.isPlaceholderData} onCambiar={(n) => cambiar({ pagina: n }, true)} onCambiarTamano={(t) => cambiar({ tamano: t }, true)} />
          </div>
        )}
      </main>
    </>
  )
}

function Hero({ total, empresas, buscar, onBuscar }: { total?: number; empresas?: number; buscar: string; onBuscar: (t: string) => void }) {
  const [texto, setTexto] = useState(buscar)
  const [oficio, setOficio] = useState(0)
  const reducido = useMovimientoReducido()
  const cuenta = useContador(0, total ?? 0, total !== undefined, 1200)

  // Si la búsqueda cambia desde fuera (chips, limpiar), el campo la refleja
  const [buscarPrevio, setBuscarPrevio] = useState(buscar)
  if (buscarPrevio !== buscar) {
    setBuscarPrevio(buscar)
    setTexto(buscar)
  }
  useEffect(() => {
    if (reducido) return
    const id = setInterval(() => setOficio((o) => (o + 1) % oficiosRotando.length), 2600)
    return () => clearInterval(id)
  }, [reducido])

  const enviar = (e: FormEvent) => {
    e.preventDefault()
    onBuscar(texto.trim())
  }

  return (
    <section className="portal-hero">
      <HojasCayendo cantidad={6} className="hojas-claras" />
      <div className="portal-hero-brillo" aria-hidden="true" />
      <div className="portal-ancho portal-hero-contenido">
        <TarjetaSesion />
        <div className="portal-hero-titulo">
          <h1>
            Encuentra trabajo como{' '}
            <span className="portal-rotador" aria-live="off">
              <span key={oficio} className="portal-rotador-palabra">{oficiosRotando[oficio]}</span>
            </span>
          </h1>
          <span className="portal-en-vivo">
            <span className="portal-en-vivo-punto" />
            {total === undefined ? 'Vacantes abiertas hoy' : <><b className="mono">{cuenta}</b> {total === 1 ? 'vacante abierta' : 'vacantes abiertas'}{empresas ? <> en <b className="mono">{empresas}</b> {empresas === 1 ? 'empresa' : 'empresas'}</> : null}</>}
          </span>
        </div>

        <form role="search" className="portal-buscador" onSubmit={enviar}>
          <IconoBuscar className="portal-buscador-icono" />
          <input aria-label="Cargo, empresa o palabra clave" placeholder="Cargo, empresa o palabra clave" value={texto} onChange={(e) => setTexto(e.target.value)} />
          <button type="submit" className="boton boton-primario">Buscar</button>
        </form>

        <div className="portal-rapidas">
          <span className="portal-como"><IconoDocumento />Sube tu hoja de vida, confirma tus datos y envía en 2 minutos.</span>
          <span>Lo más buscado:</span>
          {busquedasRapidas.map((b, i) => (
            <button key={b} type="button" style={v(i)} className={b === buscar ? 'activa' : ''} onClick={() => { setTexto(b); onBuscar(b) }}>{b}</button>
          ))}
        </div>
      </div>
    </section>
  )
}

/** Arriba del buscador: con sesión, quién está y qué tiene pendiente; sin sesión, la invitación a entrar. */
function TarjetaSesion() {
  const cuenta = useMiCuenta()
  const portal = useMiPortal(cuenta.isSuccess)
  if (cuenta.isPending) return <Esqueleto alto={56} ancho={360} redondo style={{ opacity: 0.25 }} />

  if (!cuenta.data) {
    return (
      <div className="portal-hola portal-hola-invitado">
        <span className="portal-hola-icono"><IconoDocumento /></span>
        <span className="portal-hola-texto"><b>¿Ya tienes cuenta?</b><span>Entra para postularte con un clic y seguir tus procesos.</span></span>
        <Link to="/mi-portal/ingresar?volver=%2Fempleos" className="portal-hola-entrar">Ingresar</Link>
        <Link to="/mi-portal/registro?volver=%2Fempleos" className="portal-hola-crear">Crear cuenta</Link>
      </div>
    )
  }

  const c = cuenta.data
  const nombre = c.datos.nombre.split(' ')[0]
  const iniciales = c.datos.nombre.split(' ').filter(Boolean).slice(0, 2).map((x) => x[0]?.toUpperCase()).join('')
  const perfil = Math.round(((totalPasosPerfil - faltantesPerfil(c).length) / totalPasosPerfil) * 100)
  const pendientes = portal.data?.pendientes.length ?? 0
  const procesos = portal.data?.postulaciones.length ?? 0
  return (
    <Link to="/mi-portal" className="portal-hola">
      <span className="portal-hola-avatar" aria-hidden="true">{iniciales}<span className="portal-hola-punto" /></span>
      <span className="portal-hola-texto">
        <b>Hola, {nombre}</b>
        <span><span className="portal-hola-conectado">Sesión iniciada</span> · {c.correo}</span>
      </span>
      <span className="portal-hola-datos">
        {pendientes > 0 && <span className="portal-hola-dato alerta"><b className="mono">{pendientes}</b> {pendientes === 1 ? 'pendiente' : 'pendientes'}</span>}
        <span className="portal-hola-dato"><b className="mono">{procesos}</b> {procesos === 1 ? 'proceso' : 'procesos'}</span>
        <span className="portal-hola-dato"><b className="mono">{perfil}%</b> perfil</span>
      </span>
      <span className="portal-hola-ir">Mi portal <IconoFlecha /></span>
    </Link>
  )
}

function Chips({ titulo, icono, opciones, activo, onElegir }: { titulo: string; icono: ReactNode; opciones?: Conteo[]; activo: string; onElegir: (v: string) => void }) {
  if (!opciones) {
    return <div className="portal-chips-grupo">{[0, 1, 2, 3].map((i) => <Esqueleto key={i} alto={36} ancho={96} redondo />)}</div>
  }
  if (opciones.length === 0) return null
  return (
    <div className="portal-chips-grupo" role="group" aria-label={titulo}>
      <span className="portal-chips-titulo">{icono}{titulo}</span>
      {opciones.map((o) => (
        <button key={o.nombre} type="button" className="portal-chip" aria-pressed={activo === o.nombre} onClick={() => onElegir(o.nombre)} title={o.nombre}>
          {activo === o.nombre && <IconoCheck className="portal-chip-check" />}
          <span className="portal-chip-texto">{o.nombre}</span>
          <span className="mono">{o.total}</span>
        </button>
      ))}
    </div>
  )
}

function TarjetaVacante({ vacante, i }: { vacante: VacantePublica; i: number }) {
  const navegar = useNavigate()
  const dias = diasDesde(vacante.publicadaEn)
  const url = `/empleos/${vacante.codigo}`
  return (
    // Toda la tarjeta lleva a la vacante; el botón va directo a postularse
    <article className="portal-tarjeta" style={v(i)} onClick={() => navegar(url)}>
      <div className="portal-tarjeta-cabeza">
        <span className="portal-avatar" aria-hidden="true">{iniciales(vacante.empresa)}</span>
        <span className="portal-tarjeta-empresa">{vacante.empresa}</span>
        {dias <= 3 && <span className="pill pill-ok portal-nueva">Nueva</span>}
      </div>
      <h2><Link to={url} onClick={(e) => e.stopPropagation()}>{vacante.titulo}</Link></h2>
      <ul className="portal-datos">
        <li><IconoUbicacion />{vacante.ciudad}</li>
        {vacante.jornada && <li><IconoReloj />{vacante.jornada}</li>}
        <li><IconoCupos />{vacante.cupos} {vacante.cupos === 1 ? 'cupo' : 'cupos'}</li>
      </ul>
      <p>{vacante.resumen}</p>
      <div className="portal-tarjeta-pie">
        <span className="portal-salario"><IconoDinero /><span className="mono">{rangoSalario(vacante.salarioMinimo, vacante.salarioMaximo)}</span></span>
        <span className="portal-hace mono">{haceCuanto(vacante.publicadaEn)}</span>
      </div>
      <Link to={`${url}/postular`} className="boton boton-primario portal-postular" onClick={(e) => e.stopPropagation()}>
        Postularme en 2 minutos <IconoFlecha />
      </Link>
    </article>
  )
}

function TarjetaEsqueleto() {
  return (
    <div className="portal-tarjeta portal-tarjeta-esqueleto" aria-hidden="true">
      <div className="portal-tarjeta-cabeza"><Esqueleto alto={40} ancho={40} /><Esqueleto alto={12} ancho="40%" /></div>
      <Esqueleto alto={22} ancho="75%" />
      <Esqueleto alto={14} ancho="60%" />
      <Esqueleto alto={36} />
      <Esqueleto alto={18} ancho="45%" />
      <Esqueleto alto={44} />
    </div>
  )
}
