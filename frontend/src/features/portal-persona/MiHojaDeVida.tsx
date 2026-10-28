import { type CSSProperties, type DragEvent, type KeyboardEvent, type ReactNode, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { AutocompletarMunicipio } from '../../shared/ui/AutocompletarMunicipio'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoBirrete, IconoCerrar, IconoCheck, IconoDocumento, IconoHerramienta, IconoMas, IconoPersonas, IconoSubir, IconoVacantes } from '../../shared/ui/Iconos'
import {
  type MiCuenta, type PerfilEditable, useActualizarMiHojaDeVida, useActualizarMiPerfil, useHojaDeVidaPerfil, useSubirHojaDeVidaPerfil,
} from './api'
import type { SeccionHv } from './perfil'
import { HojaDeVidaPdf } from './HojaDeVidaPdf'

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' })
const tiposArchivo = '.pdf,.docx,image/jpeg,image/png,image/webp'

const partes: { clave: SeccionHv; texto: string; icono: ReactNode }[] = [
  { clave: 'datos', texto: 'Datos personales', icono: <IconoPersonas /> },
  { clave: 'experiencia', texto: 'Experiencia', icono: <IconoVacantes /> },
  { clave: 'estudios', texto: 'Estudios', icono: <IconoBirrete /> },
  { clave: 'cursos', texto: 'Cursos y habilidades', icono: <IconoHerramienta /> },
]

/**
 * Su hoja de vida: subir una nueva (hoja de vida o el PDF de LinkedIn) para que la IA la lea, o escribirla y corregirla a mano.
 * Lo que leyó la IA llena el formulario en violeta; nada se guarda hasta que la persona pulsa «Guardar».
 */
