import { useEffect, useState, type ReactNode } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useResumenBandeja } from '../features/bandeja/api'
import { iniciales, nombreRol, usePuede, useSalir, useSesion } from '../features/sesion/api'
import { elegirEmpresaUsuaria, useEmpresaUsuariaActual } from '../features/sesion/empresaUsuaria'
import {
  Chispa, IconoBandeja, IconoCandado, IconoCerrar, IconoConexion, IconoCupos, IconoEquipo, IconoDocumento, IconoClima, IconoEdificio, IconoEstructura,
  IconoFlecha, IconoInsignia, IconoLuna, IconoManos, IconoPantalla, IconoPersonas, IconoSalir, IconoSol, IconoUsuarios, IconoVacantes,
} from '../shared/ui/Iconos'
import { HojasCayendo } from '../shared/ui/HojasCayendo'
import { LogoAnimado } from '../shared/ui/LogoAnimado'
import { useTema, type Tema } from '../shared/ui/tema'
import './riel.css'

type Cajon = 'empresa' | 'cuenta'

/** Enlace del riel: ícono y, con el riel expandido, su nombre al lado. */
function ItemRiel({ a, icono, texto, contador = 0 }: { a: string; icono: ReactNode; texto: string; contador?: number }) {
  return (
    <NavLink to={a} className="riel-item" title={contador > 0 ? `${texto} · ${contador} por decidir` : texto}>
      <span className="riel-icono">
        {icono}
        {contador > 0 && <span className="riel-contador" aria-label={`${contador} por decidir`}>{contador > 99 ? '99+' : contador}</span>}
      </span>
      <span className="riel-texto">{texto}</span>
    </NavLink>
  )
}

/** Enlace dentro de un cajón: ícono, nombre y para qué sirve. */
function EnlaceCajon({ a, icono, texto, detalle }: { a: string; icono: ReactNode; texto: string; detalle: string }) {
  return (
    <NavLink to={a} className="cajon-enlace">
      <span className="cajon-enlace-icono">{icono}</span>
      <span><b>{texto}</b><small>{detalle}</small></span>
    </NavLink>
  )
}

function ProntoCajon({ icono, texto, cuando }: { icono: ReactNode; texto: string; cuando: string }) {
  return (
    <span className="cajon-enlace pronto" aria-disabled="true">
      <span className="cajon-enlace-icono">{icono}</span>
      <span><b>{texto}</b></span>
      <span className="cajon-etiqueta">{cuando}</span>
    </span>
  )
}

const temas: { valor: Tema; texto: string; icono: ReactNode }[] = [
  { valor: 'claro', texto: 'Claro', icono: <IconoSol /> },
  { valor: 'oscuro', texto: 'Oscuro', icono: <IconoLuna /> },
  { valor: 'sistema', texto: 'Sistema', icono: <IconoPantalla /> },
]

