import { Fragment, useCallback, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { AutocompletarMunicipio } from '../../shared/ui/AutocompletarMunicipio'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto, EsqueletoPanel } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { Buscador, Paginacion } from '../../shared/ui/Paginacion'
import { usePaginacion } from '../../shared/ui/paginado'
import { PruebasDeLaVacante } from '../pruebas/PruebasDeLaVacante'
import { PreseleccionPostulante } from '../preseleccion/PreseleccionPostulante'
import { AbrirHojaDeVida } from './AbrirHojaDeVida'
import { ExplicacionAfinidad, PuntajeAfinidad } from './AfinidadPostulante'
import { AprobacionRequisicion } from './AprobacionRequisicion'
import { CargoDeLaVacante } from './CargoDelCatalogo'
import { DelBanco } from './DelBanco'
import { ComparativoFinalistas } from './ComparativoFinalistas'
import { FiltrosAts } from './FiltrosAts'
import { ReporteSesgo } from './ReporteSesgo'
import { TableroVacante } from './TableroVacante'
import { estadoVacante, type Vacante, useCalcularAfinidad, useCerrarVacante, useEditarVacante, usePostulantes, useSolicitarAprobacion, useVacante } from './api'

const pesos = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 })
const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })

const pestanas = ['vacante', 'postulantes', 'banco', 'filtros', 'tablero', 'finalistas', 'equidad', 'pruebas'] as const
type Pestana = (typeof pestanas)[number]

export function VacanteDetalle() {
  const { id = '' } = useParams()
  const vacante = useVacante(id)
  // La pestaña se puede abrir desde un enlace (?pestana=tablero), por ejemplo desde la bandeja
  const [parametros] = useSearchParams()
  const [pestana, setPestana] = useState<Pestana>(() => (pestanas as readonly string[]).includes(parametros.get('pestana') ?? '') ? parametros.get('pestana') as Pestana : 'vacante')

  if (vacante.isPending) {
    return (
      <>
        <div className="encabezado"><Esqueleto alto={14} ancho={120} /><Esqueleto alto={34} ancho="50%" style={{ marginTop: 10 }} /></div>
        <div className="tarjeta" style={{ padding: 0 }}><EsqueletoPanel /></div>
      </>
    )
  }
  if (vacante.isError) return <p role="alert" className="aviso aviso-error">No se encontró la vacante.</p>

  const v = vacante.data
  const e = estadoVacante[v.estado]
  return (
    <>
      <header className="encabezado" style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <Link to="/consola/vacantes" style={{ fontSize: 13 }}>← Vacantes</Link>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, justifyContent: 'space-between', alignItems: 'flex-end' }}>
          <div>
            <h1>{v.titulo || 'Vacante en redacción'}</h1>
            <p>{v.empresa} · <span className="mono">{v.codigo}</span> · creada por {v.creadaPor} el {fecha.format(new Date(v.creadaEn))}</p>
          </div>
          <span style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <span className={`pill ${e.clase}`}>{e.texto}</span>
            {v.estado === 'Publicada' && <a className="boton boton-secundario boton-pequeno" href={`/empleos/${v.codigo}`} target="_blank" rel="noreferrer">Ver en el portal</a>}
          </span>
        </div>
      </header>

      <div role="tablist" aria-label="Vacante" className="pestanas">
        <button type="button" role="tab" className="pestana" aria-selected={pestana === 'vacante'} onClick={() => setPestana('vacante')}>Vacante</button>
        <button type="button" role="tab" className="pestana" aria-selected={pestana === 'postulantes'} onClick={() => setPestana('postulantes')}>Postulantes · {v.postulantes}</button>
        {(v.estado === 'Publicada' || v.estado === 'Cerrada') && <button type="button" role="tab" className="pestana" aria-selected={pestana === 'banco'} onClick={() => setPestana('banco')}>Del banco</button>}
        {v.estado !== 'Redactando' && <button type="button" role="tab" className="pestana" aria-selected={pestana === 'filtros'} onClick={() => setPestana('filtros')}>Filtros ATS</button>}
        {v.postulantes > 0 && <button type="button" role="tab" className="pestana" aria-selected={pestana === 'tablero'} onClick={() => setPestana('tablero')}>Tablero</button>}
        {v.postulantes > 0 && <button type="button" role="tab" className="pestana" aria-selected={pestana === 'finalistas'} onClick={() => setPestana('finalistas')}>Finalistas</button>}
        {v.postulantes > 0 && <button type="button" role="tab" className="pestana" aria-selected={pestana === 'equidad'} onClick={() => setPestana('equidad')}>Equidad</button>}
        {v.estado !== 'Redactando' && <button type="button" role="tab" className="pestana" aria-selected={pestana === 'pruebas'} onClick={() => setPestana('pruebas')}>Pruebas · {v.pruebas.length}</button>}
      </div>

      {pestana === 'vacante' ? (v.estado === 'Redactando' ? <Redactando frase={v.frase} /> : <Contenido key={`${v.id}-${v.estado}`} vacante={v} />)
        : pestana === 'pruebas' ? <PruebasDeLaVacante vacante={v} /> : pestana === 'tablero' ? <TableroVacante vacante={v} /> : pestana === 'equidad' ? <ReporteSesgo vacanteId={v.id} /> : pestana === 'finalistas' ? <ComparativoFinalistas vacanteId={v.id} /> : pestana === 'filtros' ? <FiltrosAts vacanteId={v.id} /> : pestana === 'banco' ? <DelBanco vacante={v} /> : <Postulantes vacante={v} />}
    </>
  )
}