export function MiHojaDeVida({ cuenta: c, seccionInicial }: { cuenta: MiCuenta; seccionInicial?: SeccionHv }) {
  const guardar = useActualizarMiPerfil()
  const actualizarHv = useActualizarMiHojaDeVida()
  const [nuevaHv, setNuevaHv] = useState<string>()
  const hv = useHojaDeVidaPerfil(nuevaHv)
  const inicial = (): PerfilEditable => ({
    nombre: c.datos.nombre, celular: c.datos.celular, ciudad: c.datos.ciudad, linkedIn: c.datos.linkedIn,
    experiencia: c.datos.experiencia, formacion: c.datos.formacion, cursos: c.datos.cursos.filter(Boolean), habilidades: c.datos.habilidades.filter(Boolean),
  })
  const [p, setP] = useState<PerfilEditable>(inicial)
  const [deIa, setDeIa] = useState(false)
  const [sucio, setSucio] = useState(false)
  const [ciudadCodigo, setCiudadCodigo] = useState<string | null>(c.datos.ciudad ? 'guardada' : null)
  const [guardado, setGuardado] = useState(false)
  const [seccion, setSeccion] = useState<SeccionHv>(seccionInicial ?? 'datos')
  const [nuevaFila, setNuevaFila] = useState<string>()
  const [verCv, setVerCv] = useState(false)
  const poner = (cambio: Partial<PerfilEditable>) => { setP((x) => ({ ...x, ...cambio })); setSucio(true); setGuardado(false) }

  useEffect(() => {
    if (!guardado) return
    const t = setTimeout(() => setGuardado(false), 3500)
    return () => clearTimeout(t)
  }, [guardado])

  // Cuando la IA termina de leer la hoja de vida nueva, llena el formulario en violeta para revisarlo
  const leida = nuevaHv && hv.data && hv.data.estado !== 'Leyendo' ? hv.data : null
  const leyendo = Boolean(nuevaHv) && !leida
  const [aplicada, setAplicada] = useState<string>()
  if (leida?.datos && aplicada !== leida.id) {
    const d = leida.datos
    setAplicada(leida.id)
    setDeIa(true)
    setSucio(true)
    setP((x) => ({
      nombre: x.nombre || d.nombre, celular: x.celular || d.celular, ciudad: x.ciudad || d.ciudad, linkedIn: x.linkedIn || d.linkedIn,
      experiencia: d.experiencia, formacion: d.formacion, cursos: d.cursos.filter(Boolean), habilidades: d.habilidades.filter(Boolean),
    }))
  }

  const listo = () => { setNuevaHv(undefined); setDeIa(false); setSucio(false); setGuardado(true) }
  const enviar = () => {
    // Con una hoja de vida nueva, se guarda el archivo y lo corregido; si no, solo lo escrito a mano
    if (nuevaHv && leida) actualizarHv.mutate({ borradorId: nuevaHv, ...p }, { onSuccess: () => guardar.mutate(p, { onSuccess: listo }) })
    else guardar.mutate(p, { onSuccess: listo })
  }
  const descartar = () => { setNuevaHv(undefined); setDeIa(false); setSucio(false); setP(inicial()) }

  const error = actualizarHv.error ?? guardar.error
  const ocupado = actualizarHv.isPending || guardar.isPending
  const ia = deIa ? ' entrada-ia' : ''
  const cuenta: Record<SeccionHv, number | null> = {
    datos: [p.nombre, p.celular, p.ciudad].filter(Boolean).length === 3 ? null : 0,
    experiencia: p.experiencia.length, estudios: p.formacion.length, cursos: p.cursos.length + p.habilidades.length,
  }
  const agregar = (tipo: 'experiencia' | 'estudios') => {
    if (tipo === 'experiencia') { poner({ experiencia: [...p.experiencia, { cargo: '', empresa: '', desde: '', hasta: '' }] }); setNuevaFila(`e${p.experiencia.length}`) }
    else { poner({ formacion: [...p.formacion, { titulo: '', institucion: '', anio: '' }] }); setNuevaFila(`f${p.formacion.length}`) }
  }

  return (
    <div className="mp-hv">
      <section className={`mp-hv-fuente ${leida?.datos ? 'mp-hv-fuente-ia' : ''}`}>
        {(() => {
          const cabeza = (
            <>
              <span className="mp-hv-archivo-icono"><IconoDocumento /></span>
              <div className="mp-hv-archivo-texto">
                <h2>Tu hoja de vida</h2>
                <p>
                  {c.nombreArchivoHojaDeVida
                    ? <><b>{c.nombreArchivoHojaDeVida}</b>{c.hojaDeVidaActualizadaEn && <> · {fecha.format(new Date(c.hojaDeVidaActualizadaEn))}</>}</>
                    : 'Aún no la subes. La IA la lee y llena tu perfil por ti.'}
                </p>
              </div>
            </>
          )
          const verPdf = (c.datos.experiencia.length > 0 || c.datos.formacion.length > 0) && <Boton pequeno onClick={() => setVerCv(true)}>Ver en PDF</Boton>
          return nuevaHv
            ? <div className="mp-hv-archivo">{cabeza}{verPdf}</div>
            : <ZonaSubir onSubida={setNuevaHv} compacta={Boolean(c.nombreArchivoHojaDeVida)} cabeza={cabeza} acciones={verPdf} />
        })()}
        {verCv && <HojaDeVidaPdf cuenta={c} onCerrar={() => setVerCv(false)} />}
        {leyendo && (
          <div className="mp-hv-leyendo" role="status">
            <div className="estado-agente"><span className="orbe trabajando" /><span>La IA está leyendo tu hoja de vida…</span></div>
            {[44, 28, 56].map((a, i) => <Esqueleto key={i} alto={a} />)}
          </div>
        )}
        {leida?.estado === 'Fallida' && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{leida.mensaje} Escríbela abajo.</p>}
        {leida?.datos && (
          <p className="mp-hv-leida"><span className="pill pill-ia"><Chispa />Leída por la IA</span>Revisa lo que está en violeta, corrige lo que haga falta y guarda.</p>
        )}
      </section>

      <form className="mp-hv-editor" onSubmit={(e) => { e.preventDefault(); enviar() }}>
        <nav className="mp-hv-partes" role="tablist" aria-label="Partes de tu hoja de vida">
          {partes.map((x) => (
            <button key={x.clave} type="button" role="tab" aria-selected={seccion === x.clave} onClick={() => setSeccion(x.clave)}>
              <span className="mp-hv-partes-icono">{x.icono}</span>
              <span className="mp-hv-partes-texto">{x.texto}</span>
              {cuenta[x.clave] ? <span className="mp-hv-partes-cuenta mono">{cuenta[x.clave]}</span>
                : cuenta[x.clave] === null ? <span className="mp-hv-partes-ok"><IconoCheck /></span>
                : <span className="mp-hv-partes-falta" title="Falta completar" />}
            </button>
          ))}
        </nav>

        <div key={seccion} role="tabpanel" className="mp-hv-panel">
          {seccion === 'datos' && (
            <>
              <CabezaParte titulo="Datos personales" texto="Así te contactan las empresas. Tu correo y tu identificación no se cambian aquí." />
              <div className="mp-datos-grilla">
                <label className="postular-campo">Nombres y apellidos<input className={`entrada${ia}`} value={p.nombre} onChange={(e) => poner({ nombre: e.target.value })} required /></label>
                <label className="postular-campo">Celular (WhatsApp)<input className={`entrada mono${ia}`} inputMode="tel" placeholder="300 123 4567" value={p.celular} onChange={(e) => poner({ celular: e.target.value })} /></label>
                <label className="postular-campo">Ciudad donde vives
                  <AutocompletarMunicipio valor={{ codigo: ciudadCodigo, ciudad: p.ciudad }}
                    onElegir={(m, texto) => { poner({ ciudad: m?.municipio ?? texto }); setCiudadCodigo(m?.codigo ?? null) }} />
                </label>
                <label className="postular-campo">Perfil de LinkedIn <span className="postular-opcional">opcional</span>
                  <input className="entrada" inputMode="url" placeholder="linkedin.com/in/tu-nombre" value={p.linkedIn ?? ''} onChange={(e) => poner({ linkedIn: e.target.value })} />
                </label>
              </div>
              <div className="mp-fijos">
                <span><small>Correo</small><b>{c.correo}</b></span>
                <span><small>Identificación</small><b className="mono">{c.tipoDocumento} {c.datos.documento}</b></span>
                <Link to="/empleos/mis-datos">Pedir un cambio</Link>
              </div>
            </>
          )}

          {seccion === 'experiencia' && (
            <>
              <CabezaParte titulo="Experiencia" texto="Empieza por la más reciente. Si aún trabajas ahí, escribe «Actual» en Hasta." />
              {p.experiencia.length === 0 && <Vacio texto="Aún no tienes experiencia registrada. Si es tu primer empleo, cuéntanos tus prácticas o trabajos informales." />}
              <ol className="mp-entradas">
                {p.experiencia.map((x, i) => {
                  const cambiar = (k: keyof typeof x, v: string) => poner({ experiencia: p.experiencia.map((y, j) => (j === i ? { ...y, [k]: v } : y)) })
                  return (
                    <li key={i} className={`mp-entrada${ia}${nuevaFila === `e${i}` ? ' mp-entrada-nueva' : ''}`} style={{ '--i': i } as CSSProperties}>
                      <span className="mp-entrada-punto" aria-hidden="true" />
                      <div className="mp-entrada-campos">
                        <label className="postular-campo">Cargo<input className="entrada" value={x.cargo} placeholder="Auxiliar de bodega" autoFocus={nuevaFila === `e${i}`} onChange={(e) => cambiar('cargo', e.target.value)} /></label>
                        <label className="postular-campo">Empresa<input className="entrada" value={x.empresa} onChange={(e) => cambiar('empresa', e.target.value)} /></label>
                        <label className="postular-campo">Desde<input className="entrada mono" placeholder="03/2021" value={x.desde} onChange={(e) => cambiar('desde', e.target.value)} /></label>
                        <label className="postular-campo">Hasta<input className="entrada mono" placeholder="Actual" value={x.hasta} onChange={(e) => cambiar('hasta', e.target.value)} /></label>
                      </div>
                      <button type="button" className="mp-quitar" aria-label={`Quitar ${x.cargo || 'esta experiencia'}`} onClick={() => poner({ experiencia: p.experiencia.filter((_, j) => j !== i) })}><IconoCerrar /></button>
                    </li>
                  )
                })}
              </ol>
              <button type="button" className="mp-agregar" onClick={() => agregar('experiencia')}><IconoMas />Agregar experiencia</button>
            </>
          )}

          {seccion === 'estudios' && (
            <>
              <CabezaParte titulo="Estudios" texto="Bachillerato, técnico, tecnólogo, universidad o posgrado." />
              {p.formacion.length === 0 && <Vacio texto="Aún no tienes estudios registrados." />}
              <ol className="mp-entradas">
                {p.formacion.map((x, i) => {
                  const cambiar = (k: keyof typeof x, v: string) => poner({ formacion: p.formacion.map((y, j) => (j === i ? { ...y, [k]: v } : y)) })
                  return (
                    <li key={i} className={`mp-entrada mp-entrada-estudio${ia}${nuevaFila === `f${i}` ? ' mp-entrada-nueva' : ''}`} style={{ '--i': i } as CSSProperties}>
                      <span className="mp-entrada-punto" aria-hidden="true" />
                      <div className="mp-entrada-campos">
                        <label className="postular-campo">Título<input className="entrada" value={x.titulo} placeholder="Técnico en logística" autoFocus={nuevaFila === `f${i}`} onChange={(e) => cambiar('titulo', e.target.value)} /></label>
                        <label className="postular-campo">Institución<input className="entrada" value={x.institucion} onChange={(e) => cambiar('institucion', e.target.value)} /></label>
                        <label className="postular-campo">Año<input className="entrada mono" inputMode="numeric" placeholder="2022" value={x.anio} onChange={(e) => cambiar('anio', e.target.value)} /></label>
                      </div>
                      <button type="button" className="mp-quitar" aria-label={`Quitar ${x.titulo || 'este estudio'}`} onClick={() => poner({ formacion: p.formacion.filter((_, j) => j !== i) })}><IconoCerrar /></button>
                    </li>
                  )
                })}
              </ol>
              <button type="button" className="mp-agregar" onClick={() => agregar('estudios')}><IconoMas />Agregar estudio</button>
            </>
          )}

          {seccion === 'cursos' && (
            <>
              <CabezaParte titulo="Cursos y habilidades" texto="Escribe y pulsa Enter para agregar cada uno." />
              <Etiquetas etiqueta="Cursos y certificaciones" ejemplo="Manipulación de alimentos" valores={p.cursos} ia={deIa} onCambiar={(v) => poner({ cursos: v })} />
              <Etiquetas etiqueta="Habilidades" ejemplo="Manejo de montacargas" valores={p.habilidades} ia={deIa} onCambiar={(v) => poner({ habilidades: v })} />
            </>
          )}
        </div>

        <div className={`mp-guardar ${sucio || ocupado ? 'visible' : ''}`} aria-hidden={!(sucio || ocupado)}>
          <span className="mp-guardar-texto">
            {deIa ? <><Chispa className="mp-guardar-ia" />Revisa lo que leyó la IA y guarda</> : 'Tienes cambios sin guardar'}
          </span>
          {error && <span role="alert" className="mp-guardar-error"><IconoAlerta />{error.message}</span>}
          <Boton onClick={descartar} disabled={ocupado}>{nuevaHv ? 'Descartar la hoja de vida nueva' : 'Descartar'}</Boton>
          <Boton type="submit" variante="primario" icono={<IconoCheck />} cargando={ocupado} textoCargando="Guardando…" disabled={leyendo}>Guardar</Boton>
        </div>
      </form>

      {guardado && <div className="mp-aviso-flotante" role="status"><IconoCheck />Guardamos tu hoja de vida.</div>}
    </div>
  )
}

