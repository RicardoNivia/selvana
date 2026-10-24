import { type CSSProperties, type ReactNode, useRef } from 'react'
import {
  Chispa, IconoAlerta, IconoBuscar, IconoCheck, IconoEscudo, IconoHerramienta, IconoInsignia, IconoManos, IconoReloj, IconoVacantes,
} from '../../shared/ui/Iconos'
import { tramo, useEscenaEnVista } from '../../shared/ui/animaciones'

const v = (i: number) => ({ '--i': i }) as CSSProperties

/** «Así se ve Selvana trabajando»: cuatro escenas del producto real que se animan mientras están en pantalla. */
export function Vitrina() {
  return (
    <section id="en-accion" className="inicio-seccion vit">
      <div className="inicio-ancho">
        <h2 data-revelar>Así se ve Selvana trabajando</h2>
        <p className="vit-intro" data-revelar>Cuatro momentos de una selección real. Lo violeta lo propone la IA; lo verde ya lo decidió alguien de su equipo.</p>
        <Fila
          ceja="Banco de talento"
          titulo="Busque como lo diría en voz alta"
          texto="Todas las personas que se han postulado quedan en su banco de talento. Escriba lo que necesita y Selvana le muestra lo que entendió y a quién encontró."
          puntos={['Entiende ciudad, plurales y sinónimos: «pase C1» o «bodeguero»', 'Busca en cargos, cursos, habilidades y estudios', 'La cédula completa solo se ve al abrir la hoja de vida']}
        >
          <EscenaBanco />
        </Fila>
        <Fila
          ceja="Hoja de vida"
          titulo="Cada hoja de vida, ordenada en segundos"
          texto="La IA lee la hoja de vida, el candidato confirma sus datos y usted la ve por bloques: experiencia, formación y habilidades en conjuntos, con las blandas aparte."
          puntos={['Duración de cada empleo y el cargo normalizado', 'Alertas para revisar: fechas que se cruzan o huecos', 'Edad, género, estado civil y foto no se extraen']}
          invertida
        >
          <EscenaFicha />
        </Fila>
        <Fila
          ceja="Requisición"
          titulo="Del líder a la gerencia, sin correos"
          texto="El líder pide la vacante en una frase. RRHH y gerencia la aprueban desde su bandeja y la última aprobación la publica en el portal de empleo."
          puntos={['Cada etapa solo le llega a quien la aprueba', 'Devolver exige un motivo', 'Todo queda en la bitácora de auditoría']}
        >
          <EscenaRequisicion />
        </Fila>
        <Fila
          ceja="Agente de ranking"
          titulo="Un puntaje que se explica solo"
          texto="El agente compara un perfil anónimo con la vacante y le da un puntaje con 3 a 5 razones y la evidencia de la hoja de vida. Usted lo ajusta si no está de acuerdo."
          puntos={['Nunca recibe nombre, cédula ni contacto', 'Cada razón cita de dónde sale', 'Su ajuste y su motivo quedan registrados']}
          invertida
        >
          <EscenaRanking />
        </Fila>
      </div>
    </section>
  )
}

function Fila({ ceja, titulo, texto, puntos, invertida, children }: { ceja: string; titulo: string; texto: string; puntos: string[]; invertida?: boolean; children: ReactNode }) {
  return (
    <div className={`vit-fila ${invertida ? 'vit-invertida' : ''}`} data-revelar>
      <div className="vit-texto">
        <span className="mono vit-ceja">{ceja}</span>
        <h3>{titulo}</h3>
        <p>{texto}</p>
        <ul>{puntos.map((p) => <li key={p}><IconoCheck />{p}</li>)}</ul>
      </div>
      {children}
    </div>
  )
}

/* ---------- 1. Banco de talento ---------- */

const consulta = 'montacarguistas con licencia C1 en Mosquera'
const entendido = ['Ciudad: Mosquera', 'montacarguistas', 'licencia', 'c1']
const hallados = [
  { nombre: 'Kevin Mosquera P.', doc: 'CC ***.214', cargo: 'Operador de montacargas', exp: '4 años y 2 meses', chips: ['Operación de montacargas', 'Licencia C1'] },
  { nombre: 'Ana Lucía Moreno D.', doc: 'CC ***.973', cargo: 'Operario de montacargas', exp: '4 años y 4 meses', chips: ['Licencia C1', 'Trabajo en alturas'] },
  { nombre: 'Jorge Iván Peña Q.', doc: 'CC ***.608', cargo: 'Auxiliar de bodega', exp: '2 años', chips: ['Operación de montacargas', 'Licencia C1'] },
]

