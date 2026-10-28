import { useRef, useState } from 'react'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoAlerta, IconoDocumento, IconoSubir } from '../../shared/ui/Iconos'
import { usePuede } from '../sesion/api'
import {
  abrirPlantilla, type TipoContrato, type TipoContratoEmpresa, type TipoContratoRequest, tiposContrato, useCargarTiposPorDefecto, useCrearTipoContrato,
  useEditarTipoContrato, useQuitarPlantillaContrato, useSubirPlantillaContrato, useTiposContrato,
} from './api'
import './vinculacion.css'

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' })
const tiposPlantilla = '.pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document'

/**
 * Maestro de tipos de contrato: nombre, modalidad legal y cómo se trabaja cada contrato en la empresa.
 * La IA sigue la descripción y la plantilla al redactar la oferta y el contrato en Vinculación.
 */
export function TiposContrato() {
  const tipos = useTiposContrato()
  const configura = usePuede('ConfigurarEstructura')
  const cargar = useCargarTiposPorDefecto()
  const [creando, setCreando] = useState(false)

  return (
    <>
      <header className="encabezado vin-tipos-encabezado">
        <div>
          <h1>Tipos de contrato</h1>
          <p>Cada tipo dice cómo se trabaja ese contrato en su empresa. La IA lo sigue al redactar la oferta y el contrato; si sube una plantilla, el contrato sigue su estructura.</p>
        </div>
        {configura && !creando && <Boton variante="primario" onClick={() => setCreando(true)}>Nuevo tipo de contrato</Boton>}
      </header>

      {creando && <EditorTipo onListo={() => setCreando(false)} />}

      {tipos.isPending ? (
        <div className="vin-tipos">{[1, 2, 3].map((i) => <Esqueleto key={i} alto={210} />)}</div>
      ) : tipos.isError ? (
        <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{tipos.error.message}</p>
      ) : tipos.data.length === 0 ? (
        <div className="tarjeta vin-tipos-vacio">
          <p>Su empresa aún no tiene tipos de contrato. Cargue los de siempre (indefinido, término fijo, obra o labor y aprendizaje) y ajústelos a su forma de trabajar.</p>
          {cargar.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{cargar.error.message}</p>}
          {configura && <Boton cargando={cargar.isPending} textoCargando="Cargando…" onClick={() => cargar.mutate()}>Cargar los tipos por defecto</Boton>}
        </div>
      ) : (
        <div className="vin-tipos">
          {tipos.data.map((t) => <TarjetaTipo key={t.id} tipo={t} editable={configura} />)}
        </div>
      )}
    </>
  )
}

function TarjetaTipo({ tipo, editable }: { tipo: TipoContratoEmpresa; editable: boolean }) {
  const [editando, setEditando] = useState(false)
  if (editando) return <EditorTipo tipo={tipo} onListo={() => setEditando(false)} />

  return (
    <article className={`tarjeta vin-tipo ${tipo.activo ? '' : 'inactivo'}`}>
      <header>
        <div>
          <h2>{tipo.nombre}</h2>
          <span className="vin-tipo-meta">{tipo.modalidadNombre}{tipo.porDefecto ? ' · por defecto' : ''}</span>
        </div>
        <span className={`pill ${tipo.activo ? 'pill-ok' : 'pill-neutra'}`}>{tipo.activo ? 'Activo' : 'Inactivo'}</span>
      </header>
      <p className="vin-tipo-descripcion">{tipo.descripcion}</p>
      <Plantilla tipo={tipo} editable={editable} />
      <footer>
        <span className="vin-tipo-meta">Actualizado por {tipo.actualizadoPor} · <span className="mono">{fecha.format(new Date(tipo.actualizadoEn))}</span></span>
        {editable && <Boton pequeno onClick={() => setEditando(true)}>Editar</Boton>}
      </footer>
    </article>
  )
}

