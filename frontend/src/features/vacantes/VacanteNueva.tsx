import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { AutocompletarMunicipio, resolverMunicipio } from '../../shared/ui/AutocompletarMunicipio'
import { Boton } from '../../shared/ui/Boton'
import { Chispa, IconoAlerta, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { useSesion } from '../sesion/api'
import { useEmpresaUsuariaActual } from '../sesion/empresaUsuaria'
import { ElegirCargo } from './CargoDelCatalogo'
import { type Aclaracion, type CargoCatalogo, type ContenidoVacante, type Vacante, useCrearVacante, useEditarVacante, useEnviarAAprobacion, useEmpresaActual, useRedactarVacante, useSolicitarVacante, useVacante } from './api'

/** Lo que llega desde la barra de la lista: la frase, el cargo y la empresa usuaria; con generar, se pide a la IA al abrir. */
export interface InicioVacante {
  frase?: string
  cargo?: CargoCatalogo | null
  empresaUsuariaId?: string
  generar?: boolean
}

const lineas = (t: string) => t.split('\n').map((l) => l.trim()).filter(Boolean)
type Campos = { titulo: string; cargo: string; ciudad: string; codigoMunicipio: string | null; cupos: string; jornada: string; descripcion: string; salarioMinimo: string; salarioMaximo: string; requisitos: string; preguntas: string }
type Campo = keyof Campos
const vacios: Campos = { titulo: '', cargo: '', ciudad: '', codigoMunicipio: null, cupos: '1', jornada: '', descripcion: '', salarioMinimo: '', salarioMaximo: '', requisitos: '', preguntas: '' }
const todos: Campo[] = ['titulo', 'cargo', 'ciudad', 'cupos', 'jornada', 'descripcion', 'salarioMinimo', 'salarioMaximo', 'requisitos', 'preguntas']

/**
 * Crear una vacante en una sola pantalla. La persona describe lo que necesita y la IA llena este mismo formulario en violeta
 * (o pregunta antes si la frase pide más de un cargo o le falta algo); o la persona lo llena a mano. Una vacante es de un
 * solo cargo con varias posiciones disponibles. La ciudad se elige de la DIVIPOLA del DANE.
 */
export function VacanteNueva() {
  const navegar = useNavigate()
  const inicio = (useLocation().state ?? {}) as InicioVacante
  const empresa = useEmpresaActual()
  const { data: sesion } = useSesion()
  const actual = useEmpresaUsuariaActual()
  const solicitar = useSolicitarVacante()
  const redactar = useRedactarVacante()
  const crear = useCrearVacante()
  const enviarAAprobacion = useEnviarAAprobacion()
  const [vacanteId, setVacanteId] = useState<string>()
  const editar = useEditarVacante(vacanteId ?? '')
  const consulta = useVacante(vacanteId ?? '', Boolean(vacanteId))
  const vacante = consulta.data

  const [frase, setFrase] = useState(inicio.frase ?? '')
  const [cargo, setCargo] = useState<CargoCatalogo | null>(inicio.cargo ?? null)
  const [usuaria, setUsuaria] = useState(inicio.empresaUsuariaId ?? actual.id)
  const [f, setF] = useState<Campos>({ ...vacios, cargo: inicio.cargo?.nombre ?? '' })
  const [deIa, setDeIa] = useState<Partial<Record<Campo, boolean>>>({})
  // Momento en que la IA aceptó el pedido: solo cuenta una respuesta leída después de ese momento (no la vacante vieja)
  const [pedidoEn, setPedidoEn] = useState<number>()
  const [llenado, setLlenado] = useState(false)
  const [preguntas, setPreguntas] = useState<Aclaracion[] | null>(null)
  const [modalAbierto, setModalAbierto] = useState(false)
  const [falta, setFalta] = useState<string>()
  const [enviando, setEnviando] = useState(false)
  const visibles = new Set(sesion?.empresasUsuarias.map((e) => e.id))
  const usuarias = empresa.data?.empresasUsuarias.filter((e) => e.activa && visibles.has(e.id)) ?? []

  const poner = (k: Campo, v: string) => { setF((x) => ({ ...x, [k]: v })); setDeIa((d) => ({ ...d, [k]: false })) }

  const generar = (texto = frase) => {
    if (!texto.trim()) return setFalta('Describa en una frase lo que necesita para que la IA llene el formulario.')
    setFalta(undefined)
    setPreguntas(null)
    const aceptado = () => setPedidoEn(Date.now())
    if (vacanteId) redactar.mutate({ id: vacanteId, frase: texto }, { onSuccess: aceptado })
    else solicitar.mutate({ frase: texto, empresaUsuariaId: usuaria || null, cargoId: cargo?.id ?? null }, { onSuccess: (r) => { setVacanteId(r.id); aceptado() } })
  }

  // Desde la barra de la lista con «Redactar con IA»: se pide apenas abre la pantalla
  const yaPidio = useRef(false)
  useEffect(() => {
    if (inicio.generar && !yaPidio.current) { yaPidio.current = true; generar() }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // La respuesta de la IA: leída después del pedido y ya fuera de «redactando». O pregunta (modal), o llena el formulario
  const respuesta = vacante && pedidoEn !== undefined && consulta.dataUpdatedAt > pedidoEn && vacante.estado !== 'Redactando' ? vacante : undefined
  useEffect(() => {
    if (!respuesta) return
    setPedidoEn(undefined)
    if ((respuesta.aclaraciones?.length ?? 0) > 0) {
      setPreguntas(respuesta.aclaraciones)
      setModalAbierto(true)
      return
    }
    if (respuesta.mensajeIa || !respuesta.titulo) return
    llenar(respuesta)
    setLlenado(true)
  }, [respuesta])

  const llenar = (v: Vacante) => {
    setF({
      titulo: v.titulo, cargo: v.cargo, ciudad: v.ciudad, codigoMunicipio: v.codigoMunicipio, cupos: String(v.cupos || 1), jornada: v.jornada,
      descripcion: v.descripcion, salarioMinimo: String(v.salarioMinimo || ''), salarioMaximo: String(v.salarioMaximo || ''),
      requisitos: v.requisitos.join('\n'), preguntas: v.preguntasFiltro.join('\n'),
    })
    setDeIa(Object.fromEntries(todos.map((k) => [k, true])))
    // La IA escribe la ciudad como texto: si corresponde a un solo municipio del DANE, queda elegido con su código
    if (!v.codigoMunicipio && v.ciudad) void resolverMunicipio(v.ciudad).then((m) => m && setF((x) => ({ ...x, ciudad: m.municipio, codigoMunicipio: m.codigo })))
  }

  // Con las respuestas del modal, la frase queda ajustada (la persona la ve) y se vuelve a pedir el borrador
  const responder = (respuestas: string[]) => {
    const ajustada = `${frase.trim()}\n${(preguntas ?? []).map((p, i) => `${p.pregunta} ${respuestas[i]}`).join('\n')}`
    setFrase(ajustada)
    setModalAbierto(false)
    generar(ajustada)
  }

  const contenido = (): ContenidoVacante => ({
    titulo: f.titulo, cargo: cargo?.nombre ?? f.cargo, ciudad: f.ciudad, codigoMunicipio: f.codigoMunicipio, cupos: Number(f.cupos) || 0, jornada: f.jornada,
    descripcion: f.descripcion, salarioMinimo: Number(f.salarioMinimo) || 0, salarioMaximo: Number(f.salarioMaximo) || 0,
    requisitos: lineas(f.requisitos), preguntasFiltro: lineas(f.preguntas),
  })

  // Guardar deja el borrador; con «enviar», además lo manda a aprobación. Después se sigue en el detalle de la vacante
  const guardar = async (enviar: boolean) => {
    const c = contenido()
    if (!c.titulo.trim() || !c.cargo.trim() || !c.descripcion.trim()) return setFalta('Escriba el título, el cargo y la descripción.')
    if (!c.codigoMunicipio) return setFalta('Elija la ciudad o el municipio de la lista.')
    if (c.cupos < 1) return setFalta('Diga cuántas posiciones hay disponibles para este cargo.')
    if (c.salarioMinimo <= 0) return setFalta('Escriba el salario mínimo del rango.')
    setFalta(undefined)
    setEnviando(enviar)
    try {
      let id = vacanteId
      if (id) await editar.mutateAsync(c)
      else id = (await crear.mutateAsync({ ...c, empresaUsuariaId: usuaria || null, cargoId: cargo?.id ?? null })).id
      if (enviar) await enviarAAprobacion.mutateAsync(id)
      navegar(`/consola/vacantes/${id}`)
    } catch {
      // El error se muestra en el formulario
    }
  }

  const redactando = solicitar.isPending || redactar.isPending || pedidoEn !== undefined
  const guardando = crear.isPending || editar.isPending || enviarAAprobacion.isPending
  const hayIa = Object.values(deIa).some(Boolean)
  const error = falta ?? (solicitar.error ?? redactar.error ?? crear.error ?? editar.error ?? enviarAAprobacion.error)?.message
  const clase = (k: Campo, base = 'entrada') => `${base}${deIa[k] ? ' entrada-ia' : ''}`

  return (
    <>
      <header className="encabezado">
        <p style={{ margin: '0 0 8px', fontSize: 13 }}><Link to="/consola/vacantes">Vacantes</Link> · Nueva vacante</p>
        <h1>Nueva vacante</h1>
        <p>Una vacante es de un solo cargo, con las posiciones disponibles que necesite. Lo que proponga la IA se ve en violeta hasta que usted lo guarde.</p>
      </header>

      <form className="tarjeta formulario" style={{ padding: 24, display: 'flex', flexDirection: 'column', gap: 20 }} onSubmit={(e) => { e.preventDefault(); void guardar(false) }}>
        <div className="dos-campos">
          {empresa.data?.esTemporal && (
            <label>
              Empresa usuaria
              <select className="selector" value={usuaria} onChange={(e) => setUsuaria(e.target.value)} disabled={Boolean(vacanteId)}>
                <option value="">{empresa.data.nombre} (vacante propia)</option>
                {usuarias.map((u) => <option key={u.id} value={u.id}>{u.nombre} · {u.ciudad}</option>)}
              </select>
            </label>
          )}
          <div className="campo" style={{ gap: 6 }}>
            <span>Cargo del catálogo</span>
            {vacanteId && !cargo ? <span className="sub-celda">Sin cargo del catálogo</span> : <ElegirCargo elegido={cargo} onElegir={(c) => { if (!vacanteId) { setCargo(c); poner('cargo', c?.nombre ?? '') } }} />}
          </div>
        </div>

        <div className="bloque-ia">
          <div className="bloque-ia-cabeza"><Chispa />Llenar la vacante con IA</div>
          <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 12 }}>
            <label>
              ¿Qué necesita?
              <textarea className="area-texto" rows={2} value={frase} onChange={(e) => setFrase(e.target.value)} maxLength={1000}
                placeholder="Ej.: Necesito 25 auxiliares de bodega para Funza, turno noche, con curso de montacargas" />
            </label>
            {vacante?.mensajeIa && !redactando && <p className="aviso aviso-alerta"><IconoAlerta />{vacante.mensajeIa} Puede volver a intentarlo o llenar la vacante a mano.</p>}
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, alignItems: 'center' }}>
              {preguntas && !redactando ? (
                <Boton variante="ia" icono={<Chispa />} disabled={guardando} onClick={() => setModalAbierto(true)}>Responder a la IA</Boton>
              ) : (
                <Boton variante="ia" icono={<Chispa />} cargando={redactando} textoCargando="La IA está redactando…" disabled={guardando} onClick={() => generar()}>
                  {llenado ? 'Pedir otro borrador' : 'Generar con IA'}
                </Boton>
              )}
              <span className="ayuda">{cargo ? 'La IA parte del perfil del cargo y la frase lo ajusta (ciudad, posiciones, jornada).' : 'Sin cargo del catálogo, la IA redacta desde cero.'}</span>
            </div>
          </div>
        </div>

        {redactando && <div className="estado-agente" role="status"><span className="orbe trabajando" /><span>La IA está redactando la vacante. Los campos se llenan aquí apenas termine.</span></div>}

        <label>Título del aviso<input className={clase('titulo')} value={f.titulo} onChange={(e) => poner('titulo', e.target.value)} maxLength={200} /></label>
        <div className="dos-campos">
          <label>Cargo<input className={clase('cargo')} value={cargo?.nombre ?? f.cargo} onChange={(e) => poner('cargo', e.target.value)} disabled={Boolean(cargo)} maxLength={200} /></label>
          <label>
            Ciudad o municipio
            <AutocompletarMunicipio valor={{ codigo: f.codigoMunicipio, ciudad: f.ciudad }} ia={deIa.ciudad}
              onElegir={(m, texto) => { setF((x) => ({ ...x, ciudad: m?.municipio ?? texto, codigoMunicipio: m?.codigo ?? null })); setDeIa((d) => ({ ...d, ciudad: false })) }} />
          </label>
          <label>
            Posiciones disponibles
            <input className={clase('cupos')} type="number" min={1} value={f.cupos} onChange={(e) => poner('cupos', e.target.value)} />
            <span className="ayuda">Cuántas personas se contratan para este cargo</span>
          </label>
          <label>Jornada<input className={clase('jornada')} value={f.jornada} onChange={(e) => poner('jornada', e.target.value)} placeholder="Ej.: Turno noche, lunes a sábado" /></label>
          <label>Salario mínimo (COP)<input className={clase('salarioMinimo')} type="number" min={0} value={f.salarioMinimo} onChange={(e) => poner('salarioMinimo', e.target.value)} /></label>
          <label>Salario máximo (COP)<input className={clase('salarioMaximo')} type="number" min={0} value={f.salarioMaximo} onChange={(e) => poner('salarioMaximo', e.target.value)} /></label>
        </div>
        {vacante?.justificacionSalario && hayIa && <span className="ayuda" style={{ color: 'var(--color-ia)' }}>{vacante.justificacionSalario}</span>}
        <label>Descripción<textarea className={clase('descripcion', 'area-texto')} rows={5} value={f.descripcion} onChange={(e) => poner('descripcion', e.target.value)} /></label>
        <div className="dos-campos">
          <label>Requisitos <span className="ayuda">Uno por línea</span><textarea className={clase('requisitos', 'area-texto')} rows={6} value={f.requisitos} onChange={(e) => poner('requisitos', e.target.value)} /></label>
          <label>Preguntas filtro <span className="ayuda">Una por línea; el candidato responde sí o no</span><textarea className={clase('preguntas', 'area-texto')} rows={6} value={f.preguntas} onChange={(e) => poner('preguntas', e.target.value)} /></label>
        </div>

        {hayIa && vacante && vacante.razones.length > 0 && (
          <div className="bloque-ia">
            <div className="bloque-ia-cabeza" style={{ fontSize: 13 }}><Chispa />Cómo redactó la IA esta vacante{vacante.modeloIa && <span className="derecha">{vacante.modeloIa}</span>}</div>
            {vacante.razones.map((r) => (
              <div key={r.texto} className="razon">
                <span className={`signo ${r.signo === '+' ? 'mas' : 'menos'}`}>{r.signo}</span>
                <span className="texto">{r.texto}</span>
                <span className="cita">{r.fuente}</span>
              </div>
            ))}
            <div className="protegidas"><IconoEscudo />No se usaron para redactar: edad, género, estado civil, foto ni dirección exacta.</div>
          </div>
        )}

        {error && <span role="alert" className="error-formulario">{error}</span>}
        <div className="decision" style={{ padding: '16px 0 0' }}>
          <Boton variante="primario" icono={<IconoCheck />} cargando={guardando && enviando} textoCargando="Enviando…" disabled={redactando || guardando}
            onClick={() => void guardar(true)}>
            Guardar y enviar a aprobación
          </Boton>
          <Boton type="submit" cargando={guardando && !enviando} textoCargando="Guardando…" disabled={redactando || guardando}>Guardar borrador</Boton>
          <Boton onClick={() => navegar('/consola/vacantes')} disabled={guardando}>Volver a vacantes</Boton>
          <span className="registro">Se registran la sugerencia y tu decisión. Al aprobarse, la vacante se publica en el portal.</span>
        </div>
      </form>

      {modalAbierto && preguntas && (
        <ModalAclaraciones frase={frase} preguntas={preguntas} onCancelar={() => setModalAbierto(false)} onResponder={responder} />
      )}
    </>
  )
}