export function Layout() {
  const { data: sesion } = useSesion()
  const salir = useSalir()
  const navegar = useNavigate()
  const { pathname } = useLocation()
  const actual = useEmpresaUsuariaActual()
  const configura = usePuede('ConfigurarEstructura')
  const gestionaUsuarios = usePuede('GestionarUsuarios')
  const veBitacora = usePuede('VerBitacora')
  const veCandidatos = usePuede('VerHojasDeVida')
  const gestionaEmpleados = usePuede('GestionarEmpleados')
  const gestionaDesempeno = usePuede('GestionarDesempeno')
  const gestionaClima = usePuede('GestionarClima')
  const veEquipo = usePuede('VerMiEquipo')
  const pendientes = useResumenBandeja(Boolean(sesion)).data?.pendientes ?? 0
  const conSelector = Boolean(sesion?.empresaTemporal && sesion.empresasUsuarias.length > 0)
  const { tema, elegir: elegirTema } = useTema()
  const [abierto, setAbierto] = useState<Cajon | null>(null)

  // Al navegar se cierra el cajón; Escape también lo cierra
  useEffect(() => setAbierto(null), [pathname])
  useEffect(() => {
    if (!abierto) return
    const alTeclear = (e: KeyboardEvent) => { if (e.key === 'Escape') setAbierto(null) }
    window.addEventListener('keydown', alTeclear)
    return () => window.removeEventListener('keydown', alTeclear)
  }, [abierto])

  const alternar = (c: Cajon) => setAbierto((a) => (a === c ? null : c))
  const enEmpresa = pathname.startsWith('/consola/empresa')
  const nombreEmpresa = actual.nombre || sesion?.empresaNombre || ''

  // Riel fijo de íconos (nunca se desplaza) que se expande encima de la página al acercar el mouse;
  // la configuración y la cuenta se abren en cajones al lado y lo mantienen expandido
  return (
    <div className="consola">
      <nav className={`riel${abierto ? ' expandido' : ''}`} aria-label="Principal">
        <div className="riel-fondo" aria-hidden="true">
          <HojasCayendo cantidad={7} className="hojas-claras" />
          <svg className="riel-dosel" viewBox="0 0 24 24"><path d="M2 22C2 10 9 2 22 2C22 14 14 22 2 22Z" /></svg>
        </div>
        <NavLink to="/consola" className="riel-marca" aria-label="Selvana, consola" title="Selvana">
          <LogoAnimado tamano={32} palabra claro />
        </NavLink>

        <div className="riel-menu">
          <ItemRiel a="/consola/bandeja" icono={<IconoBandeja />} texto="Bandeja" contador={pendientes} />
          <ItemRiel a="/consola/vacantes" icono={<IconoVacantes />} texto="Vacantes" />
          {veCandidatos && <ItemRiel a="/consola/candidatos" icono={<IconoPersonas />} texto="Candidatos" />}
          {veCandidatos && <ItemRiel a="/consola/vinculacion" icono={<IconoDocumento />} texto="Vinculación" />}
          {veEquipo && <ItemRiel a="/consola/mi-equipo" icono={<IconoEquipo />} texto="Mi equipo" />}
          {gestionaEmpleados && <ItemRiel a="/consola/empleados" icono={<IconoCupos />} texto="Empleados" />}
          {gestionaDesempeno && <ItemRiel a="/consola/desempeno" icono={<IconoInsignia />} texto="Desempeño" />}
          {gestionaClima && <ItemRiel a="/consola/clima" icono={<IconoClima />} texto="Clima" />}
          <span className="riel-separador" aria-hidden="true" />
          <button type="button" className={`riel-item${enEmpresa ? ' active' : ''}${abierto === 'empresa' ? ' abierto' : ''}`}
            aria-expanded={abierto === 'empresa'} aria-controls="cajon-empresa" onClick={() => alternar('empresa')} title="Empresa">
            <span className="riel-icono"><IconoEdificio /></span>
            <span className="riel-texto">Empresa</span>
            <IconoFlecha className="riel-flecha" />
          </button>
        </div>

        <div className="riel-pie">
          {sesion && (
            <button type="button" className={`riel-item riel-cuenta${abierto === 'cuenta' ? ' abierto' : ''}`}
              aria-expanded={abierto === 'cuenta'} aria-controls="cajon-cuenta" onClick={() => alternar('cuenta')}
              title={`${sesion.nombre} · ${nombreEmpresa}`}>
              <span className="riel-avatar">{iniciales(sesion.nombre)}</span>
              <span className="riel-texto"><b>{sesion.nombre}</b><small>{nombreEmpresa}</small></span>
            </button>
          )}
        </div>

        <section id="cajon-empresa" className={`cajon${abierto === 'empresa' ? ' visible' : ''}`} aria-label="Empresa" inert={abierto !== 'empresa'}>
          <header className="cajon-cabeza">
            <div><span className="cajon-sobre">Configuración</span><h2>Empresa</h2></div>
            <button type="button" className="cajon-cerrar" aria-label="Cerrar" onClick={() => setAbierto(null)}><IconoCerrar /></button>
          </header>
          <div className="cajon-cuerpo">
            {configura && <EnlaceCajon a="/consola/empresa/conectar-nomina" icono={<IconoConexion />} texto="Conectar nómina" detalle="Archivo o conector del ERP" />}
            {configura && <EnlaceCajon a="/consola/empresa/estructura" icono={<IconoEstructura />} texto="Estructura y cargos" detalle="Sedes, áreas y catálogo de cargos" />}
            {configura && <EnlaceCajon a="/consola/empresa/tipos-contrato" icono={<IconoManos />} texto="Tipos de contrato" detalle="Modalidades y plantillas" />}
            {gestionaUsuarios && <EnlaceCajon a="/consola/empresa/usuarios" icono={<IconoUsuarios />} texto="Usuarios y roles" detalle="Invitaciones, permisos y empresas usuarias" />}
            {veBitacora && <EnlaceCajon a="/consola/empresa/privacidad" icono={<IconoCandado />} texto="Privacidad y auditoría" detalle="Ley 1581, solicitudes y bitácora" />}
            {!configura && !gestionaUsuarios && !veBitacora && <p className="cajon-nota">Su rol no configura la empresa. Pídale acceso al administrador.</p>}
            {/* Lo que viene en el plan de trabajo: visible pero desactivado, sin prometer pantallas que aún no existen */}
            <span className="cajon-grupo">Próximamente</span>
            <ProntoCajon icono={<Chispa />} texto="Agentes y autonomía" cuando="Pronto" />
          </div>
        </section>

        {sesion && (
          <section id="cajon-cuenta" className={`cajon cajon-abajo${abierto === 'cuenta' ? ' visible' : ''}`} aria-label="Tu cuenta" inert={abierto !== 'cuenta'}>
            <header className="cajon-cabeza">
              <div className="cajon-persona">
                <span className="riel-avatar">{iniciales(sesion.nombre)}</span>
                <span><b>{sesion.nombre}</b><small>{nombreRol[sesion.rol] ?? sesion.rol}</small></span>
              </div>
              <button type="button" className="cajon-cerrar" aria-label="Cerrar" onClick={() => setAbierto(null)}><IconoCerrar /></button>
            </header>
            <div className="cajon-cuerpo">
              <span className="cajon-grupo">Trabajando en</span>
              <div className="cajon-empresa">
                <span className="cajon-empresa-logo">{iniciales(nombreEmpresa)}</span>
                <span>
                  {conSelector ? (
                    <select className="selector-empresa-usuaria" aria-label="Empresa usuaria en la que trabaja" value={actual.id}
                      onChange={(e) => elegirEmpresaUsuaria(e.target.value)}>
                      <option value="">{sesion.todasLasEmpresasUsuarias ? 'Todas las empresas usuarias' : 'Todas mis empresas usuarias'}</option>
                      {sesion.empresasUsuarias.map((e) => <option key={e.id} value={e.id}>{e.nombre}</option>)}
                    </select>
                  ) : <b>{sesion.empresaNombre}</b>}
                  {conSelector && <small>{sesion.empresaNombre}</small>}
                </span>
              </div>

              <span className="cajon-grupo">Tema</span>
              <div className="cajon-temas" role="radiogroup" aria-label="Tema">
                {temas.map((t) => (
                  <button key={t.valor} type="button" role="radio" aria-checked={tema === t.valor}
                    className={tema === t.valor ? 'activo' : undefined} onClick={() => elegirTema(t.valor)}>
                    {t.icono}{t.texto}
                  </button>
                ))}
              </div>

              <button type="button" className="cajon-salir" disabled={salir.isPending} aria-busy={salir.isPending || undefined}
                onClick={() => salir.mutate(undefined, { onSettled: () => navegar('/', { replace: true }) })}>
                {salir.isPending ? <span className="indicador" aria-hidden="true" /> : <IconoSalir />}
                {salir.isPending ? 'Saliendo…' : 'Salir'}
              </button>
            </div>
          </section>
        )}
      </nav>
      {abierto && <div className="riel-velo" aria-hidden="true" onClick={() => setAbierto(null)} />}
      <main className="contenido">
        <Outlet />
      </main>
    </div>
  )
}