function Redactando({ frase }: { frase: string }) {
  return (
    <section className="tarjeta" style={{ padding: 20, display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div className="estado-agente" role="status" style={{ border: 0, padding: 0 }}>
        <span className="orbe trabajando" />
        <div><b>El agente redactor está preparando la vacante</b><div style={{ fontSize: 13, color: 'var(--color-texto-suave)' }}>«{frase}»</div></div>
      </div>
      {[90, 60, 120, 80].map((a, i) => <Esqueleto key={i} alto={a} />)}
    </section>
  )
}

const lineas = (t: string) => t.split('\n').map((l) => l.trim()).filter(Boolean)

function Contenido({ vacante: v }: { vacante: Vacante }) {
  const editar = useEditarVacante(v.id)
  const solicitar = useSolicitarAprobacion(v.id)
  const cerrar = useCerrarVacante(v.id)
  const deIa = !v.editadaPorPersona && v.modeloIa !== null
  const bloqueada = v.estado === 'Cerrada' || v.estado === 'EnAprobacion'
  const [municipio, setMunicipio] = useState(v.codigoMunicipio)
  const [ciudadTocada, setCiudadTocada] = useState(false)
  const [f, setF] = useState({
    titulo: v.titulo, cargo: v.cargo, ciudad: v.ciudad, cupos: String(v.cupos || ''), jornada: v.jornada, descripcion: v.descripcion,
    salarioMinimo: String(v.salarioMinimo || ''), salarioMaximo: String(v.salarioMaximo || ''), requisitos: v.requisitos.join('\n'), preguntas: v.preguntasFiltro.join('\n'),
  })
  const [tocado, setTocado] = useState(false)
  const campo = (k: keyof typeof f) => ({
    value: f[k],
    disabled: bloqueada,
    onChange: (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => { setF((x) => ({ ...x, [k]: e.target.value })); setTocado(true) },
    className: `${k === 'descripcion' || k === 'requisitos' || k === 'preguntas' ? 'area-texto' : 'entrada'}${deIa && !tocado ? ' entrada-ia' : ''}`,
  })
  const contenido = () => ({
    titulo: f.titulo, cargo: f.cargo, ciudad: f.ciudad, cupos: Number(f.cupos) || 0, jornada: f.jornada, descripcion: f.descripcion,
    salarioMinimo: Number(f.salarioMinimo) || 0, salarioMaximo: Number(f.salarioMaximo) || 0, requisitos: lineas(f.requisitos), preguntasFiltro: lineas(f.preguntas),
    codigoMunicipio: municipio,
  })
  const guardarYEnviar = () => (tocado ? editar.mutate(contenido(), { onSuccess: () => solicitar.mutate() }) : solicitar.mutate())
  const error = editar.error ?? solicitar.error ?? cerrar.error

  return (
    <div className="dos-columnas">
      <form className="tarjeta detalle-cargo panel-decision formulario" onSubmit={(e) => { e.preventDefault(); editar.mutate(contenido(), { onSuccess: () => setTocado(false) }) }}>
        {deIa && !tocado && <span className="ayuda" style={{ color: 'var(--color-ia)', fontSize: 12 }}>Borrador de la IA ({v.modeloIa}). Al editar un campo queda confirmado por usted.</span>}
        {v.estado === 'Borrador' && v.aprobacion.motivoDevolucion && (
          <p className="aviso aviso-alerta"><IconoAlerta />La devolvieron: «{v.aprobacion.motivoDevolucion}». Ajuste la vacante y vuelva a enviarla.</p>
        )}
        {v.estado === 'EnAprobacion' && <p className="aviso aviso-neutro"><IconoEscudo />La requisición está en aprobación: para cambiarla, quien aprueba debe devolverla.</p>}
        {v.mensajeIa && <p className="aviso aviso-alerta"><IconoAlerta />{v.mensajeIa} Complete la vacante a mano.</p>}
        {(v.aclaraciones?.length ?? 0) > 0 && (
          <div className="aviso aviso-alerta" style={{ flexDirection: 'column', gap: 6 }}>
            <b>La IA no redactó esta vacante porque necesita que aclare:</b>
            <ul style={{ margin: 0, paddingLeft: 20 }}>{v.aclaraciones?.map((a) => <li key={a.pregunta}>{a.pregunta}</li>)}</ul>
            <span>Llénela a mano aquí o cree otra con una frase de un solo cargo.</span>
          </div>
        )}
        <label>Título del aviso<input {...campo('titulo')} required /></label>
        <div className="dos-campos">
          <label>Cargo<input {...campo('cargo')} /></label>
          <label>
            Ciudad o municipio
            <AutocompletarMunicipio valor={{ codigo: municipio, ciudad: f.ciudad }} ia={deIa && !tocado && !ciudadTocada} disabled={bloqueada} requerido
              onElegir={(m, texto) => { setF((x) => ({ ...x, ciudad: m?.municipio ?? texto })); setMunicipio(m?.codigo ?? null); setCiudadTocada(true); setTocado(true) }} />
          </label>
          <label>Posiciones disponibles<input {...campo('cupos')} type="number" min={1} /></label>
          <label>Jornada<input {...campo('jornada')} /></label>
          <label>Salario mínimo (COP)<input {...campo('salarioMinimo')} type="number" min={0} /></label>
          <label>Salario máximo (COP)<input {...campo('salarioMaximo')} type="number" min={0} /></label>
        </div>
        {v.justificacionSalario && <span className="ayuda" style={{ color: deIa ? 'var(--color-ia)' : undefined }}>{v.justificacionSalario}</span>}
        <label>Descripción<textarea {...campo('descripcion')} rows={5} /></label>
        <div className="dos-campos">
          <label>Requisitos <span className="ayuda">Uno por línea</span><textarea {...campo('requisitos')} rows={6} /></label>
          <label>Preguntas filtro <span className="ayuda">Una por línea; el candidato responde sí o no</span><textarea {...campo('preguntas')} rows={6} /></label>
        </div>
        {error && <span role="alert" className="error-formulario">{error.message}</span>}
        <div className="decision" style={{ padding: '8px 0 0', borderTop: 0 }}>
          {v.estado === 'Borrador' && (
            <Boton variante="primario" icono={<IconoCheck />} cargando={solicitar.isPending || (editar.isPending && !tocado)} textoCargando="Enviando…" onClick={guardarYEnviar}>
              Enviar a aprobación
            </Boton>
          )}
          {!bloqueada && <Boton type="submit" disabled={!tocado} cargando={editar.isPending} textoCargando="Guardando…">Guardar cambios</Boton>}
          {v.estado === 'Publicada' && <Boton cargando={cerrar.isPending} textoCargando="Cerrando…" onClick={() => cerrar.mutate()}>Cerrar vacante</Boton>}
          <span className="registro">Se registran la sugerencia y tu decisión</span>
        </div>
      </form>

      <aside style={{ flex: '1 1 300px', minWidth: 0, display: 'flex', flexDirection: 'column', gap: 16 }}>
        <AprobacionRequisicion vacante={v} />
        <CargoDeLaVacante vacante={v} />
        {v.razones.length > 0 && (
          <div className="bloque-ia">
            <div className="bloque-ia-cabeza" style={{ fontSize: 13 }}><Chispa />Por qué la IA la redactó así</div>
            {v.razones.map((r) => (
              <div key={r.texto} className="razon">
                <span className={`signo ${r.signo === '+' ? 'mas' : 'menos'}`}>{r.signo}</span>
                <span className="texto">{r.texto}</span>
                <span className="cita">{r.fuente}</span>
              </div>
            ))}
            <div className="protegidas"><IconoEscudo />Nunca pide edad, género, estado civil, apariencia, foto ni dirección.</div>
          </div>
        )}
        {v.supuestos.length > 0 && (
          <div className="tarjeta" style={{ padding: 16 }}>
            <b style={{ fontSize: 14 }}>Supuestos de la IA</b>
            <ul style={{ margin: '8px 0 0', paddingLeft: 18, fontSize: 13, color: 'var(--color-texto-suave)' }}>{v.supuestos.map((s) => <li key={s}>{s}</li>)}</ul>
          </div>
        )}
        <div className="tarjeta" style={{ padding: 16, fontSize: 13, color: 'var(--color-texto-suave)' }}>
          <b style={{ color: 'var(--color-texto)' }}>Frase original</b>
          <p style={{ margin: '6px 0 0' }}>«{v.frase}»</p>
          {v.salarioMinimo > 0 && <p className="mono" style={{ margin: '8px 0 0' }}>{pesos.format(v.salarioMinimo)} – {pesos.format(v.salarioMaximo)}</p>}
        </div>
      </aside>
    </div>
  )
}

const canales: [string, string][] = [['', 'Todos'], ['Portal', 'Portal'], ['LinkedIn', 'LinkedIn'], ['Referido', 'Referidos']]

function Postulantes({ vacante }: { vacante: Vacante }) {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const [buscar, setBuscar] = useState('')
  const [canal, setCanal] = useState('')
  const datos = usePostulantes(vacante.id, { pagina, tamano, buscar, canal })
  const alBuscar = useCallback((t: string) => { setBuscar(t); setPagina(1) }, [setPagina])
  const calcular = useCalcularAfinidad(vacante.id)
  const [abierta, setAbierta] = useState<string>()
  const calculando = datos.data?.items.filter((p) => p.afinidad?.estado === 'Calculando').length ?? 0

  return (
    <>
      <section className="rejilla" aria-label="Postulantes por canal">
        <div className="cifra"><span>Total</span><b>{vacante.postulantes}</b><small>Cupos: {vacante.cupos}</small></div>
        {vacante.canales.map((c) => <div key={c.nombre} className="cifra"><span>{c.nombre}</span><b>{c.total}</b><small>{Math.round((c.total / Math.max(1, vacante.postulantes)) * 100)} % del total</small></div>)}
      </section>
      <section className="tarjeta" aria-label="Postulantes">
        <div className="barra-filtros">
          <Buscador etiqueta="Buscar por nombre o cédula" onBuscar={alBuscar} />
          <div className="segmentado" role="group" aria-label="Canal">
            {canales.map(([v, t]) => <button key={v} type="button" aria-pressed={canal === v} onClick={() => { setCanal(v); setPagina(1) }}>{t}</button>)}
          </div>
          {vacante.postulantes > 0 && (
            <span className="ranking-acciones">
              {calculando > 0
                ? <span className="afinidad-calculando" role="status"><span className="orbe trabajando" />La IA califica {calculando} {calculando === 1 ? 'postulación' : 'postulaciones'}…</span>
                : calcular.data && calcular.data.pedidas === 0 && <span className="ayuda">Todas tienen puntaje.</span>}
              <Boton variante="ia" pequeno icono={<Chispa />} cargando={calcular.isPending} textoCargando="Pidiendo…" onClick={() => calcular.mutate(false)}>Calificar con IA</Boton>
            </span>
          )}
        </div>
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead><tr><th>Afinidad</th><th>Postulante</th><th>Canal</th><th>Preguntas filtro</th><th>Ley 1581</th><th>Llegó</th><th>Hoja de vida</th></tr></thead>
            <tbody>
              {datos.isPending
                ? Array.from({ length: 5 }, (_, i) => <tr key={i}>{[20, 50, 20, 30, 25, 25, 30].map((a, j) => <td key={j}><Esqueleto ancho={`${a}%`} /></td>)}</tr>)
                : datos.data?.items.map((p) => (
                    <Fragment key={p.postulacionId}>
                    <tr>
                      <td><PuntajeAfinidad afinidad={p.afinidad} abierto={abierta === p.postulacionId} onAlternar={() => setAbierta((x) => (x === p.postulacionId ? undefined : p.postulacionId))} /></td>
                      <td>
                        <Link to={`/consola/candidatos/${p.candidatoId}`} style={{ fontWeight: 600 }}>{p.nombre}</Link>
                        <button type="button" className="enlace-boton fila-detalle" aria-expanded={abierta === p.postulacionId}
                          onClick={() => setAbierta((x) => (x === p.postulacionId ? undefined : p.postulacionId))}>{abierta === p.postulacionId ? 'Ocultar detalle' : 'Detalle y preselección'}</button>
                        <span className="mono" style={{ display: 'block', fontSize: 12, color: 'var(--color-texto-tenue)' }}>
                          {p.documento} · {p.ciudad}{p.otrasPostulaciones > 0 ? ` · ${p.otrasPostulaciones} postulación(es) más` : ''}
                        </span>
                        {p.linkedIn && <a href={p.linkedIn} target="_blank" rel="noopener noreferrer" style={{ display: 'block', fontSize: 12 }}>Perfil de LinkedIn ↗</a>}
                        {p.camposCorregidos.length > 0 && <span style={{ fontSize: 12, color: 'var(--color-texto-tenue)' }}>Corrigió lo leído por la IA: {p.camposCorregidos.join(', ')}</span>}
                      </td>
                      <td><span className="pill pill-neutra">{p.canal}</span></td>
                      <td className="mono" style={{ color: p.respuestasNegativas > 0 ? 'var(--color-alerta)' : 'var(--color-exito)' }}>{p.respuestas || '—'}</td>
                      <td style={{ fontSize: 13, color: 'var(--color-texto-suave)' }}>{p.autorizacion}</td>
                      <td className="mono" style={{ fontSize: 13 }}>{fecha.format(new Date(p.recibidaEn))}</td>
                      <td><AbrirHojaDeVida postulacionId={p.postulacionId} /></td>
                    </tr>
                    {abierta === p.postulacionId && (
                      <tr className="fila-explicacion"><td colSpan={7}>
                        <div className="fila-explicacion-cuerpo">
                          {p.afinidad?.estado === 'Lista' && <ExplicacionAfinidad postulante={p} afinidad={p.afinidad} />}
                          <PreseleccionPostulante postulacionId={p.postulacionId} />
                        </div>
                      </td></tr>
                    )}
                    </Fragment>
                  ))}
            </tbody>
          </table>
        </div>
        {datos.data && datos.data.total === 0 && (
          <div className="vacio" style={{ padding: 32 }}>
            <p>{vacante.estado === 'Publicada' ? 'Aún no llegan postulantes. Comparta el enlace del portal.' : 'Publique la vacante para recibir postulantes.'}</p>
            {vacante.estado === 'Publicada' && <span className="mono">{`${window.location.origin}/empleos/${vacante.codigo}`}</span>}
          </div>
        )}
        {datos.data && <Paginacion pagina={datos.data} cargando={datos.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
      </section>
    </>
  )
}