function EscenaBanco() {
  const ref = useRef<HTMLDivElement>(null)
  const t = useEscenaEnVista(ref, 130, 120)
  const letras = Math.max(0, Math.min(consulta.length, Math.floor((t - 6) * 1.6)))
  const escrita = letras >= consulta.length
  const buscando = escrita && t < 42
  const chips = t < 42 ? 0 : Math.min(entendido.length, Math.floor((t - 42) / 3) + 1)
  const filas = t < 54 ? 0 : Math.min(hallados.length, Math.floor((t - 54) / 7) + 1)

  return (
    <div className="vit-escena" ref={ref} aria-label="Ejemplo animado del banco de talento">
      <div className="vit-cabeza"><b>Candidatos</b><span className="mono vit-tenue">Banco de talento</span></div>
      <div className={`vit-buscador ${buscando ? 'vit-buscando' : ''}`}>
        <IconoBuscar />
        <span className="vit-consulta">{consulta.slice(0, letras)}<span className={`inicio-cursor vit-cursor ${escrita ? 'inicio-cursor-fin' : ''}`} /></span>
        <span className={`boton boton-primario boton-pequeno ${buscando ? 'inicio-pulsado' : ''}`}>Buscar</span>
      </div>
      <div className="vit-chips" aria-hidden={chips === 0}>
        {chips > 0 && <span className="vit-tenue">Buscó:</span>}
        {entendido.slice(0, chips).map((c) => <span key={c} className="pill pill-neutra inicio-aparece">{c}</span>)}
      </div>
      <div className="vit-filas">
        {hallados.slice(0, filas).map((h) => (
          <div key={h.nombre} className="vit-candidato inicio-aparece">
            <div><b>{h.nombre}</b><span className="mono vit-tenue">{h.doc} · Mosquera</span></div>
            <div className="vit-candidato-meta"><span>{h.cargo}</span><span className="vit-tenue">{h.exp}</span></div>
            <div className="vit-chips-mini">{h.chips.map((c) => <span key={c} className="pill pill-neutra">{c}</span>)}</div>
          </div>
        ))}
        {filas === hallados.length && <span className="mono vit-tenue vit-rango inicio-aparece">1–3 de 3</span>}
        {filas === 0 && Array.from({ length: 3 }, (_, i) => <div key={i} className="vit-hueco" style={v(i)} />)}
      </div>
    </div>
  )
}

/* ---------- 2. Hoja de vida por bloques ---------- */

const empleos = [
  { cargo: 'Operario de montacargas', empresa: 'Alimentos La Sabana', periodo: '2025 – hoy', dura: '1 año y 4 meses', actual: true },
  { cargo: 'Auxiliar de bodega', empresa: 'Logística del Occidente', periodo: '2022 – 2025', dura: '3 años', actual: false },
]
const conjuntos = [
  { titulo: 'Del oficio', icono: <IconoHerramienta />, chips: ['Operación de montacargas', 'Cargue y descargue'], blandas: false },
  { titulo: 'Licencias y cursos', icono: <IconoInsignia />, chips: ['Licencia C1', 'Trabajo en alturas'], blandas: false },
  { titulo: 'Habilidades blandas', icono: <IconoManos />, chips: ['Responsabilidad', 'Puntualidad', 'Trabajo en equipo'], blandas: true },
]

