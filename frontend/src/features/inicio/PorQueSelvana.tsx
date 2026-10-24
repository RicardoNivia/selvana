import { type CSSProperties, type ReactNode, useState } from 'react'
import { Chispa, IconoAlerta, IconoCandado, IconoCheck, IconoConexion, IconoEscudo } from '../../shared/ui/Iconos'

function Bloque({ etiqueta, titulo, texto, children }: { etiqueta: string; titulo: string; texto: string; children: ReactNode }) {
  return (
    <article className="pq-bloque" data-revelar>
      <span className="pq-etiqueta">{etiqueta}</span>
      <h3>{titulo}</h3>
      <p>{texto}</p>
      <div className="pq-demo">{children}</div>
    </article>
  )
}

/* 1 · Sin sesgo por diseño: lo que ve la persona frente a lo que ve la IA */
const camposHv = [
  { etiqueta: 'Nombre', valor: 'Andrea Rojas Peña', oculto: true },
  { etiqueta: 'Cédula', valor: 'CC 1.023.456.789', oculto: true, mono: true },
  { etiqueta: 'Celular', valor: '300 412 7781', oculto: true, mono: true },
  { etiqueta: 'Edad', valor: '34 años', oculto: true },
  { etiqueta: 'Estado civil', valor: 'Casada', oculto: true },
  { etiqueta: 'Dirección', valor: 'Calle 45 # 12-30', oculto: true, ancho: 'dos' },
  { etiqueta: 'Ciudad', valor: 'Mosquera', oculto: false },
  { etiqueta: 'Experiencia', valor: 'Operadora de montacargas · 4 años', oculto: false, ancho: 'tres' },
  { etiqueta: 'Cursos', valor: 'Montacargas · SENA · vigente', oculto: false, ancho: 'tres' },
]

function SinSesgo() {
  const [ia, setIa] = useState(false)
  return (
    <>
      <div className="segmentado pq-segmentado" role="group" aria-label="Vista de la hoja de vida">
        <button type="button" aria-pressed={!ia} onClick={() => setIa(false)}>Lo que usted ve</button>
        <button type="button" aria-pressed={ia} onClick={() => setIa(true)}><Chispa />Lo que ve la IA</button>
      </div>
      <dl className={`pq-hv ${ia ? 'vista-ia' : ''}`}>
        <div className="pq-campo pq-foto protegido">
          <dt>Foto</dt>
          <dd><span className="pq-avatar" aria-hidden="true">{ia ? <IconoCandado /> : 'AR'}</span>{ia && <span className="sr-only">Foto oculta para la IA</span>}</dd>
        </div>
        {camposHv.map((c) => (
          <div key={c.etiqueta} className={`pq-campo ${c.oculto ? 'protegido' : 'util'} ${c.ancho ?? ''}`}>
            <dt>{c.etiqueta}</dt>
            <dd className={c.mono ? 'mono' : undefined}>
              <span className="pq-valor">{c.valor}</span>
              {c.oculto && <span className="pq-oculto" aria-hidden={!ia}>no lo ve</span>}
            </dd>
          </div>
        ))}
      </dl>
      <p className="pq-nota" aria-live="polite">
        {ia ? 'El agente califica solo con lo que sirve para el puesto. Si una razón menciona algo protegido, se retira.' : 'Toque «Lo que ve la IA» para ver qué datos nunca entran al puntaje.'}
      </p>
    </>
  )
}

/* 2 · Cada puntaje se explica: clic en una razón y se resalta su evidencia */
const razones = [
  { id: 'exp', signo: '+', texto: '4 años operando montacargas en logística', cita: 'HV · experiencia 1' },
  { id: 'curso', signo: '+', texto: 'Curso de montacargas vigente', cita: 'HV · cursos' },
  { id: 'filtro', signo: '+', texto: 'Acepta el turno noche', cita: 'Preguntas filtro' },
  { id: 'lic', signo: '−', texto: 'No demuestra licencia de conducción C1', cita: 'HV · cursos' },
]
const evidencias = [
  { id: 'exp', texto: 'Operadora de montacargas — Logística del Occidente (2022 a hoy)' },
  { id: 'curso', texto: 'Curso de montacargas · SENA · vigente hasta 03/2027' },
  { id: 'lic', texto: 'Cursos: no menciona licencia de conducción' },
  { id: 'filtro', texto: '¿Puede trabajar en turno noche? → Sí' },
]

