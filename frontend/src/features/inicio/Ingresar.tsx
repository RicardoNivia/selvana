import { type CSSProperties, useState } from 'react'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Chispa, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { LogoAnimado } from '../../shared/ui/LogoAnimado'
import { useIngresar, useSesion } from '../sesion/api'
import { HojasCayendo } from '../../shared/ui/HojasCayendo'

/** Solo se aceptan rutas internas de la consola para volver después de ingresar. */
const destinoSeguro = (volver: string | null) => (volver && volver.startsWith('/consola') ? volver : '/consola')

const usuariosDePrueba = [
  { correo: 'laura.moreno@serviciosandinos.co', quien: 'Laura Moreno · Gerente RRHH' },
  { correo: 'admin@serviciosandinos.co', quien: 'Andrés Rojas · Administrador' },
]

/**
 * Inicio de sesión de la consola: correo y contraseña contra la API (cookie HttpOnly).
 * Microsoft y Google (OpenID Connect) se activan al configurar el emisor en Auth:Authority.
 */
export function Ingresar() {
  const navegar = useNavigate()
  const [parametros] = useSearchParams()
  const destino = destinoSeguro(parametros.get('volver'))
  const sesion = useSesion()
  const ingresar = useIngresar()
  const [correo, setCorreo] = useState('')
  const [contrasena, setContrasena] = useState('')
  const [verContrasena, setVerContrasena] = useState(false)

  if (sesion.data) {
    return <Navigate to={destino} replace />
  }

  const enviar = () => {
    if (!correo.trim() || !contrasena) return
    ingresar.mutate({ correo: correo.trim(), contrasena }, { onSuccess: () => navegar(destino, { replace: true }) })
  }

  return (
    <div className="ingresar">
      <aside className="ingresar-marca">
        <HojasCayendo cantidad={9} className="hojas-claras" />
        <Link to="/" className="ingresar-logo" aria-label="Selvana, volver al inicio"><LogoAnimado tamano={36} palabra claro /></Link>
        <div className="ingresar-promesa">
          <LogoAnimado tamano={132} claro className="ingresar-logo-grande" />
          <h1>Tu equipo de talento humano, con una IA que trabaja y explica.</h1>
          <ul>
            <li style={{ '--i': 0 } as CSSProperties}><Chispa />La IA redacta, lee y propone.</li>
            <li style={{ '--i': 1 } as CSSProperties}><IconoCheck />Tú apruebas lo que afecta a una persona.</li>
            <li style={{ '--i': 2 } as CSSProperties}><IconoEscudo />Cada decisión queda en la auditoría.</li>
          </ul>
        </div>
        <span className="ingresar-pie">Datos tratados según la Ley 1581 de 2012</span>
      </aside>

      <main className="ingresar-panel">
        <form className="ingresar-form" onSubmit={(e) => { e.preventDefault(); enviar() }} noValidate>
          <LogoAnimado tamano={44} className="ingresar-logo-movil" />
          <h2>Ingrese a Selvana</h2>
          <p>Use la cuenta de su empresa.</p>

          <button type="button" className="boton boton-secundario ingresar-sso" disabled title="Se activa al configurar el proveedor de identidad">Continuar con Microsoft</button>
          <button type="button" className="boton boton-secundario ingresar-sso" disabled title="Se activa al configurar el proveedor de identidad">Continuar con Google</button>

          <div className="ingresar-separador"><span>o con su correo</span></div>

          <label className="postular-campo">
            Correo de trabajo
            <input className="entrada" type="email" autoComplete="username" placeholder="nombre@empresa.co" required
              value={correo} onChange={(e) => setCorreo(e.target.value)} autoFocus />
          </label>
          <label className="postular-campo">
            Contraseña
            <span className="ingresar-contrasena">
              <input className="entrada" type={verContrasena ? 'text' : 'password'} autoComplete="current-password" required
                value={contrasena} onChange={(e) => setContrasena(e.target.value)} />
              <button type="button" className="ingresar-ver" onClick={() => setVerContrasena((v) => !v)} aria-pressed={verContrasena}>
                {verContrasena ? 'Ocultar' : 'Ver'}
              </button>
            </span>
          </label>

          {ingresar.isError && (
            <div className="aviso aviso-alerta ingresar-error" role="alert">{ingresar.error.message}</div>
          )}

          <Boton type="submit" variante="primario" cargando={ingresar.isPending} textoCargando="Ingresando…" className="ingresar-entrar"
            disabled={!correo.trim() || !contrasena}>
            Ingresar
          </Boton>

          {import.meta.env.DEV && (
            <div className="ingresar-prueba">
              <b>Usuarios de prueba</b> · Servicios Andinos EST
              {usuariosDePrueba.map((u) => (
                <button key={u.correo} type="button" onClick={() => setCorreo(u.correo)}>
                  <span className="mono">{u.correo}</span>
                  <span>{u.quien}</span>
                </button>
              ))}
              <span>La contraseña está en los user-secrets de la API (Desarrollo:Contrasena).</span>
            </div>
          )}

          <Link to="/" className="ingresar-volver">← Volver a selvana.co</Link>
        </form>
      </main>
    </div>
  )
}
