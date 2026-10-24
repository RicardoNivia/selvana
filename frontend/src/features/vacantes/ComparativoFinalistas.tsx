import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { type Finalista, useComparativoFinalistas } from './api'

const pesos = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 })
const etapas: Record<string, string> = { Recibida: 'Recibida', Preseleccion: 'Preselección', Pruebas: 'Pruebas', Terna: 'Terna', Descartada: 'Descartada' }
const estadosPresel: Record<string, string> = { Invitada: 'Invitado, sin responder', Abierta: 'Conversando', Completada: 'Completa', Escalada: 'Espera a una persona' }
const MAXIMO = 4

function Celda({ titulo, children }: { titulo: string; children: React.ReactNode }) {
  return <div className="comp-celda"><span className="comp-titulo-celda">{titulo}</span>{children}</div>
}

function Columna({ f }: { f: Finalista }) {
  const hv = f.hojaDeVida
  const p = f.preseleccion
  const anios = Math.floor(hv.mesesExperiencia / 12)
  return (
    <article className="comp-columna">
      <header className="comp-cabeza">
        <Link to={`/consola/candidatos/${f.candidatoId}`}>{f.nombre}</Link>
        <span>{f.ciudad || 'Sin ciudad'} · <span className="pill pill-neutra">{etapas[f.etapa] ?? f.etapa}</span></span>
        {f.puntaje !== null
          ? <b className={`comp-puntaje mono ${f.ajustado ? 'ajustado' : 'ia'}`} title={f.ajustado ? `Ajustado por una persona (la IA sugirió ${f.puntajeIa})` : 'Sugerencia de la IA'}>{f.puntaje}</b>
          : <span className="afinidad-vacia">Sin puntaje</span>}
      </header>

      <Celda titulo="A favor">
        {f.aFavor.length === 0 ? <span className="comp-vacio">—</span> : f.aFavor.map((r) => <p key={r.texto} className="comp-razon"><span className="signo mas">+</span><span>{r.texto} <span className="cita">{r.fuente}</span></span></p>)}
      </Celda>
      <Celda titulo="En contra">
        {f.enContra.length === 0 ? <span className="comp-vacio">Nada en contra</span> : f.enContra.map((r) => <p key={r.texto} className="comp-razon"><span className="signo menos">−</span><span>{r.texto} <span className="cita">{r.fuente}</span></span></p>)}
      </Celda>
      <Celda titulo="Requisitos">
        {f.cumplidos.map((x) => <span key={x} className="comp-req ok"><IconoCheck />{x}</span>)}
        {f.faltantes.map((x) => <span key={x} className="comp-req falta"><IconoAlerta />No demuestra: {x}</span>)}
        {f.cumplidos.length + f.faltantes.length === 0 && <span className="comp-vacio">—</span>}
      </Celda>

      <Celda titulo="Hoja de vida">
        <p><b>{hv.ultimoCargo || 'Sin cargo'}</b> · {anios > 0 ? `${anios} ${anios === 1 ? 'año' : 'años'}` : 'menos de 1 año'} con fechas</p>
        {hv.formacion && <p className="comp-sub">{hv.formacion}</p>}
        {hv.habilidades.length > 0 && <div className="hv-chips">{hv.habilidades.map((h) => <span key={h} className="hv-chip">{h}</span>)}</div>}
        {hv.alertas.length > 0 && <span className="comp-req falta"><IconoAlerta />{hv.alertas.length} {hv.alertas.length === 1 ? 'alerta' : 'alertas'} de la HV</span>}
        {hv.posibleDuplicado && <span className="comp-req falta"><IconoAlerta />Posible duplicado (mismo celular)</span>}
      </Celda>

      <Celda titulo="Preselección">
        {!p ? <span className="comp-vacio">Sin preselección: invítelo desde Postulantes</span> : (
          <>
            <span className="pill pill-neutra">{estadosPresel[p.estado] ?? p.estado}</span>
            <p>Interés: <b>{p.interesado === null ? '—' : p.interesado ? 'Sí' : 'No'}</b></p>
            <p>Aspiración: <b className="mono">{p.aspiracionSalarialCop ? pesos.format(p.aspiracionSalarialCop) : '—'}</b>
              {p.aspiracionEnRango !== null && <span className={`pill ${p.aspiracionEnRango ? 'pill-ok' : 'pill-alerta'}`} style={{ marginLeft: 6 }}>{p.aspiracionEnRango ? 'En rango' : 'Sobre el rango'}</span>}</p>
            {p.disponibilidad && <p className="comp-sub">{p.disponibilidad}</p>}
            {p.ubicacion && <p className="comp-sub">{p.ubicacion}</p>}
            {p.dudas > 0 && <p className="comp-sub">{p.dudas} {p.dudas === 1 ? 'duda' : 'dudas'} del candidato</p>}
          </>
        )}
      </Celda>

      <Celda titulo="Pruebas">
        {!f.pruebas ? <span className="comp-vacio">Sin pruebas enviadas</span> : (
          <>
            {f.pruebas.total !== null && <p>Total: <b className="mono">{f.pruebas.total}</b>{f.pruebas.provisional && <span className="comp-sub"> · provisional</span>}</p>}
            {f.pruebas.pruebas.map((x) => (
              <p key={x.nombre} className="comp-prueba">
                <span>{x.nombre}{!x.validada && <span className="comp-sub"> · en validación</span>}</span>
                {x.sensible ? <span className="comp-sub">Dato sensible · se ve con su rol</span>
                  : x.puntaje === null ? <span className="comp-sub">{x.estado}</span>
                  : <b className={`mono ${x.pasaCorte === false ? 'comp-bajo' : ''}`}>{x.puntaje}{x.corte !== null && <span className="comp-sub"> / corte {x.corte}</span>}</b>}
              </p>
            ))}
          </>
        )}
      </Celda>

      <Celda titulo="Su equipo">
        {f.retroalimentacion ? <p><b>{f.retroalimentacion === 'Bueno' ? 'Buen candidato' : 'No encaja'}</b>{f.notaRetroalimentacion && <span className="comp-sub"> · «{f.notaRetroalimentacion}»</span>}</p> : <span className="comp-vacio">Sin opinión registrada</span>}
      </Celda>
    </article>
  )
}