/**
 * Lo que la IA necesita saber antes de redactar: cada pregunta con sus opciones (un clic) y, si ninguna sirve, otra respuesta escrita.
 * Al confirmar, las respuestas se agregan a la frase y la IA vuelve a redactar.
 */
function ModalAclaraciones({ frase, preguntas, onCancelar, onResponder }: {
  frase: string
  preguntas: Aclaracion[]
  onCancelar: () => void
  onResponder: (respuestas: string[]) => void
}) {
  const [elegidas, setElegidas] = useState<string[]>(() => preguntas.map(() => ''))
  const [otras, setOtras] = useState<string[]>(() => preguntas.map(() => ''))
  const respuesta = (i: number) => (otras[i].trim() || elegidas[i]).trim()
  const completas = preguntas.every((_, i) => respuesta(i).length > 0)
  const cambiar = (lista: string[], i: number, v: string) => lista.map((x, k) => (k === i ? v : x))

  useEffect(() => {
    const cerrar = (e: KeyboardEvent) => e.key === 'Escape' && onCancelar()
    window.addEventListener('keydown', cerrar)
    return () => window.removeEventListener('keydown', cerrar)
  }, [onCancelar])

  return (
    <div className="capa-modal" role="presentation" onClick={(e) => e.target === e.currentTarget && onCancelar()}>
      <form className="dialogo tarjeta" role="dialog" aria-modal="true" aria-labelledby="aclarar-titulo"
        onSubmit={(e) => { e.preventDefault(); if (completas) onResponder(preguntas.map((_, i) => respuesta(i))) }}>
        <header className="dialogo-cabeza">
          <div>
            <h2 id="aclarar-titulo" style={{ display: 'inline-flex', gap: 8, alignItems: 'center' }}><Chispa />Antes de redactar</h2>
            <p>La IA necesita aclarar algo de «{frase.trim()}». Cada vacante es de un solo cargo.</p>
          </div>
          <button type="button" className="enlace-boton" onClick={onCancelar}>Cerrar</button>
        </header>
        {preguntas.map((p, i) => (
          <fieldset key={p.pregunta} className="aclaracion">
            <legend>{p.pregunta}</legend>
            {p.opciones.length > 0 && (
              <div className="aclaracion-opciones" role="group" aria-label={p.pregunta}>
                {p.opciones.map((o) => (
                  <button key={o} type="button" aria-pressed={!otras[i].trim() && elegidas[i] === o}
                    onClick={() => { setElegidas((x) => cambiar(x, i, o)); setOtras((x) => cambiar(x, i, '')) }}>
                    {o}
                  </button>
                ))}
              </div>
            )}
            <input className="entrada" value={otras[i]} onChange={(e) => setOtras((x) => cambiar(x, i, e.target.value))}
              placeholder={p.opciones.length > 0 ? 'Otra respuesta (opcional)' : 'Escriba la respuesta'} aria-label={`Otra respuesta: ${p.pregunta}`} />
          </fieldset>
        ))}
        <div className="decision" style={{ padding: '8px 0 0' }}>
          <Boton type="submit" variante="ia" icono={<Chispa />} disabled={!completas}>Generar con estas respuestas</Boton>
          <Boton onClick={onCancelar}>Cancelar</Boton>
          <span className="registro">Sus respuestas se agregan a la frase</span>
        </div>
      </form>
    </div>
  )
}