function EscenaFicha() {
  const ref = useRef<HTMLDivElement>(null)
  const t = useEscenaEnVista(ref, 140, 130)
  const meses = Math.round(tramo(t, 4, 24) * 52)
  const empleosVistos = t < 26 ? 0 : Math.min(empleos.length, Math.floor((t - 26) / 12) + 1)
  const conjuntosVistos = t < 56 ? 0 : Math.min(conjuntos.length, Math.floor((t - 56) / 12) + 1)
  const alerta = t >= 96

  return (
    <div className="vit-escena" ref={ref} aria-label="Ejemplo animado de la ficha del candidato">
      <div className="vit-cabeza">
        <span className="vit-avatar">AL</span>
        <div><b>Ana Lucía Moreno D.</b><span className="vit-tenue">Funza · actualizado hoy</span></div>
      </div>
      <div className="vit-cifras">
        <div><span>Experiencia</span><b>{Math.floor(meses / 12)} a {meses % 12} m</b></div>
        <div><span>Empleos</span><b>{Math.round(tramo(t, 4, 24) * 2)}</b></div>
        <div><span>Habilidades</span><b>{Math.round(tramo(t, 4, 24) * 7)}</b></div>
      </div>
      <div className="vit-bloque-titulo"><span className="vit-icono"><IconoVacantes /></span>Experiencia</div>
      <ol className="vit-linea">
        {empleos.slice(0, empleosVistos).map((e) => (
          <li key={e.cargo} className={`inicio-aparece ${e.actual ? 'actual' : ''}`}>
            <b>{e.cargo}</b>{e.actual && <span className="pill pill-ok">Actual</span>}
            <span className="vit-tenue">{e.empresa}</span>
            <span className="vit-meta"><span className="mono">{e.periodo}</span><span className="vit-duracion">{e.dura}</span></span>
          </li>
        ))}
      </ol>
      <div className="vit-bloque-titulo"><span className="vit-icono"><IconoHerramienta /></span>Habilidades por conjuntos</div>
      <div className="vit-conjuntos">
        {conjuntos.slice(0, conjuntosVistos).map((c) => (
          <div key={c.titulo} className={`vit-conjunto inicio-aparece ${c.blandas ? 'blandas' : ''}`}>
            <span className="vit-conjunto-titulo"><span className="vit-icono vit-icono-mini">{c.icono}</span>{c.titulo}</span>
            <span className="vit-chips-mini">{c.chips.map((x, i) => <span key={x} className="vit-chip" style={v(i)}>{x}</span>)}</span>
          </div>
        ))}
      </div>
      {alerta && <p className="vit-alerta inicio-aparece"><IconoAlerta />Para revisar: 14 meses sin experiencia registrada entre 2020 y 2022. <span className="cita">HV · experiencias 2 y 3</span></p>}
    </div>
  )
}

/* ---------- 3. Requisición líder → RRHH → gerencia ---------- */

function EscenaRequisicion() {
  const ref = useRef<HTMLDivElement>(null)
  const t = useEscenaEnVista(ref, 130, 125)
  const pidio = t >= 10
  const rrhh = t >= 32
  const gerencia = t >= 72
  const publicada = t >= 80
  const avance = gerencia ? 100 : rrhh ? 50 + tramo(t, 32, 72) * 20 : pidio ? tramo(t, 10, 32) * 50 : 0
  const postulaciones = Math.round(tramo(t, 86, 120) * 12)
  const etapas = [
    { titulo: 'Líder', hecho: pidio, texto: pidio ? 'Pidió la vacante · Mario R.' : 'Escribe la frase' },
    { titulo: 'RRHH', hecho: rrhh, pendiente: pidio && !rrhh, texto: rrhh ? 'Aprobó · Laura M.' : pidio ? 'Revisando la requisición' : 'Después del líder' },
    { titulo: 'Gerencia', hecho: gerencia, pendiente: rrhh && !gerencia, texto: gerencia ? 'Aprobó y publicó · Andrés R.' : rrhh ? 'Espera a gerencia' : 'Después de RRHH' },
  ]

  return (
    <div className="vit-escena" ref={ref} aria-label="Ejemplo animado del flujo de aprobación de una requisición">
      <div className="vit-cabeza"><b>Requisición VAC-261022-3F2A</b><span className="mono vit-tenue">Aprobación</span></div>
      <div className={`vit-vacante ${publicada ? 'vit-ok' : pidio ? '' : 'vit-ia'}`}>
        {publicada ? <span className="pill pill-ok inicio-aparece"><IconoCheck />Publicada en el portal</span>
          : pidio ? <span className="pill pill-alerta"><IconoReloj />En aprobación</span>
            : <span className="pill pill-ia"><Chispa />Borrador de la IA</span>}
        <b>Montacarguista · Funza</b>
        <span className="vit-tenue">5 cupos · turno rotativo · $1.750.905 – $2.100.000</span>
        {publicada && <span className="mono vit-postulaciones inicio-aparece">{postulaciones} postulaciones</span>}
      </div>
      <div className="vit-flujo">
        <span className="vit-flujo-riel" aria-hidden="true"><span style={{ width: `${avance}%` }} /></span>
        {etapas.map((e, i) => (
          <div key={e.titulo} className={`vit-etapa ${e.hecho ? 'hecho' : e.pendiente ? 'pendiente' : ''}`} style={v(i)}>
            <span className="vit-etapa-punto">{e.hecho ? <IconoCheck /> : e.pendiente ? <IconoReloj /> : null}</span>
            <b>{e.titulo}</b>
            <span>{e.texto}</span>
          </div>
        ))}
      </div>
      <span className="mono vit-tenue vit-registro">{gerencia ? 'Envío, aprobaciones y publicación registrados en la bitácora' : 'Se registran la sugerencia y cada decisión'}</span>
    </div>
  )
}