/** Informe comparativo: los finalistas lado a lado para decidir la terna. La IA no decide. */
export function ComparativoFinalistas({ vacanteId }: { vacanteId: string }) {
  const [elegidos, setElegidos] = useState<string[] | null>(null)
  const datos = useComparativoFinalistas(vacanteId, elegidos)

  if (datos.isPending) return <div className="comp-grilla">{Array.from({ length: 3 }, (_, i) => <div key={i} className="comp-columna"><Esqueleto alto={22} ancho="70%" /><Esqueleto alto={120} /><Esqueleto alto={90} /></div>)}</div>
  if (datos.isError) return <p role="alert" className="aviso aviso-error"><IconoAlerta />No pudimos armar el comparativo. Intente de nuevo.</p>

  const d = datos.data
  const actuales = d.finalistas.map((f) => f.postulacionId)
  const alternar = (id: string) => {
    const base = elegidos ?? actuales
    const siguiente = base.includes(id) ? base.filter((x) => x !== id) : [...base, id].slice(-MAXIMO)
    setElegidos(siguiente.length ? siguiente : null)
  }

  return (
    <section className="comparativo" aria-label="Informe comparativo de finalistas">
      <header className="comp-encabezado">
        <div>
          <h2>Finalistas · {d.vacante}</h2>
          <p><span className="mono">{d.codigo}</span> · {d.cupos} {d.cupos === 1 ? 'cupo' : 'cupos'} · rango {pesos.format(d.salarioMinimo)} – {pesos.format(d.salarioMaximo)}</p>
        </div>
        <button type="button" className="boton boton-secundario boton-pequeno comp-imprimir" onClick={() => window.print()}>Imprimir o guardar en PDF</button>
      </header>

      <div className="comp-selector" role="group" aria-label="Candidatos a comparar">
        <span>Comparar (hasta {MAXIMO}):</span>
        {d.disponibles.map((o) => (
          <button key={o.postulacionId} type="button" className="chip" aria-pressed={actuales.includes(o.postulacionId)} onClick={() => alternar(o.postulacionId)}>
            {o.nombre}{o.puntaje !== null && <span className="mono"> · {o.puntaje}</span>}
          </button>
        ))}
      </div>

      {d.finalistas.length === 0 ? <div className="vacio"><p>Elija al menos un candidato para comparar.</p></div> : (
        <div className="comp-grilla" style={{ gridTemplateColumns: `repeat(${d.finalistas.length}, minmax(240px, 1fr))` }}>
          {d.finalistas.map((f) => <Columna key={f.postulacionId} f={f} />)}
        </div>
      )}

      <p className="ficha-nota"><Chispa />Los puntajes en violeta son sugerencias de la IA; en verde, ajustados por su equipo. Ninguno usó edad, género, estado civil, foto ni dirección.</p>
      <p className="ficha-nota"><IconoEscudo />La IA no decide: compare y apruebe la terna en la bandeja. Los resultados sensibles de las pruebas no salen en este informe.</p>
    </section>
  )
}