/** Plantilla o contrato de ejemplo del tipo: la IA sigue su estructura al redactar. */
function Plantilla({ tipo, editable }: { tipo: TipoContratoEmpresa; editable: boolean }) {
  const subir = useSubirPlantillaContrato()
  const quitar = useQuitarPlantillaContrato()
  const archivo = useRef<HTMLInputElement>(null)
  const [abriendo, setAbriendo] = useState(false)
  const [errorAbrir, setErrorAbrir] = useState<string>()
  const error = subir.error?.message ?? quitar.error?.message ?? errorAbrir

  const ver = async () => {
    setAbriendo(true)
    setErrorAbrir(undefined)
    try {
      await abrirPlantilla(tipo.id)
    } catch (e) {
      setErrorAbrir(e instanceof Error ? e.message : 'No pudimos abrir la plantilla.')
    } finally {
      setAbriendo(false)
    }
  }

  return (
    <div className="vin-plantilla">
      <input ref={archivo} type="file" accept={tiposPlantilla} hidden
        onChange={(e) => { const f = e.target.files?.[0]; if (f) subir.mutate({ id: tipo.id, archivo: f }); e.target.value = '' }} />
      <span className="vin-plantilla-icono"><IconoDocumento /></span>
      {tipo.plantillaNombre ? (
        <>
          <span className="vin-plantilla-texto"><b>{tipo.plantillaNombre}</b><span>La IA sigue esta plantilla al redactar el contrato.</span></span>
          <Boton pequeno cargando={abriendo} textoCargando="Abriendo…" onClick={ver}>Ver</Boton>
          {editable && <Boton pequeno icono={<IconoSubir />} cargando={subir.isPending} textoCargando="Subiendo…" onClick={() => archivo.current?.click()}>Reemplazar</Boton>}
          {editable && <Boton pequeno cargando={quitar.isPending} textoCargando="Quitando…" onClick={() => quitar.mutate(tipo.id)}>Quitar</Boton>}
        </>
      ) : (
        <>
          <span className="vin-plantilla-texto"><b>Sin plantilla</b><span>Suba un contrato de ejemplo (PDF o Word) y la IA redactará con su estructura.</span></span>
          {editable && <Boton pequeno icono={<IconoSubir />} cargando={subir.isPending} textoCargando="Subiendo…" onClick={() => archivo.current?.click()}>Subir plantilla</Boton>}
        </>
      )}
      {error && <p role="alert" className="aviso aviso-alerta vin-plantilla-error"><IconoAlerta />{error}</p>}
    </div>
  )
}

function EditorTipo({ tipo, onListo }: { tipo?: TipoContratoEmpresa; onListo: () => void }) {
  const crear = useCrearTipoContrato()
  const editar = useEditarTipoContrato()
  const [f, setF] = useState<TipoContratoRequest>({
    nombre: tipo?.nombre ?? '', modalidad: tipo?.modalidad ?? 'Indefinido', descripcion: tipo?.descripcion ?? '', activo: tipo?.activo ?? true,
  })
  const poner = (c: Partial<TipoContratoRequest>) => setF((x) => ({ ...x, ...c }))
  const mutacion = tipo ? editar : crear
  const guardar = () => (tipo ? editar.mutate({ ...f, id: tipo.id }, { onSuccess: onListo }) : crear.mutate(f, { onSuccess: onListo }))

  return (
    <form className="tarjeta vin-tipo vin-tipo-editor formulario" onSubmit={(e) => { e.preventDefault(); guardar() }}>
      <h2>{tipo ? `Editar «${tipo.nombre}»` : 'Nuevo tipo de contrato'}</h2>
      <div className="vin-tipo-campos">
        <label>Nombre<input className="entrada" value={f.nombre} maxLength={80} onChange={(e) => poner({ nombre: e.target.value })} placeholder="Término fijo a 6 meses" required /></label>
        <label>Modalidad legal
          <select className="selector" value={f.modalidad} onChange={(e) => poner({ modalidad: e.target.value as TipoContrato })}>
            {tiposContrato.map(([v, t]) => <option key={v} value={v}>{t}</option>)}
          </select>
          <span className="ayuda">Activa las reglas de ley que revisa Selvana (fechas, periodo de prueba, salario mínimo).</span>
        </label>
      </div>
      <label>Cómo se trabaja este contrato
        <textarea className="area-texto" rows={6} maxLength={2000} value={f.descripcion} onChange={(e) => poner({ descripcion: e.target.value })} required
          placeholder="Qué fechas se especifican, cómo termina, preaviso, periodo de prueba y qué debe decir el contrato." />
        <span className="ayuda">La IA sigue este texto al redactar. <span className="mono">{f.descripcion.length}/2000</span></span>
      </label>
      <label className="vin-tipo-activo"><input type="checkbox" checked={f.activo} onChange={(e) => poner({ activo: e.target.checked })} />Activo: aparece al contratar</label>
      {mutacion.error && <p role="alert" className="aviso aviso-alerta"><IconoAlerta />{mutacion.error.message}</p>}
      <div className="vin-tipo-acciones">
        <Boton onClick={onListo} disabled={mutacion.isPending}>Cancelar</Boton>
        <Boton type="submit" variante="primario" cargando={mutacion.isPending} textoCargando="Guardando…">{tipo ? 'Guardar' : 'Crear'}</Boton>
      </div>
      {tipo && <span className="ayuda">Los procesos ya abiertos conservan la versión anterior; el cambio aplica a las contrataciones nuevas.</span>}
    </form>
  )
}
