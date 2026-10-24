import { type FormEvent, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { HojasCayendo } from '../../shared/ui/HojasCayendo'
import { IconoCheck } from '../../shared/ui/Iconos'
import { LogoAnimado } from '../../shared/ui/LogoAnimado'
import { nombreRol, useIngresar } from '../sesion/api'
import { useActivarCuenta, useInvitacion } from '../usuarios/api'

const reglas = [
  { texto: 'Al menos 10 caracteres', ok: (c: string) => c.length >= 10 },
  { texto: 'Letras', ok: (c: string) => /\p{L}/u.test(c) },
  { texto: 'Números', ok: (c: string) => /\d/.test(c) },
]

/** La persona invitada crea su contraseña y entra a la consola. El enlace sirve una sola vez. */
export function ActivarCuenta() {
  const { token = '' } = useParams()
  const navegar = useNavigate()
  const invitacion = useInvitacion(token)
  const activar = useActivarCuenta()
  const ingresar = useIngresar()
  const [contrasena, setContrasena] = useState('')
  const [repetida, setRepetida] = useState('')
  const cumple = reglas.every((r) => r.ok(contrasena))
  const coinciden = contrasena.length > 0 && contrasena === repetida

  const enviar = (e: FormEvent) => {
    e.preventDefault()
    if (!invitacion.data) return
    const correo = invitacion.data.correo
    activar.mutate({ token, contrasena }, {
      onSuccess: () => ingresar.mutate({ correo, contrasena }, { onSuccess: () => navegar('/consola', { replace: true }) }),
    })
  }

  return (
    <div className="ingresar">
      <aside className="ingresar-marca">
        <HojasCayendo cantidad={9} className="hojas-claras" />
        <Link to="/" className="ingresar-logo" aria-label="Selvana, inicio"><LogoAnimado tamano={36} palabra claro /></Link>
        <div className="ingresar-promesa">
          <LogoAnimado tamano={132} claro className="ingresar-logo-grande" />
          <h1>Le damos la bienvenida a su equipo de talento humano con IA.</h1>
        </div>
        <span className="ingresar-pie">Datos tratados según la Ley 1581 de 2012</span>
      </aside>

      <main className="ingresar-panel">
        {invitacion.isPending ? (
          <div className="ingresar-form" aria-busy="true"><Esqueleto alto={32} ancho="70%" /><Esqueleto alto={16} ancho="90%" /><Esqueleto alto={46} /><Esqueleto alto={46} /><Esqueleto alto={46} /></div>
        ) : invitacion.isError ? (
          <div className="ingresar-form">
            <h2>Este enlace ya no sirve</h2>
            <p>{invitacion.error.message}</p>
            <Link to="/ingresar" className="boton boton-secundario ingresar-entrar">Ir a ingresar</Link>
          </div>
        ) : (
          <form className="ingresar-form" onSubmit={enviar}>
            <LogoAnimado tamano={44} className="ingresar-logo-movil" />
            <h2>Hola, {invitacion.data.nombre.split(' ')[0]}</h2>
            <p>{invitacion.data.empresa} le dio acceso a Selvana como <b>{nombreRol[invitacion.data.rol] ?? invitacion.data.rol}</b>. Cree su contraseña para entrar.</p>
            <label className="postular-campo">Correo<input className="entrada" value={invitacion.data.correo} readOnly autoComplete="username" /></label>
            <label className="postular-campo">Contraseña
              <input className="entrada" type="password" autoComplete="new-password" value={contrasena} onChange={(e) => setContrasena(e.target.value)} autoFocus />
            </label>
            <ul className="reglas-contrasena" aria-label="Requisitos de la contraseña">
              {reglas.map((r) => <li key={r.texto} className={r.ok(contrasena) ? 'ok' : ''}><IconoCheck />{r.texto}</li>)}
            </ul>
            <label className="postular-campo">Repita la contraseña
              <input className="entrada" type="password" autoComplete="new-password" value={repetida} onChange={(e) => setRepetida(e.target.value)} aria-invalid={repetida.length > 0 && !coinciden || undefined} />
              {repetida.length > 0 && !coinciden && <span className="postular-nota-error">Las contraseñas no coinciden.</span>}
            </label>
            {(activar.error ?? ingresar.error) && <div className="aviso aviso-alerta ingresar-error" role="alert">{(activar.error ?? ingresar.error)!.message}</div>}
            <Boton type="submit" variante="primario" className="ingresar-entrar" disabled={!cumple || !coinciden}
              cargando={activar.isPending || ingresar.isPending} textoCargando={ingresar.isPending ? 'Entrando…' : 'Creando…'}>
              Crear contraseña y entrar
            </Boton>
          </form>
        )}
      </main>
    </div>
  )
}
