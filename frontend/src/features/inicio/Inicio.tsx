import { type CSSProperties, useRef } from 'react'
import { Link } from 'react-router-dom'
import { Chispa, IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { LogoAnimado } from '../../shared/ui/LogoAnimado'
import { useContador, useDesplazamiento, useEnVista, useEscena, usePuntero, useRevelarAlDesplazar } from '../../shared/ui/animaciones'
import { HojasCayendo } from '../../shared/ui/HojasCayendo'
import { PorQueSelvana } from './PorQueSelvana'
import { Vitrina } from './Vitrina'
import { PieLegal } from './PieLegal'

const pasos = [
  { titulo: 'Escribes una frase', texto: '«Necesito 25 auxiliares de bodega en Funza, turno noche». El agente redacta el aviso, los requisitos, las preguntas y el rango salarial.' },
  { titulo: 'La IA atrae y lee', texto: 'La vacante sale al portal de empleo. Cada candidato sube su hoja de vida y la IA llena la postulación; él solo confirma.' },
  { titulo: 'Preselecciona y prueba', texto: 'Conversa con cada candidato por chat, revisa los requisitos excluyentes, aplica pruebas psicotécnicas y arma la terna con 3 a 5 razones con evidencia.' },
  { titulo: 'Tú decides', texto: 'Apruebas o corriges en tu bandeja. Lo que cambies queda registrado y la IA aprende de tu criterio.' },
  { titulo: 'Contratas sin digitar', texto: 'Oferta y contrato con tu plantilla, firma con evidencia, documentos de ingreso leídos por la IA, afiliaciones prellenadas y plan de inducción.' },
  { titulo: 'El expediente sigue vivo', texto: 'El empleado nace con su contrato y su carpeta. Él actualiza sus datos y sube soportes desde su portal; tú apruebas cada cambio.' },
]

const sectores = [
  { nombre: 'Servicios temporales y outsourcing', dolor: 'Cientos de vacantes por cliente, cada empresa usuaria con su marca y sus datos separados.' },
  { nombre: 'BPO y contact center', dolor: 'Rotación alta y turnos rotativos: preselección masiva sin llamar a cada candidato.' },
  { nombre: 'Industria y logística', dolor: 'Montacargas, alturas, turno noche: los requisitos que importan se verifican antes de la entrevista con el líder.' },
  { nombre: 'Salud', dolor: 'ReTHUS, BLS y vacunación: la IA conoce lo que exige la norma para cada oficio.' },
  { nombre: 'Retail', dolor: 'Temporadas con picos de contratación en varias ciudades a la vez.' },
  { nombre: 'Construcción', dolor: 'Cuadrillas por obra, certificados de alturas y documentos vencidos bajo control.' },
]

const oficios = ['Auxiliar de bodega', 'Asesor de servicio', 'Auxiliar de enfermería', 'Montacarguista', 'Cajero', 'Oficial de obra', 'Agente bilingüe', 'Operario de producción', 'Mensajero', 'Conductor C2', 'Analista de nómina', 'Vigilante']

const modulos = [
  { nombre: 'Selección con IA', estado: 'Disponible', texto: 'Vacante en una frase, requisiciones con aprobación, portal de empleo, lector de hojas de vida, filtros, ranking explicado, chat de preselección, pruebas psicotécnicas y terna.' },
  { nombre: 'Banco de talento', estado: 'Disponible', texto: 'Busca en tus candidatos como lo dirías en voz alta y vuelve a invitar a quien encaja cuando publicas una vacante parecida.' },
  { nombre: 'Vinculación', estado: 'Disponible', texto: 'Oferta y contrato con tus plantillas, aceptación con evidencia, documentos de ingreso leídos por la IA, afiliaciones y plan de inducción.' },
  { nombre: 'Expediente vivo', estado: 'Disponible', texto: 'Contrato, novedades con antes y después, carpeta cifrada con vencimientos, carga de la planta y cambios propuestos desde cada documento.' },
  { nombre: 'Portal de la persona', estado: 'Disponible', texto: 'Cada candidato sigue sus procesos y guarda sus documentos una vez; cada empleado ve su expediente y actualiza sus datos.' },
  { nombre: 'Desempeño', estado: 'Próximamente', texto: 'Ciclos y formularios por cargo, objetivos con evidencias, borradores para el líder y calibración con 9-box.' },
  { nombre: 'Clima y retención', estado: 'Próximamente', texto: 'Encuestas anónimas y eNPS, temas de los comentarios, pulsos de ingreso y señales de riesgo de rotación.' },
]

const titular = ['La', 'IA', 'hace', 'el', 'trabajo', 'de', 'selección.']
const v = (i: number) => ({ '--i': i }) as CSSProperties

/** Página comercial de Selvana. Lleva al login y al portal de empleo. */
export function Inicio() {
  const raiz = useRef<HTMLDivElement>(null)
  const hero = useRef<HTMLElement>(null)
  const { avance, bajo } = useDesplazamiento()
  useRevelarAlDesplazar(raiz)
  usePuntero(hero)

  return (
    <div className="inicio" ref={raiz}>
      <div className="inicio-progreso" style={{ transform: `scaleX(${avance})` }} aria-hidden="true" />

      <header className={`inicio-cabecera ${bajo ? 'inicio-cabecera-baja' : ''}`}>
        <div className="inicio-ancho inicio-cabecera-fila">
          <Link to="/" aria-label="Selvana, inicio"><LogoAnimado tamano={30} palabra /></Link>
          <nav aria-label="Secciones" className="inicio-nav">
            <a href="#como">Cómo funciona</a>
            <a href="#en-accion">En acción</a>
            <a href="#por-que">Por qué Selvana</a>
            <a href="#para-quien">Para quién</a>
            <a href="#modulos">Módulos</a>
            <Link to="/empleos">Empleos</Link>
            <Link to="/mi-portal">Portal del candidato</Link>
          </nav>
          <Link to="/ingresar" className="boton boton-primario boton-pequeno">Ingresar</Link>
        </div>
      </header>

      <section className="inicio-hero" ref={hero}>
        <div className="inicio-dosel" aria-hidden="true">
          <svg className="dosel-hoja dosel-hoja-1" viewBox="0 0 48 48"><path d="M24 24C24 12 31 5 43 5C43 17 36 24 24 24Z" /></svg>
          <svg className="dosel-hoja dosel-hoja-2" viewBox="0 0 48 48"><path d="M24 24C24 36 17 43 5 43C5 31 12 24 24 24Z" /></svg>
          <svg className="dosel-hoja dosel-hoja-3" viewBox="0 0 48 48"><path d="M24 24C24 12 31 5 43 5C43 17 36 24 24 24Z" /></svg>
          <span className="inicio-foco" />
        </div>
        <HojasCayendo />

        <div className="inicio-ancho inicio-hero-fila">
          <div className="inicio-hero-texto">
            <span className="inicio-ceja inicio-entra" style={v(0)}><span className="inicio-ceja-punto" />Talento humano con IA · hecho para Colombia</span>
            <h1>
              {titular.map((palabra, i) => <span key={i} className="inicio-palabra" style={v(i)}>{palabra} </span>)}
              <span className="inicio-palabra inicio-decides" style={v(titular.length)}>Tú decides</span>{' '}
              <span className="inicio-palabra" style={v(titular.length + 1)}>a quién contratar.</span>
            </h1>
            <p className="inicio-entra" style={v(10)}>
              Selvana redacta vacantes, lee hojas de vida y prepara la terna con sus razones. Todo llega a tu bandeja explicado: nada que afecte a una persona
              pasa sin tu aprobación.
            </p>
            <div className="inicio-acciones inicio-entra" style={v(12)}>
              <Link to="/ingresar" className="boton boton-primario inicio-brillo">Probar Selvana</Link>
              <Link to="/empleos" className="boton boton-secundario">Ver el portal de empleo</Link>
            </div>
          </div>
          <BandejaViva />
        </div>
        <a href="#como" className="inicio-bajar" aria-label="Ver cómo funciona"><span /></a>
      </section>

      <section className="inicio-violeta">
        <div className="inicio-ancho inicio-violeta-fila">
          <span className="inicio-transicion" aria-hidden="true">
            <span className="inicio-muestra-ia"><Chispa />Propuesto por la IA</span>
            <span className="inicio-camino"><span className="inicio-viajero" /></span>
            <span className="inicio-muestra-ok"><IconoCheck />Decidido por una persona</span>
          </span>
          <p data-revelar>Es la regla de Selvana: si algo está en violeta, lo hizo la IA y aún nadie lo aprobó. Cuando tú lo confirmas, pasa a verde y queda en la auditoría.</p>
        </div>
      </section>

      <Cifras />

      <section id="como" className="inicio-seccion">
        <div className="inicio-ancho">
          <h2 data-revelar>De la vacante al empleado</h2>
          <ol className="inicio-pasos" data-revelar>
            {pasos.map((p, i) => (
              <li key={p.titulo} style={v(i)}>
                <span className="mono inicio-paso-num">{String(i + 1).padStart(2, '0')}</span>
                <b>{p.titulo}</b>
                <p>{p.texto}</p>
              </li>
            ))}
          </ol>
        </div>
      </section>

      <Vitrina />

      <PorQueSelvana />

      <div className="inicio-marquesina" aria-hidden="true">
        <div className="inicio-marquesina-pista">
          {[...oficios, ...oficios].map((o, i) => <span key={i}><svg viewBox="0 0 24 24"><path d="M2 22C2 10 9 2 22 2C22 14 14 22 2 22Z" /></svg>{o}</span>)}
        </div>
      </div>

      <section id="para-quien" className="inicio-seccion inicio-seccion-tenue">
        <div className="inicio-ancho">
          <h2 data-revelar>Para empresas que contratan mucho y bien</h2>
          <div className="inicio-sectores" data-revelar>
            {sectores.map((s, i) => (
              <article key={s.nombre} className="tarjeta inicio-sector" style={v(i)}>
                <svg className="inicio-sector-hoja" viewBox="0 0 24 24" aria-hidden="true"><path d="M2 22C2 10 9 2 22 2C22 14 14 22 2 22Z" /></svg>
                <b>{s.nombre}</b>
                <p>{s.dolor}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="inicio-seccion">
        <div className="inicio-ancho inicio-distintos" data-revelar>
          <div style={v(0)}>
            <span className="inicio-distinto-icono"><IconoConexionAnimado /></span>
            <h3>Se conecta con tu nómina, no la reemplaza</h3>
            <p>Funciona con cualquier ERP que liquide nómina. Lee empleados, cargos y centros de costo; la IA los ordena y tú apruebas antes de que cambie algo.</p>
          </div>
          <div style={v(1)}>
            <span className="inicio-distinto-icono"><IconoEscudo /></span>
            <h3>Colombia en cada detalle</h3>
            <p>Autorización de la Ley 1581 en cada postulación, solicitudes de los titulares con plazos hábiles, salario mínimo vigente, ARL por clase de riesgo, EPS, pensión y caja.</p>
          </div>
          <div style={v(2)}>
            <span className="inicio-distinto-icono"><IconoCheck /></span>
            <h3>IA que se explica</h3>
            <p>Cada sugerencia trae sus razones con evidencia. Edad, género, estado civil y foto nunca cuentan. Hojas de vida cifradas y costo de IA medido.</p>
          </div>
        </div>
      </section>

      <section id="modulos" className="inicio-seccion inicio-seccion-tenue">
        <div className="inicio-ancho">
          <h2 data-revelar>Un módulo de talento humano completo</h2>
          <div className="inicio-modulos" data-revelar>
            {modulos.map((m, i) => (
              <article key={m.nombre} className={`tarjeta inicio-modulo ${m.estado === 'Disponible' ? 'inicio-modulo-vivo' : ''}`} style={v(i)}>
                <span className={`pill ${m.estado === 'Disponible' ? 'pill-ok' : 'pill-neutra'}`}>{m.estado}</span>
                <b>{m.nombre}</b>
                <p>{m.texto}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="inicio-cierre">
        <div className="inicio-cierre-fondo" aria-hidden="true">
          <LogoAnimado tamano={220} claro className="inicio-cierre-logo" />
        </div>
        <HojasCayendo cantidad={6} className="hojas-claras" />
        <div className="inicio-ancho inicio-cierre-fila" data-revelar>
          <div>
            <h2>Empieza con tu próxima vacante</h2>
            <p>Escribe una frase y mira lo que la IA prepara para ti.</p>
          </div>
          <Link to="/ingresar" className="boton inicio-boton-claro inicio-halo">Probar Selvana</Link>
        </div>
      </section>

      <PieLegal />
    </div>
  )
}

function IconoConexionAnimado() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.6} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <rect x="2.5" y="8" width="7" height="8" rx="2" />
      <rect x="14.5" y="8" width="7" height="8" rx="2" />
      <path className="inicio-pulso-linea" d="M9.5 12h5" />
    </svg>
  )
}

const razones = [
  { signo: '+', texto: '3 años en contact center', cita: 'HV p.1' },
  { signo: '+', texto: 'Acepta turnos rotativos', cita: 'Postulación' },
  { signo: '−', texto: 'Aspira 5 % sobre el rango', cita: 'Postulación' },
]
const tituloRedactado = 'Auxiliar de enfermería · Medellín'

/**
 * Escena de 14 s que se repite: la IA lee hojas de vida (con avance real), propone la terna con sus razones,
 * alguien la aprueba y pasa de violeta a verde; mientras, el agente redacta otra vacante letra por letra.
 */
function BandejaViva() {
  const t = useEscena(140, 120)
  const leidas = Math.min(128, Math.round((t / 40) * 128))
  const leyendo = t < 40
  const aprobada = t >= 96
  const pulsando = t >= 88 && t < 96
  const razonesVisibles = leyendo ? 0 : Math.min(3, Math.floor((t - 44) / 10) + 1)
  const letras = Math.max(0, Math.min(tituloRedactado.length, Math.floor((t - 20) * 0.8)))
  const porDecidir = aprobada ? 2 : 3

  return (
    <div className="inicio-bandeja inicio-entra" style={v(6)} aria-label="Ejemplo animado de la bandeja de aprobaciones">
      <div className="inicio-bandeja-cabeza">
        <b>Bandeja de aprobaciones</b>
        <span className="pill pill-neutra" key={porDecidir}><span className="inicio-cuenta">{porDecidir}</span> por decidir</span>
      </div>

      <div className={`inicio-bandeja-item ${aprobada ? 'inicio-item-ok' : 'inicio-item-ia'} inicio-item-vivo`}>
        {leyendo ? (
          <span className="pill pill-ia"><span className="orbe trabajando inicio-orbe" />Leyendo hojas de vida <span className="mono">{leidas}/128</span></span>
        ) : aprobada ? (
          <span className="pill pill-ok inicio-aparece"><IconoCheck />Aprobado por Laura M.</span>
        ) : (
          <span className="pill pill-ia inicio-aparece"><Chispa />Terna propuesta</span>
        )}
        <b>Asesor(a) de servicio al cliente · Bogotá</b>
        {leyendo ? (
          <div className="barra inicio-barra" aria-hidden="true"><span style={{ width: `${(leidas / 128) * 100}%` }} /></div>
        ) : (
          <ul>
            {razones.slice(0, razonesVisibles).map((r) => (
              <li key={r.texto} className="inicio-aparece">
                <span className={`signo ${r.signo === '+' ? 'mas' : 'menos'}`}>{r.signo}</span> {r.texto} <span className="cita">{r.cita}</span>
              </li>
            ))}
          </ul>
        )}
        {!leyendo && !aprobada && razonesVisibles === 3 && (
          <span className="inicio-bandeja-acciones inicio-aparece">
            <span className={`boton boton-primario boton-pequeno ${pulsando ? 'inicio-pulsado' : ''}`}>Aprobar terna</span>
            <span className="boton boton-secundario boton-pequeno">Ajustar</span>
          </span>
        )}
        {aprobada && <span className="inicio-registro inicio-aparece mono">Sugerencia y decisión registradas · 10:42</span>}
      </div>

      <div className="inicio-bandeja-item inicio-item-ok">
        <span className="pill pill-ok"><IconoCheck />Aprobado por Laura M.</span>
        <b>Perfil de cargo · Auxiliar de bodega</b>
      </div>

      <div className="inicio-bandeja-item inicio-item-ia">
        <span className="pill pill-ia"><Chispa />{letras < tituloRedactado.length ? 'Redactando vacante' : 'Vacante redactada'}</span>
        <b>{tituloRedactado.slice(0, letras)}<span className={`inicio-cursor ${letras < tituloRedactado.length ? '' : 'inicio-cursor-fin'}`} /></b>
      </div>
    </div>
  )
}

const cifras = [
  { desde: 0, hasta: 1, antes: '', despues: ' frase', texto: 'basta para que la IA redacte una vacante completa' },
  { desde: 0, hasta: 5, antes: '3 a ', despues: '', texto: 'razones con evidencia citada por cada candidato' },
  { desde: 5, hasta: 0, antes: '', despues: '', texto: 'datos protegidos en el puntaje: ni edad, ni género, ni estado civil, ni foto, ni dirección' },
  { desde: 0, hasta: 100, antes: '', despues: ' %', texto: 'de las decisiones sobre personas las aprueba alguien de tu equipo' },
]

function Cifras() {
  const ref = useRef<HTMLElement>(null)
  const visto = useEnVista(ref)
  return (
    <section className="inicio-cifras" ref={ref}>
      <div className="inicio-ancho inicio-cifras-fila">
        {cifras.map((c, i) => <Cifra key={c.texto} {...c} activo={visto} i={i} />)}
      </div>
    </section>
  )
}

function Cifra({ desde, hasta, antes, despues, texto, activo, i }: (typeof cifras)[number] & { activo: boolean; i: number }) {
  const valor = useContador(desde, hasta, activo, 1400 + i * 200)
  return (
    <div className={`inicio-cifra ${activo ? 'revelado' : ''}`} style={v(i)}>
      <span className="inicio-cifra-numero">{antes}{valor}{despues}</span>
      <span>{texto}</span>
    </div>
  )
}
