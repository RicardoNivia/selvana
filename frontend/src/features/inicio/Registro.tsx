import { type CSSProperties, type FormEvent, useState } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { HojasCayendo } from '../../shared/ui/HojasCayendo'
import { Chispa, IconoCheck, IconoEscudo, IconoFlecha, IconoReloj } from '../../shared/ui/Iconos'
import { LogoAnimado } from '../../shared/ui/LogoAnimado'
import { type RegistroEmpresa, useRegistrarEmpresa, useSesion } from '../sesion/api'
import '../portal-persona/portal-persona.css'

const sectores = ['Servicios temporales (EST)', 'Outsourcing', 'BPO y call center', 'Comercio y retail', 'Industria y manufactura', 'Logística y transporte',
  'Salud', 'Construcción', 'Servicios corporativos', 'Otro']
const tamanos = ['1 a 50', '51 a 200', '201 a 1.000', 'Más de 1.000']

/** Dígito de verificación de la DIAN, para avisar mientras escribe (la API lo vuelve a validar). */
function nitValido(nit: string) {
  const limpio = nit.replace(/[.\s]/g, '')
  const m = /^(\d{9})(?:-?(\d))?$/.exec(limpio)
  if (!m) return false
  const pesos = [3, 7, 13, 17, 19, 23, 29, 37, 41]
  const cuerpo = m[1]!
  const suma = [...cuerpo].reverse().reduce((s, d, i) => s + Number(d) * pesos[i]!, 0)
  const r = suma % 11
  const dv = r > 1 ? 11 - r : r
  return m[2] === undefined || Number(m[2]) === dv
}

const contrasenaAceptable = (c: string) => c.length >= 10 && /[a-zA-Z]/.test(c) && /\d/.test(c)

/**
 * Sprint 10 · Autoservicio de alta: la empresa se registra sola y arranca 30 días de prueba gratuita.
 * Dos pasos cortos (su empresa y su cuenta); al terminar entra a la consola con el recorrido guiado.
 */
