import { useEffect, useRef, useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoDocumento, IconoEscudo, IconoInicio, IconoSalir, IconoUsuarios, IconoVacantes } from '../../shared/ui/Iconos'
import { useMiCuenta, useSalirPortal } from '../portal-persona/api'
import { LogoAnimado } from '../../shared/ui/LogoAnimado'
import { useDesplazamiento } from '../../shared/ui/animaciones'
import '../portal-persona/portal-persona.css'

/** Marco del portal de empleo público: liviano, pensado primero para celular, con transición suave entre páginas. */
export function PortalLayout() {
  const ubicacion = useLocation()
  const { bajo } = useDesplazamiento()

  // Cada página nueva empieza arriba (salvo al cambiar solo filtros o la página de resultados)
  useEffect(() => {
    window.scrollTo({ top: 0 })
  }, [ubicacion.pathname])

  return (
    <div className="portal">
      <header className={`portal-cabecera ${bajo ? 'portal-cabecera-baja' : ''}`}>
        <div className="portal-ancho portal-cabecera-fila">
          <Link to="/empleos" className="portal-marca" aria-label="Selvana empleos, inicio">
            <LogoAnimado tamano={28} palabra />
            <span className="portal-marca-sufijo">empleos</span>
          </Link>
          <nav aria-label="Portal" className="portal-nav">
            <NavLink to="/empleos" end>Vacantes</NavLink>
            <Link to="/" className="portal-nav-empresas">Para empresas</Link>
          </nav>
          <SesionPortal />
        </div>
      </header>

      {/* Lo que llega por enlace (chat, pruebas, oferta) también está en el portal de la persona (ADR 0013) */}
      {/^\/(preseleccion|pruebas|vinculacion)\//.test(ubicacion.pathname) && (
        <div className="mp-aviso-enlace">
          <span>Ahora todos tus procesos, chats, pruebas y tu contrato están en un solo lugar.</span>
          <Link to="/mi-portal">Entra a tu portal o crea tu cuenta</Link>
        </div>
      )}

      <div key={ubicacion.pathname} className="portal-pagina">
        <Outlet />
      </div>

      <footer className="portal-pie">
        <div className="portal-ancho portal-pie-fila">
          <span className="portal-pie-confianza"><IconoEscudo />Tus datos se tratan según la Ley 1581 de 2012. Nunca usamos tu edad, género, estado civil ni foto para evaluarte.</span>
          <Link to="/empleos/mis-datos">Tus datos personales</Link>
          <Link to="/legal/tratamiento-de-datos">Política de datos</Link>
          <Link to="/legal/terminos">Términos</Link>
          <Link to="/">Selvana para empresas</Link>
        </div>
      </footer>
    </div>
  )
}

/** Quién está en el portal: sin sesión, Ingresar y Crear cuenta (vuelven a la página actual); con sesión, su menú. */
function SesionPortal() {
  const cuenta = useMiCuenta()
  const salir = useSalirPortal()
  const navegar = useNavigate()
  const { pathname, search } = useLocation()
  const [abierto, setAbierto] = useState(false)
  const menu = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!abierto) return
    const fuera = (e: PointerEvent) => { if (!menu.current?.contains(e.target as Node)) setAbierto(false) }
    const tecla = (e: KeyboardEvent) => { if (e.key === 'Escape') setAbierto(false) }
    document.addEventListener('pointerdown', fuera)
    document.addEventListener('keydown', tecla)
    return () => { document.removeEventListener('pointerdown', fuera); document.removeEventListener('keydown', tecla) }
  }, [abierto])

  if (cuenta.isPending) return <Esqueleto alto={36} ancho={36} redondo />

  if (!cuenta.data) {
    // En las páginas de entrar y registrarse no se repiten los botones
    if (pathname.startsWith('/mi-portal/ingresar') || pathname.startsWith('/mi-portal/registro')) return null
    const volver = pathname.startsWith('/mi-portal') ? '' : `?volver=${encodeURIComponent(pathname + search)}`
    return (
      <div className="portal-sesion">
        <Link to={`/mi-portal/ingresar${volver}`} className="portal-sesion-entrar">Ingresar</Link>
        <Link to={`/mi-portal/registro${volver}`} className="boton boton-primario boton-pequeno">Crear cuenta</Link>
      </div>
    )
  }

  const nombre = cuenta.data.datos.nombre
  const iniciales = nombre.split(' ').filter(Boolean).slice(0, 2).map((x) => x[0]?.toUpperCase()).join('')
  const ir = (ruta: string) => { setAbierto(false); navegar(ruta) }

  return (
    <div className="portal-sesion" ref={menu}>
      <button type="button" className="portal-sesion-boton" aria-haspopup="menu" aria-expanded={abierto} onClick={() => setAbierto((v) => !v)}>
        <span className="portal-sesion-avatar" aria-hidden="true">{iniciales}</span>
        <span className="portal-sesion-nombre">{nombre.split(' ')[0]}</span>
        <svg viewBox="0 0 20 20" aria-hidden="true" className="portal-sesion-flecha"><path d="M5 8l5 5 5-5" /></svg>
      </button>
      {abierto && (
        <div className="portal-menu" role="menu">
          <div className="portal-menu-quien">
            <b>{nombre}</b>
            <span>{cuenta.data.correo}</span>
          </div>
          <button type="button" role="menuitem" onClick={() => ir('/mi-portal')}><IconoInicio />Mi portal</button>
          <button type="button" role="menuitem" onClick={() => ir('/mi-portal?ver=procesos')}><IconoVacantes />Mis procesos</button>
          <button type="button" role="menuitem" onClick={() => ir('/mi-portal?ver=hv')}><IconoDocumento />Mi hoja de vida</button>
          <button type="button" role="menuitem" onClick={() => ir('/mi-portal?ver=cuenta')}><IconoUsuarios />Mi cuenta</button>
          <button type="button" role="menuitem" className="portal-menu-salir" disabled={salir.isPending} aria-busy={salir.isPending || undefined}
            onClick={() => salir.mutate(undefined, { onSettled: () => { setAbierto(false); navegar('/empleos') } })}>
            {salir.isPending ? <span className="indicador" aria-hidden="true" /> : <IconoSalir />}{salir.isPending ? 'Saliendo…' : 'Salir'}
          </button>
        </div>
      )}
    </div>
  )
}
