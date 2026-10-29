import { type CSSProperties, type FormEvent, type ReactNode, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { HojasCayendo } from '../../shared/ui/HojasCayendo'
import { IconoAlerta, IconoCheck, IconoChat, IconoEscudo, IconoFlecha, IconoPrueba } from '../../shared/ui/Iconos'
import { LogoAnimado } from '../../shared/ui/LogoAnimado'
import { type Registro, type TipoDocumento, tiposDocumento, useIngresarPortal, useRegistrarme } from './api'
import './portal-persona.css'

/** A dónde volver después de entrar o registrarse (por ejemplo, la vacante a la que se iba a postular). Solo rutas internas. */
function useVolver() {
  const v = new URLSearchParams(useLocation().search).get('volver')
  return v && v.startsWith('/') && !v.startsWith('//') ? v : '/mi-portal'
}

const conVolver = (ruta: string, volver: string) => (volver === '/mi-portal' ? ruta : `${ruta}?volver=${encodeURIComponent(volver)}`)

/** Marco de entrar y registrarse: panel de marca con hojas a la izquierda y el formulario a la derecha (en celular, solo el formulario). */
function MarcoAcceso({ titulo, children }: { titulo: string; children: ReactNode }) {
  const promesas: [ReactNode, string][] = [
    [<IconoCheck key="a" />, 'Sigues cada proceso paso a paso'],
    [<IconoChat key="b" />, 'Respondes el chat de las empresas'],
    [<IconoPrueba key="c" />, 'Presentas tus pruebas y firmas tu contrato'],
  ]
  return (
    <main className="portal-ancho mp-acceso">
      <div className="mp-acceso-tarjeta">
        <aside className="mp-acceso-marca">
          <HojasCayendo cantidad={8} className="hojas-claras" />
          <LogoAnimado tamano={88} claro />
          <h2>Tu búsqueda de empleo, en un solo lugar.</h2>
          <ul>
            {promesas.map(([icono, texto], i) => <li key={texto} style={{ '--i': i } as CSSProperties}>{icono}{texto}</li>)}
          </ul>
          <span className="mp-acceso-ley"><IconoEscudo />Tus datos se tratan según la Ley 1581 de 2012.</span>
        </aside>
        <section className="mp-acceso-form" aria-label={titulo}>{children}</section>
      </div>
    </main>
  )
}

/** Contraseña con el botón «Ver» para revisarla antes de enviar. */
function CampoContrasena({ etiqueta, valor, onCambiar, nueva, invalida, extra }: {
  etiqueta: ReactNode; valor: string; onCambiar: (v: string) => void; nueva?: boolean; invalida?: boolean; extra?: ReactNode
}) {
  const [ver, setVer] = useState(false)
  return (
    <label className="postular-campo">{etiqueta}
      <span className="ingresar-contrasena">
        <input className="entrada" type={ver ? 'text' : 'password'} autoComplete={nueva ? 'new-password' : 'current-password'} minLength={nueva ? 8 : undefined}
          value={valor} onChange={(e) => onCambiar(e.target.value)} aria-invalid={invalida || undefined} required />
        <button type="button" className="ingresar-ver" aria-pressed={ver} onClick={() => setVer((v) => !v)}>{ver ? 'Ocultar' : 'Ver'}</button>
      </span>
      {extra}
    </label>
  )
}

export function IngresarPortal() {
  const navegar = useNavigate()
  const volver = useVolver()
  const ingresar = useIngresarPortal()
  const [correo, setCorreo] = useState('')
  const [contrasena, setContrasena] = useState('')

  return (
    <MarcoAcceso titulo="Entrar a tu portal">
      <form className="mp-form" onSubmit={(e) => { e.preventDefault(); ingresar.mutate({ correo: correo.trim(), contrasena }, { onSuccess: () => navegar(volver) }) }}>
        <header className="mp-form-cabeza">
          <span className="mp-ceja">Mi portal</span>
          <h1>Qué bueno verte</h1>
          <p>Entra para ver tus procesos, tus pendientes y las ofertas que encajan contigo.</p>
        </header>
        <label className="postular-campo">Correo<input className="entrada" type="email" autoComplete="email" placeholder="tu@correo.com" value={correo} onChange={(e) => setCorreo(e.target.value)} required autoFocus /></label>
        <CampoContrasena etiqueta="Contraseña" valor={contrasena} onCambiar={setContrasena} />
        {ingresar.error && <p role="alert" className="aviso aviso-alerta ingresar-error"><IconoAlerta />{ingresar.error.message}</p>}
        <Boton type="submit" variante="primario" className="mp-form-enviar" cargando={ingresar.isPending} textoCargando="Entrando…">Entrar <IconoFlecha /></Boton>
        <p className="mp-form-cambiar">¿Primera vez? <Link to={conVolver('/mi-portal/registro', volver)}>Crea tu cuenta en un minuto</Link></p>
      </form>
    </MarcoAcceso>
  )
}

/* ---------- Registro en dos pasos: quién eres y tu contraseña; la hoja de vida se sube después en el portal ---------- */

const textoAutorizacion =
  'Autorizo a Selvana a tratar mis datos personales (hoja de vida y datos de contacto) para crear mi perfil, mostrarme vacantes que encajen conmigo, ' +
  'permitirme postularme y ver el estado de mis procesos, según la Ley 1581 de 2012. Al postularme a una vacante autorizo también a la empresa que la ' +
  'publica. Puedo consultar, actualizar o pedir que se supriman mis datos en cualquier momento.'

/** Qué tan segura es la contraseña, solo por largo y variedad; nunca bloquea más allá del mínimo de 8. */
function fuerza(c: string): [number, string] {
  if (c.length < 8) return [c.length ? 1 : 0, c.length ? `Faltan ${8 - c.length} caracteres` : 'Mínimo 8 caracteres']
  const variedad = [/[a-z]/, /[A-Z]/, /\d/, /[^A-Za-z0-9]/].filter((r) => r.test(c)).length
  return c.length >= 12 && variedad >= 3 ? [3, 'Muy buena'] : variedad >= 2 ? [2, 'Buena'] : [2, 'Aceptable: mezcla letras y números']
}

export function RegistroPortal() {
  const navegar = useNavigate()
  const volver = useVolver()
  const registrar = useRegistrarme()
  const [paso, setPaso] = useState<1 | 2>(1)
  const [f, setF] = useState<Registro>({ correo: '', contrasena: '', nombre: '', tipoDocumento: 'CC', numeroDocumento: '', aceptaAutorizacion: false })
  const poner = (cambio: Partial<Registro>) => setF((x) => ({ ...x, ...cambio }))
  const [nivel, textoNivel] = fuerza(f.contrasena)
  const [repetida, setRepetida] = useState('')
  const coinciden = f.contrasena.length > 0 && f.contrasena === repetida

  const enviar = (e: FormEvent) => {
    e.preventDefault()
    if (paso === 1) setPaso(2)
    else registrar.mutate(f, { onSuccess: () => navegar(volver) })
  }

  return (
    <MarcoAcceso titulo="Crear tu cuenta">
      <form className="mp-form" onSubmit={enviar}>
        <header className="mp-form-cabeza">
          <span className="mp-ceja">Paso <span className="mono">{paso}</span> de <span className="mono">2</span></span>
          <h1>{paso === 1 ? 'Crea tu cuenta' : 'Protege tu cuenta'}</h1>
          <p>{paso === 1
            ? 'Con tu cuenta sigues tus postulaciones, respondes el chat de las empresas y firmas tu contrato.'
            : 'Elige una contraseña y autoriza el tratamiento de tus datos. Después subes tu hoja de vida y la IA llena tu perfil.'}</p>
          <div className="mp-pasos-barra" aria-hidden="true"><span style={{ width: paso === 1 ? '50%' : '100%' }} /></div>
        </header>

        {paso === 1 ? (
          <div key="paso-1" className="mp-form-paso">
            <label className="postular-campo">Nombres y apellidos<input className="entrada" autoComplete="name" value={f.nombre} onChange={(e) => poner({ nombre: e.target.value })} required autoFocus /></label>
            <label className="postular-campo">Correo<input className="entrada" type="email" autoComplete="email" placeholder="tu@correo.com" value={f.correo} onChange={(e) => poner({ correo: e.target.value })} required /></label>
            <div className="mp-identificacion">
              <label className="postular-campo">Tipo de identificación
                <select className="selector entrada" value={f.tipoDocumento} onChange={(e) => poner({ tipoDocumento: e.target.value as TipoDocumento })}>
                  {tiposDocumento.map(([v, t]) => <option key={v} value={v}>{t}</option>)}
                </select>
              </label>
              <label className="postular-campo">Número<input className="entrada mono" inputMode={f.tipoDocumento === 'PA' ? 'text' : 'numeric'} value={f.numeroDocumento} onChange={(e) => poner({ numeroDocumento: e.target.value })} required /></label>
            </div>
            <p className="mp-nota">Usa el mismo correo y la misma identificación con que te postulaste: así unimos a tu cuenta las postulaciones que ya hiciste.</p>
            <Boton type="submit" variante="primario" className="mp-form-enviar">Continuar <IconoFlecha /></Boton>
          </div>
        ) : (
          <div key="paso-2" className="mp-form-paso">
            <CampoContrasena nueva etiqueta={<>Contraseña <span className="postular-opcional">mínimo 8 caracteres</span></>} valor={f.contrasena} onCambiar={(v) => poner({ contrasena: v })}
              extra={
                <span className="mp-fuerza" data-nivel={nivel}>
                  <span className="mp-fuerza-barras" aria-hidden="true"><i /><i /><i /></span>
                  <span>{textoNivel}</span>
                </span>
              } />
            <CampoContrasena nueva etiqueta="Repite la contraseña" valor={repetida} onCambiar={setRepetida} invalida={repetida.length > 0 && !coinciden}
              extra={repetida.length > 0 && !coinciden && <span className="postular-nota-error">Las contraseñas no coinciden.</span>} />
            <label className={`postular-autorizacion ${f.aceptaAutorizacion ? 'aceptada' : ''}`}>
              <input type="checkbox" checked={f.aceptaAutorizacion} onChange={(e) => poner({ aceptaAutorizacion: e.target.checked })} />
              <span>{textoAutorizacion}</span>
            </label>
            {registrar.error && <p role="alert" className="aviso aviso-alerta ingresar-error"><IconoAlerta />{registrar.error.message}</p>}
            <div className="mp-form-acciones">
              <Boton onClick={() => setPaso(1)} disabled={registrar.isPending}>Atrás</Boton>
              <Boton type="submit" variante="primario" className="mp-form-enviar" cargando={registrar.isPending} textoCargando="Creando tu cuenta…" disabled={!f.aceptaAutorizacion || !coinciden}>
                Crear mi cuenta <IconoFlecha />
              </Boton>
            </div>
          </div>
        )}
        <p className="mp-form-cambiar">¿Ya tienes cuenta? <Link to={conVolver('/mi-portal/ingresar', volver)}>Entra aquí</Link></p>
      </form>
    </MarcoAcceso>
  )
}