function PuntajeExplicado() {
  const [activa, setActiva] = useState('exp')
  return (
    <div className="pq-explica">
      <div className="pq-razones">
        <div className="pq-puntaje"><Chispa /><b className="mono">86</b><span>/100 · sugerencia de la IA</span></div>
        {razones.map((r) => (
          <button key={r.id} type="button" className="pq-razon" aria-pressed={activa === r.id} onClick={() => setActiva(r.id)}
            onMouseEnter={() => setActiva(r.id)} onFocus={() => setActiva(r.id)}>
            <span className={`signo ${r.signo === '+' ? 'mas' : 'menos'}`}>{r.signo}</span>
            <span className="pq-razon-texto">{r.texto}</span>
            <span className="cita">{r.cita}</span>
          </button>
        ))}
      </div>
      <div className="pq-evidencia" aria-label="Evidencia en la hoja de vida">
        <span className="pq-evidencia-titulo">Hoja de vida</span>
        {evidencias.map((e) => <p key={e.id} className={activa === e.id ? 'resaltada' : undefined}>{e.texto}</p>)}
      </div>
    </div>
  )
}

/* 3 · Usted tiene la última palabra: ajustar con motivo y verlo en la bitácora */
const motivos = ['El curso venció en 2025', 'Ya trabajó con nosotros', 'Vive lejos del turno noche']

function UltimaPalabra() {
  const [paso, setPaso] = useState<'ver' | 'ajustar' | 'listo'>('ver')
  const [motivo, setMotivo] = useState(motivos[0]!)
  const [ajustado, setAjustado] = useState(false)
  return (
    <div className="pq-ultima">
      <div className="pq-fila-puntaje">
        <span>Andrea R. · Operadora de montacargas</span>
        <b className={`mono pq-chip-puntaje ${paso === 'listo' ? 'humano' : 'ia'}`}>{paso === 'listo' && ajustado ? 72 : 86}</b>
      </div>
      {paso === 'ver' && (
        <div className="pq-acciones">
          <button type="button" className="boton boton-primario boton-pequeno" onClick={() => { setAjustado(false); setPaso('listo') }}><IconoCheck />Mantener el puntaje</button>
          <button type="button" className="boton boton-secundario boton-pequeno" onClick={() => setPaso('ajustar')}>Ajustar puntaje</button>
        </div>
      )}
      {paso === 'ajustar' && (
        <div className="pq-ajuste">
          <span>¿Por qué lo ajusta?</span>
          <div className="pq-motivos">
            {motivos.map((m) => <button key={m} type="button" className="pq-motivo" aria-pressed={motivo === m} onClick={() => setMotivo(m)}>{m}</button>)}
          </div>
          <div className="pq-acciones">
            <button type="button" className="boton boton-primario boton-pequeno" onClick={() => { setAjustado(true); setPaso('listo') }}>Guardar ajuste a 72</button>
            <button type="button" className="boton boton-secundario boton-pequeno" onClick={() => setPaso('ver')}>Cancelar</button>
          </div>
        </div>
      )}
      {paso === 'listo' && (
        <div className="pq-bitacora" role="status">
          <span className="mono">04:32 p. m.</span>
          <span><b>Laura M.</b> {ajustado ? 'ajustó la afinidad 86 → 72' : 'confirmó la afinidad de 86'}</span>
          <small>{ajustado ? `Motivo: «${motivo}» · la sugerencia de la IA se conserva para medir al agente` : 'La sugerencia pasa a decisión de una persona'}</small>
          <button type="button" className="enlace-boton" onClick={() => { setPaso('ver'); setMotivo(motivos[0]!) }}>Repetir</button>
        </div>
      )}
      <p className="pq-nota">Descartar, contratar o calificar a una persona siempre pasa por alguien de su equipo.</p>
    </div>
  )
}

/* 4 · Se conecta con su nómina */
const fuentes = [
  { id: 'archivo', nombre: 'Archivo Excel o CSV', estado: 'Disponible hoy', texto: 'Suba el archivo de su nómina: Selvana trae sedes, áreas, centros de costo y cargos, y la IA propone unir los cargos repetidos.' },
  { id: 'otro', nombre: 'Su ERP de nómina', estado: 'Por archivo programado hoy', texto: 'Con cualquier ERP que liquide nómina: un archivo programado ahora y un adaptador dedicado cuando haga falta.' },
  { id: 'api', nombre: 'API propia', estado: 'En camino', texto: 'Una API abierta con eventos para conectar sistemas propios sin tocar su base de datos.' },
]

