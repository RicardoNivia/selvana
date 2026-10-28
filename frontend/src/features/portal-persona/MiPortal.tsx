import { type FormEvent, useRef, useState } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { AutocompletarMunicipio } from '../../shared/ui/AutocompletarMunicipio'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoCheck, IconoEscudo, IconoFlecha, IconoSubir } from '../../shared/ui/Iconos'
import { rangoSalario } from '../portal/api'
import {
  type DatosEditables, type Pendiente, useAbrirPendiente, useActualizarMiHojaDeVida, useActualizarMisDatos, useHojaDeVidaPerfil, useIngresarPortal,
  useMiCuenta, useMiPortal, useOfertasParaTi, usePostularmeConMiPerfil, useRegistrarme, useSalirPortal, useSubirHojaDeVidaPerfil,
} from './api'
import './portal-persona.css'

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' })
const fechaHora = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const tiposArchivo = '.pdf,.docx,image/jpeg,image/png,image/webp'

/* ---------- Ingresar ---------- */

export function IngresarPortal() {
  const navegar = useNavigate()
  const ingresar = useIngresarPortal()
  const [correo, setCorreo] = useState('')
  const [contrasena, setContrasena] = useState('')

  return (
    <main className="portal-ancho mp-acceso">
      <form className="postular-cuerpo" onSubmit={(e) => { e.preventDefault(); ingresar.mutate({ correo, contrasena }, { onSuccess: () => navegar('/mi-portal') }) }}>
        <h1>Entra a tu portal</h1>
        <p className="postular-ayuda">Tus postulaciones, el chat con las empresas, tus pruebas y tu contrato, en un solo lugar.</p>
        <label className="postular-campo">Correo<input className="entrada" type="email" autoComplete="email" value={correo} onChange={(e) => setCorreo(e.target.value)} required /></label>
        <label className="postular-campo">Contraseña<input className="entrada" type="password" autoComplete="current-password" value={contrasena} onChange={(e) => setContrasena(e.target.value)} required /></label>
        {ingresar.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{ingresar.error.message}</p>}
        <Boton type="submit" variante="primario" cargando={ingresar.isPending} textoCargando="Entrando…">Entrar <IconoFlecha /></Boton>
        <p className="postular-ayuda">¿Primera vez? <Link to="/mi-portal/registro">Crea tu cuenta con tu hoja de vida</Link></p>
      </form>
    </main>
  )
}

/* ---------- Registro: hoja de vida → la IA la lee → confirma y crea la cuenta ---------- */

export function RegistroPortal() {
  const [borradorId, setBorradorId] = useState<string>()
  const [sinHojaDeVida, setSinHojaDeVida] = useState(false)
  const hv = useHojaDeVidaPerfil(borradorId)

  return (
    <main className="portal-ancho mp-acceso">
      {!borradorId && !sinHojaDeVida ? (
        <SubirHojaDeVida titulo="Crea tu cuenta" ayuda="Sube tu hoja de vida y la IA llena tu perfil. Tú solo revisas, escoges tu contraseña y listo."
          onSubida={setBorradorId} onSinHojaDeVida={() => setSinHojaDeVida(true)} />
      ) : borradorId && (hv.isPending || hv.data?.estado === 'Leyendo') ? (
        <LeyendoHojaDeVida />
      ) : (
        <ConfirmarRegistro borradorId={borradorId ?? null} leidos={hv.data?.datos ?? null} mensaje={hv.data?.mensaje ?? null}
          textoAutorizacion={hv.data?.textoAutorizacion} />
      )}
    </main>
  )
}

