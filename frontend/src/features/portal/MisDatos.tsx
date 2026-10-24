import { type CSSProperties, type FormEvent, useState } from 'react'
import { Link } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { IconoCheck, IconoEscudo } from '../../shared/ui/Iconos'
import { type TipoSolicitud, useSolicitudPortal } from '../privacidad/api'

const opciones: { tipo: TipoSolicitud; titulo: string; texto: string; plazo: string }[] = [
  { tipo: 'Consulta', titulo: 'Saber qué datos tienen', texto: 'Te contamos qué datos tuyos guardan las empresas y para qué.', plazo: '10 días hábiles' },
  { tipo: 'Actualizacion', titulo: 'Corregir mis datos', texto: 'Cambiaste de celular, de ciudad o hay algo mal.', plazo: '15 días hábiles' },
  { tipo: 'Supresion', titulo: 'Borrar mis datos', texto: 'Eliminan tu hoja de vida y tus datos de sus procesos de selección.', plazo: '15 días hábiles' },
  { tipo: 'Revocatoria', titulo: 'Retirar mi autorización', texto: 'Dejan de usar tus datos y no te vuelven a contactar.', plazo: '15 días hábiles' },
]

/** Canal del titular (Ley 1581): consultar, corregir, borrar o revocar, sin iniciar sesión. */
export function MisDatos() {
  const enviar = useSolicitudPortal()
  const [tipo, setTipo] = useState<TipoSolicitud>('Consulta')
  const [titular, setTitular] = useState('')
  const [documento, setDocumento] = useState('')
  const [contacto, setContacto] = useState('')
  const [descripcion, setDescripcion] = useState('')
  const elegida = opciones.find((o) => o.tipo === tipo)!

  const mandar = (e: FormEvent) => {
    e.preventDefault()
    enviar.mutate({ tipo, titular: titular.trim(), documento, contacto: contacto.trim(), descripcion: descripcion.trim() })
  }

  if (enviar.isSuccess) {
    return (
      <main className="portal-ancho postular">
        <section className="postular-cuerpo postular-final" role="status">
          <span className="mis-datos-listo"><IconoCheck /></span>
          <h1>Recibimos tu solicitud</h1>
          <p className="postular-ayuda">
            Si alguna empresa de Selvana tiene datos tuyos, te responde a <b>{contacto}</b> en máximo {elegida.plazo}, como manda la Ley 1581 de 2012.
            Antes de responder puede pedirte confirmar que eres tú.
          </p>
          <Link to="/empleos" className="boton boton-secundario">Volver a las vacantes</Link>
        </section>
      </main>
    )
  }

  return (
    <main className="portal-ancho postular">
      <section className="postular-cuerpo">
        <h1>Tus datos personales</h1>
        <p className="postular-ayuda">Tú decides sobre tus datos. Pide aquí lo que necesites; no hace falta crear una cuenta.</p>

        <div className="mis-datos-opciones" role="radiogroup" aria-label="Qué quieres pedir">
          {opciones.map((o, i) => (
            <button key={o.tipo} type="button" role="radio" aria-checked={tipo === o.tipo} onClick={() => setTipo(o.tipo)} style={{ '--i': i } as CSSProperties}>
              <b>{o.titulo}</b>
              <span>{o.texto}</span>
              <span className="mono">Respuesta en {o.plazo}</span>
            </button>
          ))}
        </div>

        <form className="postular-grupo" onSubmit={mandar}>
          <label className="postular-campo">Nombre completo<input className="entrada" value={titular} onChange={(e) => setTitular(e.target.value)} autoComplete="name" required /></label>
          <label className="postular-campo">Cédula<input className="entrada" inputMode="numeric" value={documento} onChange={(e) => setDocumento(e.target.value)} required /></label>
          <label className="postular-campo">Correo o celular para responderte<input className="entrada" value={contacto} onChange={(e) => setContacto(e.target.value)} required /></label>
          <label className="postular-campo">
            {tipo === 'Actualizacion' ? 'Qué hay que corregir' : 'Algo más que debamos saber'} <span className="postular-opcional">opcional</span>
            <textarea className="entrada" rows={3} value={descripcion} onChange={(e) => setDescripcion(e.target.value)} />
          </label>
          {enviar.error && <p role="alert" className="aviso aviso-alerta postular-error">{enviar.error.message}</p>}
          <Boton type="submit" variante="primario" className="postular-enviar" cargando={enviar.isPending} textoCargando="Enviando…">Enviar solicitud</Boton>
        </form>

        <p className="postular-confianza"><IconoEscudo />Por seguridad, la respuesta es la misma tengamos o no datos tuyos: así nadie puede averiguar dónde te postulaste.</p>
      </section>
    </main>
  )
}
