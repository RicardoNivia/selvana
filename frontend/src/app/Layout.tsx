import { type ReactNode, useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useResumenBandeja } from '../features/bandeja/api'
import { iniciales, nombreRol, usePuede, useSalir, useSesion } from '../features/sesion/api'
import { elegirEmpresaUsuaria, useEmpresaUsuariaActual } from '../features/sesion/empresaUsuaria'
import {
  Chispa, IconoBandeja, IconoCandado, IconoConexion, IconoContraer, IconoEstructura, IconoExpandir, IconoPersonas, IconoSalir, IconoUsuarios,
  IconoVacantes, IconoDocumento, IconoCupos, IconoInsignia, Isotipo,
} from '../shared/ui/Iconos'

const claveColapsado = 'selvana.menu-colapsado'

/** El menú contraído es una preferencia de cada persona en su navegador (si el almacenamiento falla, arranca expandido). */
function leerColapsado() {
  try {
    return localStorage.getItem(claveColapsado) === '1'
  } catch {
    return false
  }
}

function Pronto({ icono, texto, cuando }: { icono: ReactNode; texto: string; cuando: string }) {
  return (
    <span className="nav-item pronto" aria-disabled="true" title={`${texto} · ${cuando}`}>
      {icono}
      <span className="nav-texto">{texto}</span>
      <span className="etiqueta">{cuando}</span>
    </span>
  )
}

function Enlace({ a, icono, texto }: { a: string; icono: ReactNode; texto: string }) {
  return (
    <NavLink to={a} className="nav-item" title={texto}>
      {icono}
      <span className="nav-texto">{texto}</span>
    </NavLink>
  )
}

export function Layout() {
  const { data: sesion } = useSesion()
  const salir = useSalir()
  const navegar = useNavigate()
  const actual = useEmpresaUsuariaActual()
  const configura = usePuede('ConfigurarEstructura')
  const gestionaUsuarios = usePuede('GestionarUsuarios')
  const veBitacora = usePuede('VerBitacora')
  const veCandidatos = usePuede('VerHojasDeVida')
  const pendientes = useResumenBandeja(Boolean(sesion)).data?.pendientes ?? 0
  const conSelector = Boolean(sesion?.empresaTemporal && sesion.empresasUsuarias.length > 0)
  const [colapsado, setColapsado] = useState(leerColapsado)
  const alternar = () => {
    setColapsado((c) => {
      try {
        localStorage.setItem(claveColapsado, c ? '0' : '1')
      } catch {
        // Sin almacenamiento solo se pierde la preferencia
      }
      return !c
    })
  }

  return (
    <div className={`consola ${colapsado ? 'menu-colapsado' : ''}`}>
      <nav className="lateral" aria-label="Principal">
        <div className="lateral-cabeza">
          <NavLink to="/consola" className="marca" aria-label="Selvana, consola">
            <Isotipo />
            <span className="nav-texto">selvana</span>
          </NavLink>
          <button type="button" className="lateral-alternar" onClick={alternar} aria-expanded={!colapsado}
            aria-label={colapsado ? 'Expandir el menú' : 'Contraer el menú'} title={colapsado ? 'Expandir el menú' : 'Contraer el menú'}>
            {colapsado ? <IconoExpandir /> : <IconoContraer />}
          </button>
        </div>
        <div className="lateral-menu">
          <NavLink to="/consola/bandeja" className="nav-item" title={pendientes > 0 ? `Bandeja · ${pendientes} por decidir` : 'Bandeja'}>
            <IconoBandeja />
            <span className="nav-texto">Bandeja</span>
            {pendientes > 0 && <span className="nav-contador" aria-label={`${pendientes} por decidir`}>{pendientes}</span>}
          </NavLink>
          <Enlace a="/consola/vacantes" icono={<IconoVacantes />} texto="Vacantes" />
          {veCandidatos && <Enlace a="/consola/candidatos" icono={<IconoPersonas />} texto="Candidatos" />}
          {veCandidatos && <Enlace a="/consola/vinculacion" icono={<IconoDocumento />} texto="Vinculación" />}

          {(configura || gestionaUsuarios || veBitacora) && <div className="nav-grupo">Empresa</div>}
          {configura && <Enlace a="/consola/empresa/conectar-nomina" icono={<IconoConexion />} texto="Conectar nómina" />}
          {configura && <Enlace a="/consola/empresa/estructura" icono={<IconoEstructura />} texto="Estructura y cargos" />}
          {gestionaUsuarios && <Enlace a="/consola/empresa/usuarios" icono={<IconoUsuarios />} texto="Usuarios y roles" />}
          {veBitacora && <Enlace a="/consola/empresa/privacidad" icono={<IconoCandado />} texto="Privacidad y auditoría" />}
          <Pronto icono={<Chispa />} texto="Agentes y autonomía" cuando="Pronto" />

          {/* Lo que viene en el plan de trabajo: visible pero desactivado, sin prometer pantallas que aún no existen */}
          <div className="nav-grupo">Próximas etapas</div>
          <Pronto icono={<IconoCupos />} texto="Empleados" cuando="Etapa 3" />
          <Pronto icono={<IconoInsignia />} texto="Desempeño y clima" cuando="Etapa 4" />
        </div>

        {/* Pie fijo del menú: quién trabaja, en qué empresa, y salir */}
        <div className="lateral-pie">
          {sesion && (
            <div className="empresa-actual">
              <span className="logo">{iniciales(actual.nombre || sesion.empresaNombre)}</span>
              <span className="empresa-actual-detalle">
                <span>Trabajando en</span>
                {conSelector ? (
                  <select className="selector-empresa-usuaria" aria-label="Empresa usuaria en la que trabaja" value={actual.id}
                    onChange={(e) => elegirEmpresaUsuaria(e.target.value)}>
                    <option value="">{sesion.todasLasEmpresasUsuarias ? 'Todas las empresas usuarias' : 'Todas mis empresas usuarias'}</option>
                    {sesion.empresasUsuarias.map((e) => <option key={e.id} value={e.id}>{e.nombre}</option>)}
                  </select>
                ) : <b>{sesion.empresaNombre}</b>}
                {conSelector && <span className="empresa-actual-matriz">{sesion.empresaNombre}</span>}
                {sesion.nombre} · {nombreRol[sesion.rol] ?? sesion.rol}
              </span>
            </div>
          )}
          <button type="button" className="nav-item nav-salir" disabled={salir.isPending} aria-busy={salir.isPending || undefined}
            onClick={() => salir.mutate(undefined, { onSettled: () => navegar('/', { replace: true }) })} title="Salir">
            {salir.isPending ? <span className="indicador" aria-hidden="true" /> : <IconoSalir />}
            <span className="nav-texto">{salir.isPending ? 'Saliendo…' : 'Salir'}</span>
          </button>
        </div>
      </nav>
      <main className="contenido">
        <Outlet />
      </main>
    </div>
  )
}
