import { useState } from 'react'
import { useParams } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoAlerta, IconoCheck, IconoEscudo, IconoSubir } from '../../shared/ui/Iconos'
import { type DocumentoPublico, estadosDocumento, fechaCorta, pesos, type TareaPublica, useOfertaPublica, useResponderOferta, useSubirDocumentoCandidato } from './api'
import './vinculacion.css'

const fechaHora = new Intl.DateTimeFormat('es-CO', { dateStyle: 'long', timeStyle: 'short' })

const ayudas: Record<string, string> = {
  Cedula: 'Por las dos caras, en una sola foto o PDF.',
  CertificadoBancario: 'De la cuenta donde te pagarán, con menos de 30 días.',
  CertificadoEps: 'Lo descargas de la página de tu EPS.',
  CertificadoPension: 'Lo descargas de la página de tu fondo de pensiones.',
}

/** El candidato sube sus documentos de ingreso; ve si la IA los está leyendo, si quedaron aprobados o por qué los rechazaron. */
function MisDocumentos({ token, documentos }: { token: string; documentos: DocumentoPublico[] }) {
  const subir = useSubirDocumentoCandidato(token)
  const faltan = documentos.filter((d) => d.obligatorio && (d.estado === 'Pendiente' || d.estado === 'Rechazado')).length
  return (
    <section className="tarjeta vin-firma" aria-label="Tus documentos de ingreso">
      <h2>Tus documentos de ingreso</h2>
      <p>{faltan === 0 ? 'Ya subiste todo lo obligatorio. Te avisamos si algo hay que corregir.' : `Te faltan ${faltan}. Súbelos en PDF o como foto con buena luz.`}</p>
      <ul className="vin-mis-docs">
        {documentos.map((d) => (
          <li key={d.tipo}>
            <div>
              <b>{d.nombre}</b>{!d.obligatorio && <span className="vin-sub"> · opcional</span>}
              {ayudas[d.tipo] && <span className="vin-sub">{ayudas[d.tipo]}</span>}
              {d.estado === 'Rechazado' && <span className="vin-alerta-texto">Hay que corregirlo: {d.motivoRechazo}</span>}
              {d.nombreArchivo && d.estado !== 'Rechazado' && <span className="vin-sub">{d.nombreArchivo}</span>}
            </div>
            <span className={`pill ${estadosDocumento[d.estado].clase}`}>{d.estado === 'Leyendo' ? 'Revisando' : d.estado === 'PorRevisar' ? 'Recibido' : estadosDocumento[d.estado].texto}</span>
            {(d.estado === 'Pendiente' || d.estado === 'Rechazado') && (
              <label className="boton boton-secundario boton-pequeno vin-subir" aria-busy={(subir.isPending && subir.variables?.tipo === d.tipo) || undefined}>
                {subir.isPending && subir.variables?.tipo === d.tipo ? <span className="indicador" aria-hidden="true" /> : <IconoSubir />}
                {subir.isPending && subir.variables?.tipo === d.tipo ? 'Subiendo…' : 'Subir'}
                <input type="file" accept="application/pdf,image/jpeg,image/png,image/webp" capture="environment" hidden disabled={subir.isPending}
                  onChange={(e) => { const f = e.target.files?.[0]; if (f) subir.mutate({ tipo: d.tipo, archivo: f }); e.target.value = '' }} />
              </label>
            )}
          </li>
        ))}
      </ul>
      {subir.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{subir.error.message}</p>}
      <p className="ayuda"><IconoEscudo /> Tus documentos se guardan cifrados y solo los ve el equipo de selección de la empresa.</p>
    </section>
  )
}

/** El trabajador ve su plan de inducción aprobado: qué pasa, con quién y cuándo. */
function MiInduccion({ tareas }: { tareas: TareaPublica[] }) {
  const hechas = tareas.filter((t) => t.estado !== 'Pendiente').length
  return (
    <section className="tarjeta vin-firma" aria-label="Tu inducción">
      <h2>Tu inducción</h2>
      <p>Esto es lo que viene en tus primeros días{hechas > 0 ? ` · ya van ${hechas} de ${tareas.length}` : ''}. Si tienes dudas, pregúntale a tu líder o a Recursos Humanos.</p>
      <ul className="vin-mis-docs">
        {tareas.map((t) => (
          <li key={`${t.fecha}-${t.titulo}`}>
            <div>
              <b>{t.titulo}</b>
              {t.descripcion && <span className="vin-sub">{t.descripcion}</span>}
              <span className="vin-sub">{t.responsable}</span>
            </div>
            <span className="mono">{fechaCorta(t.fecha)}</span>
            {t.estado === 'Hecha' && <span className="pill pill-ok">Hecha</span>}
          </li>
        ))}
      </ul>
    </section>
  )
}