function CabezaParte({ titulo, texto }: { titulo: string; texto: string }) {
  return <header className="mp-parte-cabeza"><h3>{titulo}</h3><p>{texto}</p></header>
}

function Vacio({ texto }: { texto: string }) {
  return <p className="mp-parte-vacia">{texto}</p>
}

/** Lista de etiquetas: se escribe y Enter (o coma) agrega; Retroceso en vacío quita la última. */
function Etiquetas({ etiqueta, ejemplo, valores, ia, onCambiar }: { etiqueta: string; ejemplo: string; valores: string[]; ia: boolean; onCambiar: (v: string[]) => void }) {
  const [texto, setTexto] = useState('')
  const agregar = () => {
    const t = texto.trim().replace(/,$/, '')
    if (t && !valores.some((v) => v.toLowerCase() === t.toLowerCase())) onCambiar([...valores, t])
    setTexto('')
  }
  const tecla = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter' || e.key === ',') { e.preventDefault(); agregar() }
    else if (e.key === 'Backspace' && !texto && valores.length) onCambiar(valores.slice(0, -1))
  }
  return (
    <label className="postular-campo">{etiqueta}
      <span className={`mp-etiquetas${ia ? ' entrada-ia' : ''}`}>
        {valores.map((v, i) => (
          <span key={v} className="mp-etiqueta" style={{ '--i': i } as CSSProperties}>
            {v}
            <button type="button" aria-label={`Quitar ${v}`} onClick={(e) => { e.preventDefault(); onCambiar(valores.filter((x) => x !== v)) }}><IconoCerrar /></button>
          </span>
        ))}
        <input value={texto} placeholder={valores.length ? 'Agregar otra…' : `Por ejemplo: ${ejemplo}`} onChange={(e) => setTexto(e.target.value)} onKeyDown={tecla} onBlur={agregar} />
      </span>
    </label>
  )
}