/* ---------- 4. Agente de ranking ---------- */

const razones = [
  { signo: '+', texto: '4 años operando montacargas en bodega', cita: 'HV · experiencia 1' },
  { signo: '+', texto: 'Licencia C1 y curso de alturas vigentes', cita: 'HV · cursos' },
  { signo: '−', texto: 'No confirmó disponibilidad para turno noche', cita: 'Preguntas filtro' },
]
const RADIO = 52
const CIRCUNFERENCIA = 2 * Math.PI * RADIO

function EscenaRanking() {
  const ref = useRef<HTMLDivElement>(null)
  const t = useEscenaEnVista(ref, 140, 130)
  const calculando = t < 28
  const puntaje = Math.round(tramo(t, 28, 52) * 87)
  const vistas = t < 56 ? 0 : Math.min(razones.length, Math.floor((t - 56) / 9) + 1)
  const listo = t >= 86

  return (
    <div className="vit-escena" ref={ref} aria-label="Ejemplo animado del agente de ranking">
      <div className="vit-cabeza"><b>Afinidad con la vacante</b><span className="mono vit-tenue">Montacarguista · Funza</span></div>
      <div className="vit-ranking">
        <div className="vit-anillo">
          <svg viewBox="0 0 120 120" aria-hidden="true">
            <circle cx="60" cy="60" r={RADIO} className="vit-anillo-fondo" />
            <circle cx="60" cy="60" r={RADIO} className="vit-anillo-valor" strokeDasharray={CIRCUNFERENCIA} strokeDashoffset={CIRCUNFERENCIA * (1 - puntaje / 100)} />
          </svg>
          <span className="vit-anillo-cifra">{calculando ? '—' : puntaje}<small>/100</small></span>
        </div>
        <div className="vit-ranking-estado">
          {calculando ? (
            <span className="pill pill-ia"><span className="orbe trabajando inicio-orbe" />Calculando afinidad</span>
          ) : <span className="pill pill-ia inicio-aparece"><Chispa />Sugerencia de la IA</span>}
          <span className="vit-anonimo">Perfil anónimo: sin nombre, cédula ni contacto</span>
          <span className="vit-tenue">Funza · 4 años en montacargas · licencia C1</span>
        </div>
      </div>
      <ul className="vit-razones">
        {razones.slice(0, vistas).map((r) => (
          <li key={r.texto} className="inicio-aparece">
            <span className={`signo ${r.signo === '+' ? 'mas' : 'menos'}`}>{r.signo}</span>
            <span>{r.texto}</span>
            <span className="cita">{r.cita}</span>
          </li>
        ))}
      </ul>
      {listo && (
        <div className="vit-cierre inicio-aparece">
          <span className="vit-protegidas"><IconoEscudo />No se usaron para el puntaje: edad, género, estado civil, foto ni dirección exacta.</span>
          <span className="boton boton-secundario boton-pequeno">Ajustar puntaje</span>
        </div>
      )}
    </div>
  )
}
