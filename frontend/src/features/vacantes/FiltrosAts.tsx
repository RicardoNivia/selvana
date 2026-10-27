import { useState } from 'react'
import { Link } from 'react-router-dom'
import { AutocompletarMunicipio } from '../../shared/ui/AutocompletarMunicipio'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoAlerta, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { Paginacion } from '../../shared/ui/Paginacion'
import { usePaginacion } from '../../shared/ui/paginado'
import { usePuede } from '../sesion/api'
import {
  type Filtros, type GuardarFiltros, type ResultadoFiltros, resultados, useAprobarDescartes, useEvaluacionFiltros, useFiltros, useGuardarFiltros,
  useProponerDescartes, useRescatar,
} from './filtrosApi'
import './filtros.css'

const orden: ResultadoFiltros[] = ['NoCumple', 'PorRevisar', 'Cumple', 'Rescatada', 'Descartada']

/**
 * Filtros ATS de la vacante: qué es excluyente y cómo queda cada postulante, con su evidencia.
 * Los filtros no descartan solos: proponen, y una persona aprueba los descartes o rescata a alguien.
 */
export function FiltrosAts({ vacanteId }: { vacanteId: string }) {
  const filtros = useFiltros(vacanteId)
  if (filtros.isPending) return <div className="ats"><Esqueleto alto={260} /><Esqueleto alto={320} /></div>
  if (filtros.isError) return <p role="alert" className="aviso aviso-error"><IconoAlerta />{filtros.error.message}</p>
  return (
    <div className="ats">
      <Configuracion key={filtros.data.configuradoEn ?? 'sugeridos'} vacanteId={vacanteId} f={filtros.data} />
      {filtros.data.configurados && <Evaluacion vacanteId={vacanteId} f={filtros.data} />}
    </div>
  )
}

function Configuracion({ vacanteId, f }: { vacanteId: string; f: Filtros }) {
  const puede = usePuede('CrearVacantes')
  const guardar = useGuardarFiltros(vacanteId)
  const [v, setV] = useState<GuardarFiltros>(() => ({
    requisitos: f.requisitos.filter((x) => x.excluyente).map((x) => x.texto),
    preguntas: f.preguntas.filter((x) => x.excluyente).map((x) => x.texto),
    ciudades: f.ciudades,
    experienciaMinimaMeses: f.experienciaMinimaMeses,
  }))
  const alternar = (lista: 'requisitos' | 'preguntas', texto: string) =>
    setV((x) => ({ ...x, [lista]: x[lista].includes(texto) ? x[lista].filter((t) => t !== texto) : [...x[lista], texto] }))

  const opcion = (lista: 'requisitos' | 'preguntas', texto: string, sugerencia: string | null) => (
    <label key={texto} className="ats-opcion">
      <input type="checkbox" checked={v[lista].includes(texto)} disabled={!puede} onChange={() => alternar(lista, texto)} />
      <span>
        {texto}
        {!f.configurados && sugerencia && <small className="ats-sugerencia">Sugerido: {sugerencia}</small>}
      </span>
    </label>
  )

  return (
    <section className="tarjeta ats-panel" aria-label="Filtros excluyentes">
      <header>
        <div>
          <h2>Filtros excluyentes</h2>
          <p>{f.configurados
            ? `Configurados por ${f.configuradoPor}. Quien no pase uno queda como descarte sugerido; nadie se descarta sin que una persona lo apruebe.`
            : 'Selvana sugiere estos filtros por reglas desde los requisitos y las preguntas de la vacante. Revíselos y guárdelos para evaluar a los postulantes.'}</p>
        </div>
        {!f.configurados && <span className="pill pill-alerta">Sin guardar</span>}
      </header>

      <div className="ats-columnas">
        <fieldset>
          <legend>Requisitos que son excluyentes</legend>
          {f.requisitos.length === 0 ? <p className="ayuda">La vacante no tiene requisitos.</p>
            : f.requisitos.map((r) => opcion('requisitos', r.texto, r.sugerencia))}
        </fieldset>
        <fieldset>
          <legend>Preguntas en las que un «No» descarta</legend>
          {f.preguntas.length === 0 ? <p className="ayuda">La vacante no tiene preguntas filtro.</p>
            : f.preguntas.map((p) => opcion('preguntas', p.texto, p.sugerencia))}
        </fieldset>
        <fieldset>
          <legend>Dónde debe vivir</legend>
          <div className="hv-chips">
            {v.ciudades.map((c) => (
              <span key={c} className="hv-chip">{c}{puede && <button type="button" className="ats-quitar" aria-label={`Quitar ${c}`} onClick={() => setV((x) => ({ ...x, ciudades: x.ciudades.filter((y) => y !== c) }))}>×</button>}</span>
            ))}
            {v.ciudades.length === 0 && <span className="ayuda">Cualquier ciudad</span>}
          </div>
          {puede && (
            <div className="ats-ciudad">
              {/* Se elige de la DIVIPOLA del DANE y queda agregada al elegirla */}
              <AutocompletarMunicipio key={v.ciudades.length} texto="Agregar ciudad" valor={{ codigo: null, ciudad: '' }}
                onElegir={(m) => { if (m) setV((x) => ({ ...x, ciudades: [...new Set([...x.ciudades, m.municipio])] })) }} />
            </div>
          )}
          <legend className="ats-legend-2">Experiencia mínima con fechas</legend>
          <label className="ats-meses">
            <input className="entrada mono" type="number" min={0} max={480} disabled={!puede} value={v.experienciaMinimaMeses ?? ''}
              onChange={(e) => setV((x) => ({ ...x, experienciaMinimaMeses: e.target.value ? Math.max(0, Number(e.target.value)) : null }))} placeholder="0" />
            <span>meses</span>
          </label>
        </fieldset>
      </div>

      <p className="ficha-nota"><IconoEscudo />Un filtro nunca puede usar edad, género, estado civil, foto, barrio ni dirección exacta: Selvana no lo deja guardar.</p>
      {guardar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{guardar.error.message}</p>}
      {puede && (
        <div className="decision">
          <Boton variante="primario" cargando={guardar.isPending} textoCargando="Guardando…" onClick={() => guardar.mutate(v)}>
            {f.configurados ? 'Guardar filtros' : 'Guardar y evaluar postulantes'}
          </Boton>
          <span className="registro">Queda en la bitácora con su nombre</span>
        </div>
      )}
    </section>
  )
}

