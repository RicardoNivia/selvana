import { useEffect } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { IconoEscudo } from '../../shared/ui/Iconos'
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
            <NavLink to="/mi-portal">Mi portal</NavLink>
            <Link to="/" className="portal-nav-empresas">Para empresas</Link>
          </nav>
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