function SubirHojaDeVida({ titulo, ayuda, onSubida, onSinHojaDeVida }: { titulo: string; ayuda: string; onSubida: (id: string) => void; onSinHojaDeVida?: () => void }) {
  const subir = useSubirHojaDeVidaPerfil()
  const archivo = useRef<HTMLInputElement>(null)
  const enviar = (f?: File) => f && subir.mutate(f, { onSuccess: (r) => onSubida(r.id) })

  return (
    <section className="postular-cuerpo">
      <h1>{titulo}</h1>
      <p className="postular-ayuda">{ayuda}</p>
      <input ref={archivo} type="file" accept={tiposArchivo} hidden onChange={(e) => enviar(e.target.files?.[0])} />
      <button type="button" className={`postular-subir ${subir.isPending ? 'subiendo' : ''}`} disabled={subir.isPending} aria-busy={subir.isPending || undefined}
        onClick={() => archivo.current?.click()} onDragOver={(e) => e.preventDefault()} onDrop={(e) => { e.preventDefault(); enviar(e.dataTransfer.files[0]) }}>
        <span className="postular-subir-icono">{subir.isPending ? <span className="indicador" aria-hidden="true" /> : <IconoSubir />}</span>
        <b>{subir.isPending ? 'Subiendo…' : 'Toca para subir tu hoja de vida'}</b>
        <span>o arrástrala aquí · PDF, Word o una foto clara</span>
        <span className="pill pill-ia postular-subir-ia"><Chispa />La IA la lee por ti</span>
      </button>
      {subir.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{subir.error.message}</p>}
      {onSinHojaDeVida && <button type="button" className="enlace-boton" onClick={onSinHojaDeVida}>Crear mi cuenta sin hoja de vida por ahora</button>}
      <p className="postular-confianza"><IconoEscudo />Tu hoja de vida se guarda cifrada. No usamos tu edad, género, estado civil ni foto para evaluarte.</p>
    </section>
  )
}

function LeyendoHojaDeVida() {
  return (
    <section className="postular-cuerpo postular-leyendo" role="status">
      <div className="estado-agente"><span className="orbe trabajando" /><span>La IA está leyendo tu hoja de vida…</span></div>
      {[60, 40, 80].map((a, i) => <Esqueleto key={i} alto={a} />)}
    </section>
  )
}