function Evaluacion({ vacanteId, f }: { vacanteId: string; f: Filtros }) {
  const [resultado, setResultado] = useState('')
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const lista = useEvaluacionFiltros(vacanteId, { resultado, pagina, tamano })
  const proponer = useProponerDescartes(vacanteId)
  const aprobar = useAprobarDescartes(vacanteId)
  const rescatar = useRescatar(vacanteId)
  const aprueba = usePuede('AprobarBandeja')
  const [hecho, setHecho] = useState<string | null>(null)
  const sugeridos = f.sugeridos

  return (
    <section className="tarjeta ats-panel" aria-label="Postulantes según los filtros">
      <header>
        <div>
          <h2>Postulantes según los filtros</h2>
          <p>Cada resultado muestra de dónde sale. «Sin evidencia» no es un descarte: la hoja de vida no dice ni sí ni no, y lo revisa una persona.</p>
        </div>
      </header>
      <div className="ats-resumen">
        <div className="segmentado" role="group" aria-label="Resultado">
          <button type="button" aria-pressed={resultado === ''} onClick={() => { setResultado(''); setPagina(1) }}>Todos</button>
          {orden.map((r) => (
            <button key={r} type="button" aria-pressed={resultado === r} onClick={() => { setResultado(r); setPagina(1) }}>
              {resultados[r].texto} <span className="mono">· {f.porResultado[r] ?? 0}</span>
            </button>
          ))}
        </div>
        {sugeridos > 0 && (
          <div className="ats-acciones">
            <Boton cargando={proponer.isPending} textoCargando="Enviando…" onClick={() => proponer.mutate(undefined, { onSuccess: (r) => setHecho(`${r.postulaciones} descartes sugeridos quedaron en la bandeja.`) })}>
              Enviar a la bandeja
            </Boton>
            {aprueba && (
              <Boton variante="primario" cargando={aprobar.isPending} textoCargando="Descartando…"
                onClick={() => aprobar.mutate(null, { onSuccess: (r) => setHecho(`${r.postulaciones} postulantes pasaron a Descartadas con el motivo del filtro.`) })}>
                Aprobar {sugeridos} {sugeridos === 1 ? 'descarte' : 'descartes'}
              </Boton>
            )}
          </div>
        )}
      </div>
      {hecho && <p className="aviso aviso-ok" role="status"><IconoCheck />{hecho} <Link to="/consola/bandeja">Ir a la bandeja</Link></p>}
      {(proponer.error ?? aprobar.error ?? rescatar.error) && <p role="alert" className="aviso aviso-error"><IconoAlerta />{(proponer.error ?? aprobar.error ?? rescatar.error)!.message}</p>}

      <div className="ats-filas">
        {lista.isPending && Array.from({ length: 4 }, (_, i) => <div key={i} className="ats-fila"><Esqueleto alto={16} ancho="40%" /><Esqueleto alto={40} /></div>)}
        {lista.data?.items.map((p) => (
          <article key={p.postulacionId} className={`ats-fila ${p.descarteSugerido ? 'no-cumple' : ''}`}>
            <header>
              <Link to={`/consola/candidatos/${p.candidatoId}`}>{p.nombre}</Link>
              <span className="ats-sub">{p.ciudad || 'Sin ciudad'} · <span className="mono">{p.numero}</span> · {p.etapa}</span>
              <span className={`pill ${resultados[p.resultado].clase}`}>{p.descarteSugerido ? 'Descarte sugerido' : resultados[p.resultado].texto}</span>
              {p.descarteSugerido && (
                <Boton pequeno cargando={rescatar.isPending && rescatar.variables === p.postulacionId} textoCargando="Rescatando…" onClick={() => rescatar.mutate(p.postulacionId)}>Rescatar</Boton>
              )}
            </header>
            {p.rescatadaPor && <span className="ats-sub">Rescatado por {p.rescatadaPor}: sigue en el proceso aunque no pasó un filtro.</span>}
            {p.resultado === 'NoCumple' && !p.descarteSugerido && <span className="ats-sub">Ya está en la terna: no se propone descartarlo, pero revise el filtro que no pasa.</span>}
            <ul className="ats-chequeos">
              {p.chequeos.map((c) => (
                <li key={`${c.tipo}-${c.filtro}`} className={c.estado.toLowerCase()}>
                  <span className="signo">{c.estado === 'Cumple' ? '+' : c.estado === 'NoCumple' ? '−' : '?'}</span>
                  <span><b>{c.filtro}</b> · {c.detalle}</span>
                  <span className="cita">{c.evidencia}</span>
                </li>
              ))}
            </ul>
          </article>
        ))}
        {lista.data && lista.data.total === 0 && <div className="vacio"><p>No hay postulantes con este resultado.</p></div>}
      </div>
      {lista.data && <Paginacion pagina={lista.data} cargando={lista.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
      <p className="ficha-nota"><IconoEscudo />Descartar afecta a una persona: cada descarte queda con el motivo del filtro, quién lo aprobó y cuándo.</p>
    </section>
  )
}