export function Registro() {
  const navegar = useNavigate()
  const sesion = useSesion()
  const registrar = useRegistrarEmpresa()
  const [paso, setPaso] = useState<1 | 2>(1)
  const [f, setF] = useState<RegistroEmpresa>({ empresa: '', nit: '', esTemporal: false, sector: '', tamano: '', nombre: '', correo: '', contrasena: '', aceptaTerminos: false })
  const [repetida, setRepetida] = useState('')
  const [ver, setVer] = useState(false)
  const poner = (c: Partial<RegistroEmpresa>) => setF((x) => ({ ...x, ...c }))

  if (sesion.data) return <Navigate to="/consola" replace />

  const nitOk = nitValido(f.nit)
  const paso1Listo = f.empresa.trim().length > 1 && nitOk && f.sector !== '' && f.tamano !== ''
  const coinciden = f.contrasena.length > 0 && f.contrasena === repetida
  const paso2Listo = f.nombre.trim() !== '' && /\S+@\S+\.\S+/.test(f.correo) && contrasenaAceptable(f.contrasena) && coinciden && f.aceptaTerminos

  const enviar = (e: FormEvent) => {
    e.preventDefault()
    if (paso === 1) {
      if (paso1Listo) setPaso(2)
      return
    }
    registrar.mutate({ ...f, empresa: f.empresa.trim(), nombre: f.nombre.trim(), correo: f.correo.trim() }, { onSuccess: () => navegar('/consola', { replace: true }) })
  }

  return (
    <div className="ingresar">
      <aside className="ingresar-marca">
        <HojasCayendo cantidad={9} className="hojas-claras" />
        <Link to="/" className="ingresar-logo" aria-label="Selvana, volver al inicio"><LogoAnimado tamano={36} palabra claro /></Link>
        <div className="ingresar-promesa">
          <LogoAnimado tamano={132} claro className="ingresar-logo-grande" />
          <h1>Pruebe Selvana 30 días, gratis y sin tarjeta.</h1>
          <ul>
            <li style={{ '--i': 0 } as CSSProperties}><Chispa />Su primera vacante, redactada por la IA en minutos.</li>
            <li style={{ '--i': 1 } as CSSProperties}><IconoCheck />Un recorrido guiado le muestra cada paso.</li>
            <li style={{ '--i': 2 } as CSSProperties}><IconoReloj />Al terminar la prueba, usted decide si sigue.</li>
          </ul>
        </div>
        <span className="ingresar-pie">Datos tratados según la Ley 1581 de 2012</span>
      </aside>

      <main className="ingresar-panel">
        <form className="ingresar-form" onSubmit={enviar} noValidate>
          <LogoAnimado tamano={44} className="ingresar-logo-movil" />
          <span className="mp-ceja">Paso <span className="mono">{paso}</span> de <span className="mono">2</span></span>
          <h2>{paso === 1 ? 'Su empresa' : 'Su cuenta'}</h2>
          <p>{paso === 1 ? 'Con esto preparamos Selvana para su sector.' : 'Usted queda como administrador: después invita a su equipo.'}</p>
          <div className="mp-pasos-barra" aria-hidden="true"><span style={{ width: paso === 1 ? '50%' : '100%' }} /></div>

          {paso === 1 ? (
            <div key="paso-1" className="mp-form-paso">
              <label className="postular-campo">Razón social
                <input className="entrada" autoComplete="organization" value={f.empresa} onChange={(e) => poner({ empresa: e.target.value })} required autoFocus />
              </label>
              <label className="postular-campo">NIT
                <input className="entrada mono" inputMode="numeric" placeholder="900.412.887-9" value={f.nit} onChange={(e) => poner({ nit: e.target.value })}
                  aria-invalid={f.nit.length > 8 && !nitOk ? true : undefined} required />
                {f.nit.length > 8 && !nitOk && <span className="postular-nota-error">Revise el NIT: 9 dígitos y el dígito de verificación.</span>}
              </label>
              <div className="dos-campos">
                <label className="postular-campo">Sector
                  <select className="selector entrada" value={f.sector} onChange={(e) => poner({ sector: e.target.value, esTemporal: e.target.value === sectores[0] })} required>
                    <option value="">Elija…</option>
                    {sectores.map((s) => <option key={s}>{s}</option>)}
                  </select>
                </label>
                <label className="postular-campo">Personas en la empresa
                  <select className="selector entrada" value={f.tamano} onChange={(e) => poner({ tamano: e.target.value })} required>
                    <option value="">Elija…</option>
                    {tamanos.map((t) => <option key={t}>{t}</option>)}
                  </select>
                </label>
              </div>
              <label className="postular-autorizacion">
                <input type="checkbox" checked={f.esTemporal} onChange={(e) => poner({ esTemporal: e.target.checked })} />
                <span>Somos una empresa de servicios temporales (EST): seleccionamos para empresas usuarias.</span>
              </label>
              <Boton type="submit" variante="primario" className="ingresar-entrar" disabled={!paso1Listo}>Continuar <IconoFlecha /></Boton>
            </div>
          ) : (
            <div key="paso-2" className="mp-form-paso">
              <label className="postular-campo">Su nombre
                <input className="entrada" autoComplete="name" value={f.nombre} onChange={(e) => poner({ nombre: e.target.value })} required autoFocus />
              </label>
              <label className="postular-campo">Correo de trabajo
                <input className="entrada" type="email" autoComplete="username" placeholder="nombre@empresa.co" value={f.correo} onChange={(e) => poner({ correo: e.target.value })} required />
              </label>
              <label className="postular-campo">Contraseña <span className="postular-opcional">10 o más caracteres, con letras y números</span>
                <span className="ingresar-contrasena">
                  <input className="entrada" type={ver ? 'text' : 'password'} autoComplete="new-password" value={f.contrasena}
                    onChange={(e) => poner({ contrasena: e.target.value })} aria-invalid={f.contrasena.length > 0 && !contrasenaAceptable(f.contrasena) ? true : undefined} required />
                  <button type="button" className="ingresar-ver" onClick={() => setVer((v) => !v)} aria-pressed={ver}>{ver ? 'Ocultar' : 'Ver'}</button>
                </span>
              </label>
              <label className="postular-campo">Repita la contraseña
                <input className="entrada" type={ver ? 'text' : 'password'} autoComplete="new-password" value={repetida} onChange={(e) => setRepetida(e.target.value)}
                  aria-invalid={repetida.length > 0 && !coinciden ? true : undefined} required />
                {repetida.length > 0 && !coinciden && <span className="postular-nota-error">Las contraseñas no coinciden.</span>}
              </label>
              <label className={`postular-autorizacion ${f.aceptaTerminos ? 'aceptada' : ''}`}>
                <input type="checkbox" checked={f.aceptaTerminos} onChange={(e) => poner({ aceptaTerminos: e.target.checked })} />
                <span>Acepto los <Link to="/legal/terminos" target="_blank">términos de uso</Link> y la <Link to="/legal/tratamiento-de-datos" target="_blank">política de tratamiento de datos</Link> (Ley 1581 de 2012).</span>
              </label>
              {registrar.error && <div className="aviso aviso-alerta ingresar-error" role="alert">{registrar.error.message}</div>}
              <div className="mp-form-acciones">
                <Boton onClick={() => setPaso(1)} disabled={registrar.isPending}>Atrás</Boton>
                <Boton type="submit" variante="primario" className="ingresar-entrar" cargando={registrar.isPending} textoCargando="Creando su cuenta…" disabled={!paso2Listo}>
                  Empezar la prueba gratis <IconoFlecha />
                </Boton>
              </div>
            </div>
          )}

          <p className="ingresar-registro"><IconoEscudo />¿Su empresa ya usa Selvana? <Link to="/ingresar">Ingrese aquí</Link></p>
          <Link to="/" className="ingresar-volver">← Volver a selvana.co</Link>
        </form>
      </main>
    </div>
  )
}