function ConfirmarRegistro({ borradorId, leidos, mensaje, textoAutorizacion }: {
  borradorId: string | null
  leidos: { nombre: string; documento: string; celular: string; correo: string; ciudad: string; linkedIn: string } | null
  mensaje: string | null
  textoAutorizacion?: string
}) {
  const navegar = useNavigate()
  const registrar = useRegistrarme()
  const [correo, setCorreo] = useState(leidos?.correo ?? '')
  const [contrasena, setContrasena] = useState('')
  const [documento, setDocumento] = useState(leidos?.documento ?? '')
  const [datos, setDatos] = useState<DatosEditables>({ nombre: leidos?.nombre ?? '', celular: leidos?.celular ?? '', ciudad: '', linkedIn: leidos?.linkedIn ?? '' })
  const [ciudadCodigo, setCiudadCodigo] = useState<string | null>(null)
  const [acepta, setAcepta] = useState(false)
  const ia = (valor: string | undefined, actual: string) => Boolean(valor) && valor === actual

  const enviar = (e: FormEvent) => {
    e.preventDefault()
    registrar.mutate({ borradorId, correo, contrasena, documento, ...datos, aceptaAutorizacion: acepta }, { onSuccess: () => navegar('/mi-portal') })
  }

  return (
    <form className="postular-cuerpo" onSubmit={enviar}>
      <h1>Revisa tus datos</h1>
      {leidos
        ? <p className="postular-ayuda"><span className="pill pill-ia"><Chispa />En violeta</span> lo que leímos de tu hoja de vida. Corrígelo si hace falta.</p>
        : mensaje && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{mensaje}</p>}
      <div className="postular-grupo">
        <label className="postular-campo">Correo<input className={`entrada${ia(leidos?.correo, correo) ? ' entrada-ia' : ''}`} type="email" autoComplete="email" value={correo} onChange={(e) => setCorreo(e.target.value)} required /></label>
        <label className="postular-campo">Contraseña <span className="postular-opcional">mínimo 8 caracteres</span>
          <input className="entrada" type="password" autoComplete="new-password" minLength={8} value={contrasena} onChange={(e) => setContrasena(e.target.value)} required />
        </label>
        <label className="postular-campo">Nombre completo<input className={`entrada${ia(leidos?.nombre, datos.nombre) ? ' entrada-ia' : ''}`} value={datos.nombre} onChange={(e) => setDatos({ ...datos, nombre: e.target.value })} required /></label>
        <label className="postular-campo">Cédula<input className={`entrada mono${ia(leidos?.documento, documento) ? ' entrada-ia' : ''}`} inputMode="numeric" value={documento} onChange={(e) => setDocumento(e.target.value)} required /></label>
        <label className="postular-campo">Celular (WhatsApp)<input className={`entrada${ia(leidos?.celular, datos.celular) ? ' entrada-ia' : ''}`} inputMode="tel" value={datos.celular} onChange={(e) => setDatos({ ...datos, celular: e.target.value })} required /></label>
        <label className="postular-campo">Ciudad donde vives
          <AutocompletarMunicipio valor={{ codigo: ciudadCodigo, ciudad: datos.ciudad || (leidos?.ciudad ?? '') }} ia={Boolean(leidos?.ciudad) && !ciudadCodigo} requerido
            onElegir={(m, texto) => { setDatos({ ...datos, ciudad: m?.municipio ?? texto }); setCiudadCodigo(m?.codigo ?? null) }} />
        </label>
      </div>
      <p className="postular-nota-suave">Con tu correo y tu cédula unimos a tu cuenta las postulaciones que ya hiciste. Revisa que sean los mismos que diste al postularte.</p>
      <label className={`postular-autorizacion ${acepta ? 'aceptada' : ''}`}>
        <input type="checkbox" checked={acepta} onChange={(e) => setAcepta(e.target.checked)} />
        <span>{textoAutorizacion ?? 'Autorizo a Selvana a tratar mis datos personales para crear mi perfil, según la Ley 1581 de 2012.'}</span>
      </label>
      {registrar.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{registrar.error.message}</p>}
      <Boton type="submit" variante="primario" cargando={registrar.isPending} textoCargando="Creando tu cuenta…" disabled={!acepta || !ciudadCodigo}>
        Crear mi cuenta <IconoFlecha />
      </Boton>
      {!ciudadCodigo && <span className="postular-ayuda">Elige tu ciudad de la lista.</span>}
    </form>
  )
}

/* ---------- Mi portal ---------- */

type Pestana = 'pendientes' | 'postulaciones' | 'ofertas' | 'hv'

export function MiPortal() {
  const cuenta = useMiCuenta()
  const portal = useMiPortal()
  const salir = useSalirPortal()
  const navegar = useNavigate()
  const [pestana, setPestana] = useState<Pestana>('pendientes')

  if (cuenta.isError) return <Navigate to="/mi-portal/ingresar" replace />
  if (cuenta.isPending) return <main className="portal-ancho mp"><Esqueleto alto={40} ancho="50%" /><Esqueleto alto={200} style={{ marginTop: 16 }} /></main>

  const pendientes = portal.data?.pendientes ?? []
  const pestanas: [Pestana, string][] = [
    ['pendientes', `Pendientes${pendientes.length ? ` · ${pendientes.length}` : ''}`],
    ['postulaciones', `Mis postulaciones${portal.data ? ` · ${portal.data.postulaciones.length}` : ''}`],
    ['ofertas', 'Ofertas para ti'],
    ['hv', 'Mi hoja de vida'],
  ]

  return (
    <main className="portal-ancho mp">
      <header className="mp-cabeza">
        <div>
          <h1>Hola, {cuenta.data.datos.nombre.split(' ')[0]}</h1>
          <p className="postular-ayuda">{pendientes.length ? `Tienes ${pendientes.length} ${pendientes.length === 1 ? 'cosa pendiente' : 'cosas pendientes'}.` : 'No tienes nada pendiente.'}</p>
        </div>
        <Boton pequeno cargando={salir.isPending} textoCargando="Saliendo…" onClick={() => salir.mutate(undefined, { onSettled: () => navegar('/mi-portal/ingresar') })}>Salir</Boton>
      </header>

      <div role="tablist" aria-label="Mi portal" className="pestanas mp-pestanas">
        {pestanas.map(([clave, texto]) => (
          <button key={clave} type="button" role="tab" className="pestana" aria-selected={pestana === clave} onClick={() => setPestana(clave)}>{texto}</button>
        ))}
      </div>

      {pestana === 'pendientes' && <Pendientes pendientes={pendientes} cargando={portal.isPending} />}
      {pestana === 'postulaciones' && <Postulaciones />}
      {pestana === 'ofertas' && <Ofertas />}
      {pestana === 'hv' && <MiHojaDeVida />}
    </main>
  )
}