/** El candidato lee su oferta y su contrato y los acepta con su nombre y su documento, sin cuenta. */
export function OfertaCandidato() {
  const { token = '' } = useParams()
  const oferta = useOfertaPublica(token)
  const responder = useResponderOferta(token)
  const [nombre, setNombre] = useState('')
  const [documento, setDocumento] = useState('')
  const [rechazando, setRechazando] = useState(false)
  const [motivo, setMotivo] = useState('')

  if (oferta.isPending) return <div className="vin-publica"><Esqueleto alto={32} ancho="70%" /><Esqueleto alto={160} /><Esqueleto alto={300} /></div>
  if (oferta.isError) return <div className="vin-publica"><p role="alert" className="aviso aviso-error"><IconoAlerta />{oferta.error.message}</p></div>

  const o = oferta.data
  const c = o.condiciones
  return (
    <div className="vin-publica">
      <header>
        <span className="vin-etiqueta">Oferta laboral · {o.empresa}</span>
        <h1>{o.cargo}</h1>
        {o.empresaUsuaria && <p>En misión en {o.empresaUsuaria}</p>}
      </header>

      {o.estado === 'Aceptada' && (
        <p className="aviso aviso-ok"><IconoCheck />Aceptaste esta oferta el {fechaHora.format(new Date(o.respondidaEn!))}.{o.etapa === 'Documentos' ? ' Ahora sube tus documentos de ingreso.' : ''}</p>
      )}
      {o.etapa === 'Documentos' && o.documentos.length > 0 && <MisDocumentos token={token} documentos={o.documentos} />}
      {o.induccion.length > 0 && <MiInduccion tareas={o.induccion} />}
      {o.estado === 'Rechazada' && <p className="aviso aviso-alerta"><IconoAlerta />Respondiste que no aceptas esta oferta. Gracias por avisarnos.</p>}

      <dl className="vin-resumen tarjeta">
        <div><dt>Contrato</dt><dd>{c.tipoNombre}</dd></div>
        <div><dt>Salario</dt><dd className="mono">{pesos.format(c.salarioCop)}</dd></div>
        <div><dt>Ingreso</dt><dd className="mono">{fechaCorta(c.fechaIngreso)}</dd></div>
        {c.fechaFin && <div><dt>Hasta</dt><dd className="mono">{fechaCorta(c.fechaFin)}</dd></div>}
        <div><dt>Jornada</dt><dd>{c.jornada}</dd></div>
        <div><dt>Lugar</dt><dd>{c.ciudad}</dd></div>
        {c.auxilioTransporte && <div><dt>Auxilio de transporte</dt><dd className="mono">{pesos.format(c.auxilioTransporteCop)}</dd></div>}
      </dl>

      <article className="vin-documento tarjeta">
        <div className="vin-carta">{o.carta}</div>
        <h2>Contrato</h2>
        {o.clausulas.map((x) => <div key={x.titulo} className="vin-clausula"><b>{x.titulo}</b><p>{x.texto}</p></div>)}
      </article>

      {o.estado === 'Enviada' && (
        rechazando ? (
          <form className="tarjeta formulario vin-firma" onSubmit={(e) => { e.preventDefault(); responder.mutate({ acepta: false, nombre: null, documento: null, motivo: motivo.trim() || null }) }}>
            <label>¿Nos cuentas por qué? <span className="ayuda">(opcional)</span>
              <textarea className="area-texto" rows={2} value={motivo} onChange={(e) => setMotivo(e.target.value)} placeholder="Acepté otra oferta" /></label>
            <div className="vin-acciones">
              <Boton type="submit" cargando={responder.isPending} textoCargando="Enviando…">No acepto la oferta</Boton>
              <Boton onClick={() => setRechazando(false)}>Volver</Boton>
            </div>
            {responder.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{responder.error.message}</p>}
          </form>
        ) : (
          <form className="tarjeta formulario vin-firma" onSubmit={(e) => { e.preventDefault(); responder.mutate({ acepta: true, nombre: nombre.trim(), documento, motivo: null }) }}>
            <h2>Aceptar la oferta</h2>
            <p>Para aceptar, escribe tu nombre completo y tu número de cédula. Guardamos la fecha, la hora, tu IP y una huella del texto que leíste.</p>
            <label>Nombre completo<input className="entrada" value={nombre} onChange={(e) => setNombre(e.target.value)} autoComplete="name" /></label>
            <label>Número de cédula<input className="entrada mono" inputMode="numeric" value={documento} onChange={(e) => setDocumento(e.target.value)} /></label>
            <div className="vin-acciones">
              <Boton type="submit" variante="primario" disabled={!nombre.trim() || !documento.trim()} cargando={responder.isPending} textoCargando="Aceptando…">Acepto la oferta y el contrato</Boton>
              <button type="button" className="enlace-boton" onClick={() => setRechazando(true)}>No voy a aceptar</button>
            </div>
            {responder.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{responder.error.message}</p>}
            <p className="ayuda"><IconoEscudo /> Es una aceptación electrónica con evidencia; la firma electrónica certificada llega más adelante.</p>
          </form>
        )
      )}
      {o.huella && <p className="ayuda mono vin-huella">Huella: {o.huella}</p>}
    </div>
  )
}
