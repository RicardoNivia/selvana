import { Link } from 'react-router-dom'
import { IconoEscudo } from '../../shared/ui/Iconos'
import { LogoAnimado } from '../../shared/ui/LogoAnimado'

const anio = new Date().getFullYear()

/** Pie de las páginas públicas: producto, candidatos, legal y la línea de derechos reservados. */
export function PieLegal() {
  return (
    <footer className="inicio-pie pie-legal">
      <div className="inicio-ancho pie-legal-columnas">
        <div className="pie-legal-marca">
          <LogoAnimado tamano={26} palabra />
          <p>Talento humano con una IA que trabaja y explica. Usted decide.</p>
          <span className="pie-legal-sello"><IconoEscudo />Datos personales según la Ley 1581 de 2012</span>
        </div>
        <nav aria-label="Producto">
          <b>Producto</b>
          <a href="/#como">Cómo funciona</a>
          <a href="/#en-accion">En acción</a>
          <a href="/#por-que">Por qué Selvana</a>
          <a href="/#para-quien">Para quién</a>
          <a href="/#modulos">Módulos</a>
        </nav>
        <nav aria-label="Candidatos">
          <b>Candidatos</b>
          <Link to="/empleos">Portal de empleo</Link>
          <Link to="/empleos/mis-datos">Tus datos personales</Link>
        </nav>
        <nav aria-label="Legal">
          <b>Legal</b>
          <Link to="/legal/tratamiento-de-datos">Política de tratamiento de datos</Link>
          <Link to="/legal/aviso-de-privacidad">Aviso de privacidad</Link>
          <Link to="/legal/terminos">Términos y condiciones</Link>
        </nav>
        <nav aria-label="Ingresa">
          <b>Ingresa</b>
          <Link to="/ingresar">Consola para empresas</Link>
        </nav>
      </div>
      <div className="pie-legal-derechos">
        <div className="inicio-ancho">© {anio} Selvana · Todos los derechos reservados · Hecho en Colombia</div>
      </div>
    </footer>
  )
}