const iconoPendiente: Record<Pendiente['tipo'], string> = { Chat: 'Chat', Pruebas: 'Pruebas', Oferta: 'Contratación' }

function Pendientes({ pendientes, cargando }: { pendientes: Pendiente[]; cargando: boolean }) {
  const abrir = useAbrirPendiente()
  const navegar = useNavigate()
  const [abriendo, setAbriendo] = useState<string>()

  if (cargando) return <div className="mp-lista">{[1, 2].map((i) => <Esqueleto key={i} alto={88} />)}</div>
  if (pendientes.length === 0) return <div className="tarjeta vacio mp-vacio"><IconoCheck /><p>Estás al día. Aquí te aparecen el chat de las empresas, tus pruebas y tu contrato cuando te los envíen.</p></div>

  return (
    <div className="mp-lista">
      {abrir.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{abrir.error.message}</p>}
      {pendientes.map((p) => (
        <article key={`${p.tipo}-${p.id}`} className="tarjeta mp-tarjeta mp-pendiente">
          <div>
            <span className="pill pill-alerta">{iconoPendiente[p.tipo]}</span>
            <h3>{p.titulo}</h3>
            <p>{p.vacante} · {p.empresa}{p.venceEn ? <> · vence el <span className="mono">{fechaHora.format(new Date(p.venceEn))}</span></> : null}</p>
          </div>
          <Boton variante="primario" cargando={abrir.isPending && abriendo === p.id} textoCargando="Abriendo…" disabled={abrir.isPending}
            onClick={() => { setAbriendo(p.id); abrir.mutate(p, { onSuccess: (r) => navegar(r.ruta) }) }}>
            Abrir <IconoFlecha />
          </Boton>
        </article>
      ))}
    </div>
  )
}

const claseEstado = (estado: string) =>
  estado === 'Contratada' || estado === 'Finalista' ? 'pill-ok' : estado === 'No seleccionada' || estado.includes('cerrad') ? 'pill-neutra' : 'pill-info'

function Postulaciones() {
  const portal = useMiPortal()
  if (portal.isPending) return <div className="mp-lista">{[1, 2, 3].map((i) => <Esqueleto key={i} alto={72} />)}</div>
  const lista = portal.data?.postulaciones ?? []
  if (lista.length === 0) {
    return (
      <div className="tarjeta vacio mp-vacio">
        <p>Aún no vemos postulaciones con tu correo y tu cédula. Si te postulaste con otros datos, escríbenos desde <Link to="/empleos/mis-datos">tus datos personales</Link>.</p>
        <Link to="/empleos" className="boton boton-secundario">Ver vacantes</Link>
      </div>
    )
  }
  return (
    <div className="mp-lista">
      {lista.map((p) => (
        <article key={p.postulacionId} className="tarjeta mp-tarjeta">
          <div>
            <h3><Link to={`/empleos/${p.vacanteCodigo}`}>{p.vacante}</Link></h3>
            <p>{p.empresa} · {p.ciudad} · <span className="mono">{p.numero}</span> · {fecha.format(new Date(p.recibidaEn))}</p>
            <p className="mp-explicacion">{p.explicacion}</p>
          </div>
          <span className={`pill ${claseEstado(p.estado)}`}>{p.estado}</span>
        </article>
      ))}
    </div>
  )
}