function ConectaNomina() {
  const [fuente, setFuente] = useState('archivo')
  const f = fuentes.find((x) => x.id === fuente)!
  return (
    <div className="pq-conecta">
      <div className="pq-fuentes" role="group" aria-label="Su sistema de nómina">
        {fuentes.map((x) => <button key={x.id} type="button" className="pq-fuente" aria-pressed={fuente === x.id} onClick={() => setFuente(x.id)}>{x.nombre}</button>)}
      </div>
      <div className="pq-flujo" key={fuente} aria-hidden="true">
        <span className="pq-nodo">{f.nombre}</span>
        <span className="pq-tubo"><span /><span /><span /></span>
        <span className="pq-nodo pq-nodo-selvana"><IconoConexion />Selvana</span>
      </div>
      <p className="pq-nota" aria-live="polite"><b className={fuente === 'archivo' ? 'pq-disponible' : 'pq-pronto'}>{f.estado}.</b> {f.texto} Su nómina sigue donde está.</p>
    </div>
  )
}

/* 5 · Ley 1581 con evidencia verificable */
const eventos = [
  { titulo: 'Autorización aceptada', detalle: 'Versión v3 · 22 oct 2026, 08:12', huella: 'a3f2…9c1e' },
  { titulo: 'Solicitud del titular', detalle: 'Consulta radicada · vence en 10 días hábiles', huella: '7b10…e44a' },
  { titulo: 'Supresión aprobada', detalle: 'Datos borrados en todos los módulos', huella: 'd95c…02f7' },
]

function Ley1581() {
  const [estado, setEstado] = useState<'listo' | 'verificando' | 'integra'>('listo')
  const verificar = () => {
    setEstado('verificando')
    setTimeout(() => setEstado('integra'), window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : 1300)
  }
  return (
    <div className="pq-ley">
      <ol className={`pq-cadena ${estado}`}>
        {eventos.map((e, i) => (
          <li key={e.titulo} style={{ '--i': i } as CSSProperties}>
            <span className="pq-eslabon" aria-hidden="true">{estado === 'integra' ? <IconoCheck /> : <IconoCandado />}</span>
            <span><b>{e.titulo}</b><small>{e.detalle}</small></span>
            <span className="mono cita">{e.huella}</span>
          </li>
        ))}
      </ol>
      <div className="pq-acciones">
        <button type="button" className="boton boton-secundario boton-pequeno" onClick={verificar} disabled={estado === 'verificando'} aria-busy={estado === 'verificando' || undefined}>
          {estado === 'verificando' ? <><span className="indicador indicador-oscuro" aria-hidden="true" />Verificando…</> : <><IconoEscudo />Verificar integridad</>}
        </button>
        {estado === 'integra' && <span className="pq-integra" role="status"><IconoCheck />Cadena íntegra: ningún registro fue alterado</span>}
      </div>
      <p className="pq-nota">Cada línea guarda la huella de la anterior: si alguien cambia un registro, la verificación lo detecta.</p>
    </div>
  )
}

/* 6 · Alertas antes de decidir */
const empleos = [
  { id: 'a', cargo: 'Auxiliar de bodega', periodo: '2016 – 2019', desde: 0, hasta: 30 },
  { id: 'b', cargo: 'Alistadora', periodo: '2018 – 2020', desde: 22, hasta: 42 },
  { id: 'c', cargo: 'Operadora de montacargas', periodo: '2022 – hoy', desde: 62, hasta: 100 },
]
const alertas: Record<string, { titulo: string; texto: string; evidencia: string }> = {
  b: { titulo: 'Fechas que se cruzan', texto: 'Bodega y alistamiento se cruzan 14 meses. Puede ser un trabajo simultáneo: confírmelo.', evidencia: 'HV · experiencias 1 y 2' },
  hueco: { titulo: 'Hueco en la experiencia', texto: '20 meses sin experiencia registrada entre 2020 y 2022.', evidencia: 'HV · experiencias 2 y 3' },
  dup: { titulo: 'Posible duplicado', texto: 'Otra cédula usa el mismo celular. Puede ser la misma persona.', evidencia: 'Mismo celular' },
}

