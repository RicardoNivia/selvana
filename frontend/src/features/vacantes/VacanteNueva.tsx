import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { AutocompletarMunicipio, resolverMunicipio } from '../../shared/ui/AutocompletarMunicipio'
import { Boton } from '../../shared/ui/Boton'
import { Chispa, IconoAlerta, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { useSesion } from '../sesion/api'
import { useEmpresaUsuariaActual } from '../sesion/empresaUsuaria'
import { ElegirCargo } from './CargoDelCatalogo'
import { type CargoCatalogo, type ContenidoVacante, type Vacante, useCrearVacante, useEditarVacante, useEmpresaActual, useRedactarVacante, useSolicitarVacante, useVacante } from './api'

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
  const [vacanteId, setVacanteId] = useState<string>()
  const editar = useEditarVacante(vacanteId ?? '')
  const vacante = useVacante(vacanteId ?? '', Boolean(vacanteId)).data

  const [frase, setFrase] = useState(inicio.frase ?? '')
  const [cargo, setCargo] = useState<CargoCatalogo | null>(inicio.cargo ?? null)
  const [usuaria, setUsuaria] = useState(inicio.empresaUsuariaId ?? actual.id)
  const [f, setF] = useState<Campos>({ ...vacios, cargo: inicio.cargo?.nombre ?? '' })
  const [deIa, setDeIa] = useState<Partial<Record<Campo, boolean>>>({})
  const [pidiendo, setPidiendo] = useState(false)
  const [llenadoCon, setLlenadoCon] = useState<string>()
  const [falta, setFalta] = useState<string>()
  const visibles = new Set(sesion?.empresasUsuarias.map((e) => e.id))
  const usuarias = empresa.data?.empresasUsuarias.filter((e) => e.activa && visibles.has(e.id)) ?? []

  const poner = (k: Campo, v: string) => { setF((x) => ({ ...x, [k]: v })); setDeIa((d) => ({ ...d, [k]: false })) }

  const generar = () => {
    if (!frase.trim()) return setFalta('Describa en una frase lo que necesita para que la IA llene el formulario.')
    setFalta(undefined)
    setPidiendo(true)
    const listo = { onError: () => setPidiendo(false) }
    if (vacanteId) redactar.mutate({ id: vacanteId, frase }, listo)
    else solicitar.mutate({ frase, empresaUsuariaId: usuaria || null, cargoId: cargo?.id ?? null }, { ...listo, onSuccess: (r) => setVacanteId(r.id) })
  }

  // Desde la barra de la lista con «Redactar con IA»: se pide apenas abre la pantalla
  const yaPidio = useRef(false)
  useEffect(() => {
    if (inicio.generar && !yaPidio.current) { yaPidio.current = true; generar() }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Cuando llega la respuesta de la IA: o pregunta, o llena el formulario en violeta
  useEffect(() => {
    if (!vacante || vacante.estado === 'Redactando' || !pidiendo) return
    setPidiendo(false)
    if ((vacante.aclaraciones?.length ?? 0) > 0 || vacante.mensajeIa) return
    const clave = `${vacante.frase}|${vacante.titulo}`
    if (llenadoCon === clave) return
    setLlenadoCon(clave)
    llenar(vacante)
  }, [vacante, pidiendo, llenadoCon])

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

  const contenido = (): ContenidoVacante => ({
    titulo: f.titulo, cargo: cargo?.nombre ?? f.cargo, ciudad: f.ciudad, codigoMunicipio: f.codigoMunicipio, cupos: Number(f.cupos) || 0, jornada: f.jornada,
    descripcion: f.descripcion, salarioMinimo: Number(f.salarioMinimo) || 0, salarioMaximo: Number(f.salarioMaximo) || 0,
    requisitos: lineas(f.requisitos), preguntasFiltro: lineas(f.preguntas),
  })

  const guardar = async () => {
    const c = contenido()
    if (!c.titulo.trim() || !c.cargo.trim() || !c.descripcion.trim()) return setFalta('Escriba el título, el cargo y la descripción.')
    if (!c.codigoMunicipio) return setFalta('Elija la ciudad o el municipio de la lista.')
    if (c.cupos < 1) return setFalta('Diga cuántas posiciones hay disponibles para este cargo.')
    if (c.salarioMinimo <= 0) return setFalta('Escriba el salario mínimo del rango.')
    setFalta(undefined)
    try {
      if (vacanteId) {
        await editar.mutateAsync(c)
        navegar(`/consola/vacantes/${vacanteId}`)
      } else {
        const r = await crear.mutateAsync({ ...c, empresaUsuariaId: usuaria || null, cargoId: cargo?.id ?? null })
        navegar(`/consola/vacantes/${r.id}`)
      }
    } catch {
      // El error se muestra en el formulario
    }
  }

  const redactando = pidiendo || vacante?.estado === 'Redactando'
  const aclaraciones = !redactando ? vacante?.aclaraciones ?? [] : []
  const guardando = crear.isPending || editar.isPending
  const hayIa = Object.values(deIa).some(Boolean)
  const error = falta ?? (solicitar.error ?? redactar.error ?? crear.error ?? editar.error)?.message
  const clase = (k: Campo, base = 'entrada') => `${base}${deIa[k] ? ' entrada-ia' : ''}`

  return (
    <>
      <header className="encabezado">
        <p style={{ margin: '0 0 8px', fontSize: 13 }}><Link to="/consola/vacantes">Vacantes</Link> · Nueva vacante</p>
        <h1>Nueva vacante</h1>
        <p>Una vacante es de un solo cargo, con las posiciones disponibles que necesite. Lo que proponga la IA se ve en violeta hasta que usted lo guarde.</p>
      </header>

      <form className="tarjeta formulario" style={{ padding: 24, display: 'flex', flexDirection: 'column', gap: 20 }} onSubmit={(e) => { e.preventDefault(); void guardar() }}>
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
            {aclaraciones.length > 0 && (
              <div className="aviso aviso-alerta" role="status" style={{ flexDirection: 'column', gap: 6 }}>
                <b style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}><IconoAlerta />Antes de redactar, la IA necesita que aclare:</b>
                <ul style={{ margin: 0, paddingLeft: 20 }}>{aclaraciones.map((a) => <li key={a}>{a}</li>)}</ul>
                <span>Ajuste la frase con la respuesta y vuelva a generar. Cada vacante es de un solo cargo; para otro cargo, cree otra vacante.</span>
              </div>
            )}
            {vacante?.mensajeIa && !redactando && <p className="aviso aviso-alerta"><IconoAlerta />{vacante.mensajeIa} Puede volver a intentarlo o llenar la vacante a mano.</p>}
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, alignItems: 'center' }}>
              <Boton variante="ia" icono={<Chispa />} cargando={redactando} textoCargando="La IA está redactando…" disabled={guardando} onClick={generar}>
                {aclaraciones.length > 0 ? 'Responder y generar' : llenadoCon ? 'Pedir otro borrador' : 'Generar con IA'}
              </Boton>
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
          <Boton type="submit" variante="primario" icono={<IconoCheck />} cargando={guardando} textoCargando="Guardando…" disabled={redactando}>
            {hayIa ? 'Confirmar y guardar borrador' : 'Guardar borrador'}
          </Boton>
          <Boton onClick={() => navegar('/consola/vacantes')} disabled={guardando}>Volver a vacantes</Boton>
          <span className="registro">Se registran la sugerencia y tu decisión. Después se envía a aprobación.</span>
        </div>
      </form>
    </>
  )
}