function Ofertas() {
  const ofertas = useOfertasParaTi(true)
  const postular = usePostularmeConMiPerfil()
  const navegar = useNavigate()
  const [eligiendo, setEligiendo] = useState<string>()

  if (ofertas.isPending) return <div className="mp-lista">{[1, 2, 3].map((i) => <Esqueleto key={i} alto={110} />)}</div>
  if ((ofertas.data ?? []).length === 0) {
    return (
      <div className="tarjeta vacio mp-vacio">
        <p>Aún no encontramos vacantes que encajen con tu hoja de vida. Actualízala con tu experiencia y tus cursos, o mira todas las vacantes.</p>
        <Link to="/empleos" className="boton boton-secundario">Ver todas las vacantes</Link>
      </div>
    )
  }
  return (
    <div className="mp-lista">
      {postular.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{postular.error.message}</p>}
      {ofertas.data?.map((o) => (
        <article key={o.codigo} className="tarjeta mp-tarjeta">
          <div>
            <h3><Link to={`/empleos/${o.codigo}`}>{o.titulo}</Link></h3>
            <p>{o.empresa} · {o.ciudad} · {o.jornada} · <span className="mono">{rangoSalario(o.salarioMinimo, o.salarioMaximo)}</span></p>
            <ul className="mp-porque">{o.porque.map((r) => <li key={r}><IconoCheck />{r}</li>)}</ul>
          </div>
          <Boton variante="primario" cargando={postular.isPending && eligiendo === o.codigo} textoCargando="Preparando…" disabled={postular.isPending}
            onClick={() => { setEligiendo(o.codigo); postular.mutate(o.codigo, { onSuccess: (r) => navegar(`/empleos/${o.codigo}/postular?borrador=${r.id}`) }) }}>
            Postularme
          </Boton>
        </article>
      ))}
    </div>
  )
}