function Alertas() {
  const [abierta, setAbierta] = useState<string>()
  const a = abierta ? alertas[abierta] : undefined
  return (
    <div className="pq-alertas">
      <div className="pq-linea" role="group" aria-label="Experiencia en el tiempo">
        {empleos.map((e) => (
          <button key={e.id} type="button" className={`pq-tramo ${alertas[e.id] ? 'con-alerta' : ''}`} style={{ left: `${e.desde}%`, width: `${e.hasta - e.desde}%`, top: e.id === 'b' ? 28 : 0 }}
            aria-expanded={abierta === e.id} onMouseEnter={() => setAbierta(alertas[e.id] ? e.id : undefined)} onFocus={() => setAbierta(alertas[e.id] ? e.id : undefined)}
            onClick={() => setAbierta(alertas[e.id] ? e.id : undefined)}>
            <b>{e.cargo}</b><span className="mono">{e.periodo}</span>
          </button>
        ))}
        <button type="button" className="pq-hueco" style={{ left: '42%', width: '20%' }} aria-expanded={abierta === 'hueco'}
          onMouseEnter={() => setAbierta('hueco')} onFocus={() => setAbierta('hueco')} onClick={() => setAbierta('hueco')}>
          <IconoAlerta /><span className="sr-only">Hueco en la experiencia</span>
        </button>
      </div>
      <button type="button" className="pq-dup" aria-expanded={abierta === 'dup'} onMouseEnter={() => setAbierta('dup')} onFocus={() => setAbierta('dup')} onClick={() => setAbierta('dup')}>
        <IconoAlerta />Celular 300 412 7781 · también en otra postulación
      </button>
      <div className={`pq-alerta ${a ? 'visible' : ''}`} aria-live="polite">
        {a ? <><IconoAlerta /><span><b>{a.titulo}.</b> {a.texto} <span className="cita">{a.evidencia}</span></span></>
          : <span>Pase el mouse por la línea de tiempo: las alertas salen solas, con su evidencia.</span>}
      </div>
    </div>
  )
}

/** Lo que hace distinto a Selvana, para tocarlo: cada bloque es una pieza real del producto en miniatura. */
export function PorQueSelvana() {
  return (
    <section id="por-que" className="inicio-seccion pq">
      <div className="inicio-ancho">
        <h2 data-revelar>Por qué Selvana</h2>
        <p className="pq-intro" data-revelar>Seis cosas que su equipo va a notar desde la primera vacante. Pruébelas aquí mismo: son piezas reales del producto.</p>
        <div className="pq-rejilla">
          <Bloque etiqueta="Sin sesgo por diseño" titulo="La IA no sabe quién es" texto="El agente califica un perfil anónimo: sin nombre, cédula, contacto, edad, estado civil, foto ni dirección.">
            <SinSesgo />
          </Bloque>
          <Bloque etiqueta="IA que se explica" titulo="Cada puntaje trae sus razones" texto="De 3 a 5 razones con signo y la cita exacta de la hoja de vida. Toque una razón y vea de dónde sale.">
            <PuntajeExplicado />
          </Bloque>
          <Bloque etiqueta="Usted decide" titulo="La última palabra es de su equipo" texto="La IA propone; una persona mantiene o ajusta, con su motivo. Todo queda en la bitácora.">
            <UltimaPalabra />
          </Bloque>
          <Bloque etiqueta="Alertas antes de decidir" titulo="Lo que no cuadra, salta a la vista" texto="Fechas que se cruzan, huecos, fechas imposibles y posibles duplicados, cada uno con su evidencia.">
            <Alertas />
          </Bloque>
          <Bloque etiqueta="Con la nómina que ya tiene" titulo="Se conecta, no la reemplaza" texto="Selvana trabaja encima del ERP que liquida su nómina. Elija el suyo:">
            <ConectaNomina />
          </Bloque>
          <Bloque etiqueta="Ley 1581, sin papeleo" titulo="Autorización con evidencia verificable" texto="Cada candidato autoriza al postularse, las solicitudes de titulares corren con su plazo legal y la bitácora no se puede alterar.">
            <Ley1581 />
          </Bloque>
        </div>
      </div>
    </section>
  )
}