/**
 * Subir la hoja de vida o el PDF de LinkedIn. Compacta (ya hay una): todo en una fila y se suelta el archivo sobre ella.
 * Sin hoja de vida: la cabecera y debajo la zona para soltar.
 */
function ZonaSubir({ onSubida, compacta, cabeza, acciones }: { onSubida: (id: string) => void; compacta: boolean; cabeza: ReactNode; acciones: ReactNode }) {
  const subir = useSubirHojaDeVidaPerfil()
  const archivo = useRef<HTMLInputElement>(null)
  const [modo, setModo] = useState<'hv' | 'linkedin'>('hv')
  const [arrastrando, setArrastrando] = useState(false)
  const enviar = (f?: File) => f && subir.mutate(f, { onSuccess: (r) => onSubida(r.id) })
  const soltar = {
    onDragOver: (e: DragEvent) => { e.preventDefault(); setArrastrando(true) },
    onDragLeave: () => setArrastrando(false),
    onDrop: (e: DragEvent) => { e.preventDefault(); setArrastrando(false); enviar(e.dataTransfer.files[0]) },
  }
  const selector = (
    <div className="segmentado mp-subir-modo" role="group" aria-label="Qué quieres subir">
      <button type="button" aria-pressed={modo === 'hv'} onClick={() => setModo('hv')}>Hoja de vida</button>
      <button type="button" aria-pressed={modo === 'linkedin'} onClick={() => setModo('linkedin')}>LinkedIn</button>
    </div>
  )
  const entrada = <input ref={archivo} type="file" accept={tiposArchivo} hidden onChange={(e) => enviar(e.target.files?.[0])} />

  return (
    <div className="mp-subir">
      {compacta ? (
        <div className={`mp-hv-archivo mp-hv-compacta ${arrastrando ? 'arrastrando' : ''}`} {...soltar}>
          {entrada}
          {cabeza}
          {selector}
          <Boton pequeno icono={<IconoSubir />} cargando={subir.isPending} textoCargando="Subiendo…" onClick={() => archivo.current?.click()}>
            {modo === 'linkedin' ? 'Subir PDF de LinkedIn' : 'Subir otra'}
          </Boton>
          {acciones}
        </div>
      ) : (
        <>
          <div className="mp-hv-archivo">{cabeza}{selector}</div>
          <div className={`mp-zona ${arrastrando ? 'arrastrando' : ''}`} {...soltar}>
            {entrada}
            <span className="mp-zona-icono"><IconoSubir /></span>
            <span className="mp-zona-texto">
              <b>{modo === 'linkedin' ? 'Suelta aquí el PDF de tu LinkedIn' : 'Suelta aquí tu hoja de vida'}</b>
              <span>PDF, Word o foto · <Chispa className="mp-zona-chispa" /> la IA la lee y llena tu perfil · se guarda cifrada</span>
            </span>
            <Boton variante="primario" pequeno cargando={subir.isPending} textoCargando="Subiendo…" onClick={() => archivo.current?.click()}>Elegir archivo</Boton>
          </div>
        </>
      )}
      {modo === 'linkedin' && <span className="mp-nota">En linkedin.com abre tu perfil, toca <b>Más</b> y luego <b>Guardar en PDF</b>. LinkedIn no deja leer el perfil solo con el enlace.</span>}
      {subir.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{subir.error.message}</p>}
    </div>
  )
}