function MiHojaDeVida() {
  const cuenta = useMiCuenta()
  const actualizarDatos = useActualizarMisDatos()
  const actualizarHv = useActualizarMiHojaDeVida()
  const [nuevaHv, setNuevaHv] = useState<string>()
  const hv = useHojaDeVidaPerfil(nuevaHv)
  const c = cuenta.data!
  const [datos, setDatos] = useState<DatosEditables>({ nombre: c.datos.nombre, celular: c.datos.celular, ciudad: c.datos.ciudad, linkedIn: c.datos.linkedIn })
  const [ciudadCodigo, setCiudadCodigo] = useState<string | null>(c.datos.ciudad ? 'guardada' : null)
  const [guardado, setGuardado] = useState(false)

  const leyendo = Boolean(nuevaHv) && (hv.isPending || hv.data?.estado === 'Leyendo')
  const leida = nuevaHv && hv.data && hv.data.estado !== 'Leyendo' ? hv.data : null

  return (
    <div className="mp-lista">
      <section className="tarjeta mp-seccion">
        <h3>Tu hoja de vida</h3>
        <p className="postular-ayuda">
          {c.nombreArchivoHojaDeVida
            ? <>Tienes <b>{c.nombreArchivoHojaDeVida}</b>{c.hojaDeVidaActualizadaEn && <>, actualizada el {fecha.format(new Date(c.hojaDeVidaActualizadaEn))}</>}. La usamos para postularte con un clic.</>
            : 'Aún no has subido tu hoja de vida. Súbela para postularte con un clic y ver ofertas que encajen contigo.'}
        </p>
        {!nuevaHv && <SubirHojaDeVida titulo="" ayuda="Sube una versión nueva y la IA actualiza tu experiencia, estudios y habilidades." onSubida={setNuevaHv} />}
        {leyendo && <LeyendoHojaDeVida />}
        {leida && (
          <div className="mp-confirmar">
            {leida.estado === 'Fallida' && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{leida.mensaje}</p>}
            {leida.datos && <p className="postular-ayuda"><span className="pill pill-ia"><Chispa />Leído por la IA</span> {leida.datos.experiencia.length} experiencias, {leida.datos.formacion.length} estudios y {leida.datos.habilidades.length} habilidades.</p>}
            {actualizarHv.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{actualizarHv.error.message}</p>}
            <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
              <Boton variante="primario" cargando={actualizarHv.isPending} textoCargando="Guardando…"
                onClick={() => actualizarHv.mutate({ borradorId: nuevaHv!, ...datos }, { onSuccess: () => setNuevaHv(undefined) })}>
                Usar esta hoja de vida
              </Boton>
              <Boton onClick={() => setNuevaHv(undefined)} disabled={actualizarHv.isPending}>Cancelar</Boton>
            </div>
          </div>
        )}
      </section>

      <form className="tarjeta mp-seccion" onSubmit={(e) => { e.preventDefault(); actualizarDatos.mutate(datos, { onSuccess: () => setGuardado(true) }) }}>
        <h3>Tus datos</h3>
        <div className="postular-grupo">
          <label className="postular-campo">Nombre completo<input className="entrada" value={datos.nombre} onChange={(e) => { setDatos({ ...datos, nombre: e.target.value }); setGuardado(false) }} required /></label>
          <label className="postular-campo">Celular (WhatsApp)<input className="entrada" inputMode="tel" value={datos.celular} onChange={(e) => { setDatos({ ...datos, celular: e.target.value }); setGuardado(false) }} required /></label>
          <label className="postular-campo">Ciudad donde vives
            <AutocompletarMunicipio valor={{ codigo: ciudadCodigo, ciudad: datos.ciudad }} requerido
              onElegir={(m, texto) => { setDatos({ ...datos, ciudad: m?.municipio ?? texto }); setCiudadCodigo(m?.codigo ?? null); setGuardado(false) }} />
          </label>
          <label className="postular-campo">Perfil de LinkedIn <span className="postular-opcional">opcional</span>
            <input className="entrada" inputMode="url" value={datos.linkedIn ?? ''} onChange={(e) => { setDatos({ ...datos, linkedIn: e.target.value }); setGuardado(false) }} />
          </label>
        </div>
        <p className="postular-nota-suave">Tu correo (<b>{c.correo}</b>) y tu cédula (<span className="mono">{c.datos.documento}</span>) unen tu cuenta con tus postulaciones; para cambiarlos, pídelo desde <Link to="/empleos/mis-datos">tus datos personales</Link>.</p>
        {actualizarDatos.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{actualizarDatos.error.message}</p>}
        <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
          <Boton type="submit" variante="primario" cargando={actualizarDatos.isPending} textoCargando="Guardando…" disabled={!ciudadCodigo}>Guardar mis datos</Boton>
          {guardado && <span className="pill pill-ok"><IconoCheck />Guardado</span>}
        </div>
      </form>

      {(c.datos.experiencia.length > 0 || c.datos.formacion.length > 0) && (
        <section className="tarjeta mp-seccion">
          <h3>Lo que leímos de tu hoja de vida</h3>
          {c.datos.experiencia.length > 0 && <ul className="mp-hv">{c.datos.experiencia.map((x, i) => <li key={i}><b>{x.cargo}</b> · {x.empresa} <span className="mono">{x.desde} – {x.hasta}</span></li>)}</ul>}
          {c.datos.formacion.length > 0 && <ul className="mp-hv">{c.datos.formacion.map((x, i) => <li key={i}><b>{x.titulo}</b> · {x.institucion} <span className="mono">{x.anio}</span></li>)}</ul>}
          {c.datos.habilidades.length > 0 && <p className="postular-ayuda">Habilidades: {c.datos.habilidades.join(', ')}</p>}
        </section>
      )}
    </div>
  )
}
