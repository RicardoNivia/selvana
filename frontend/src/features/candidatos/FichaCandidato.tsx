import { Fragment, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Esqueleto, EsqueletoPanel } from '../../shared/ui/Esqueleto'
import { IconoAlerta, IconoEscudo } from '../../shared/ui/Iconos'
import { PreseleccionPostulante } from '../preseleccion/PreseleccionPostulante'
import { AbrirHojaDeVida } from '../vacantes/AbrirHojaDeVida'
import { estadoVacante, type EstadoVacante } from '../vacantes/api'
import { type CandidatoDetalle, duracion, useCandidato } from './api'
import { HojaDeVidaVisual } from './HojaDeVidaVisual'

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' })
const iniciales = (n: string) => n.split(' ').filter(Boolean).slice(0, 2).map((p) => p[0]!.toUpperCase()).join('')

/** Ficha del candidato: lo que leyó la IA y confirmó el candidato, alertas de la hoja de vida y sus postulaciones. */
export function FichaCandidato() {
  const { id = '' } = useParams()
  const candidato = useCandidato(id)
  const [pestana, setPestana] = useState<'hv' | 'postulaciones'>('hv')

  if (candidato.isPending) {
    return (
      <>
        <div className="encabezado"><Esqueleto alto={14} ancho={120} /><Esqueleto alto={34} ancho="45%" style={{ marginTop: 10 }} /></div>
        <div className="dos-columnas"><div className="tarjeta" style={{ flex: '1 1 300px' }}><EsqueletoPanel /></div><div className="tarjeta" style={{ flex: '1.6 1 460px' }}><EsqueletoPanel /></div></div>
      </>
    )
  }
  if (candidato.isError) return <p role="alert" className="aviso aviso-error"><IconoAlerta />No encontramos a esa persona o no está en su alcance.</p>

  const c = candidato.data
  return (
    <>
      <header className="encabezado ficha-cabeza">
        <Link to="/consola/candidatos" style={{ fontSize: 13 }}>← Candidatos</Link>
        <div className="ficha-titulo">
          <span className="ficha-avatar" aria-hidden="true">{iniciales(c.nombre)}</span>
          <div>
            <h1>{c.nombre}</h1>
            <p>{c.ciudad || 'Sin ciudad'} · {duracion(c.mesesExperiencia)} de experiencia · actualizado el {fecha.format(new Date(c.actualizadoEn))}</p>
          </div>
        </div>
      </header>

      {c.suprimido && <p className="aviso aviso-alerta"><IconoEscudo />El titular pidió suprimir sus datos: solo queda la constancia.</p>}

      <div className="dos-columnas">
        <aside className="tarjeta ficha-datos" aria-label="Datos del candidato">
          <b>Datos de contacto</b>
          <Dato etiqueta="Documento" valor={c.documento} mono />
          <Dato etiqueta="Celular (WhatsApp)" valor={c.celular} mono />
          <Dato etiqueta="Correo" valor={c.correo} />
          <Dato etiqueta="Ciudad" valor={c.ciudad} />
          {c.linkedIn && <a href={c.linkedIn} target="_blank" rel="noopener noreferrer" style={{ fontSize: 13 }}>Perfil de LinkedIn ↗</a>}
          {c.autorizacion && <span className="ficha-nota"><IconoEscudo />Autorizó el tratamiento de datos (Ley 1581): <span className="mono">{c.autorizacion}</span></span>}
          <span className="ficha-nota">La IA leyó la hoja de vida y el candidato confirmó los datos. Edad, género, estado civil, foto y dirección exacta no se extraen.</span>
          {c.posiblesDuplicados.length > 0 && (
            <div className="aviso aviso-alerta" role="note">
              <IconoAlerta />
              <span>
                Puede ser la misma persona:{' '}
                {c.posiblesDuplicados.map((d, i) => (
                  <span key={d.id}>{i > 0 && ', '}<Link to={`/consola/candidatos/${d.id}`}>{d.nombre}</Link> <span className="mono">({d.documento}, {d.coincide.toLowerCase()})</span></span>
                ))}
              </span>
            </div>
          )}
        </aside>

        <section className="tarjeta ficha-cuerpo">
          <div role="tablist" aria-label="Ficha del candidato" className="pestanas" style={{ padding: '0 var(--espacio-4)' }}>
            <button type="button" role="tab" className="pestana" aria-selected={pestana === 'hv'} onClick={() => setPestana('hv')}>Hoja de vida</button>
            <button type="button" role="tab" className="pestana" aria-selected={pestana === 'postulaciones'} onClick={() => setPestana('postulaciones')}>Postulaciones · {c.postulaciones.length}</button>
          </div>
          {pestana === 'hv' ? <HojaDeVidaVisual c={c} /> : <Postulaciones c={c} />}
        </section>
      </div>
    </>
  )
}

function Dato({ etiqueta, valor, mono }: { etiqueta: string; valor: string; mono?: boolean }) {
  return (
    <div className="ficha-dato">
      <span>{etiqueta}</span>
      <b className={mono ? 'mono' : undefined}>{valor || '—'}</b>
    </div>
  )
}

/** Cada postulación es una fila; su preselección se abre debajo al pedirla, para que con varias postulaciones se lea fácil. */
function Postulaciones({ c }: { c: CandidatoDetalle }) {
  const [abierta, setAbierta] = useState<string>()
  if (c.postulaciones.length === 0) return <div className="vacio"><p>No tiene postulaciones en vacantes que usted ve.</p></div>
  return (
    <div className="tabla-envoltura">
      <table className="tabla">
        <thead><tr><th>Vacante</th><th>Estado</th><th>Canal</th><th>Llegó</th><th>Hoja de vida</th></tr></thead>
        <tbody>
          {c.postulaciones.map((p) => {
            const e = estadoVacante[p.estadoVacante as EstadoVacante]
            const abiertaEsta = abierta === p.postulacionId
            return (
              <Fragment key={p.postulacionId}>
                <tr>
                  <td>
                    <Link to={`/consola/vacantes/${p.vacanteId}`} style={{ fontWeight: 600 }}>{p.vacanteTitulo}</Link>
                    <span className="mono banco-sub">{p.vacanteCodigo} · {p.numero}{p.respuestasNegativas > 0 ? ` · ${p.respuestasNegativas} respuesta(s) negativa(s)` : ''}</span>
                    <button type="button" className="enlace-boton fila-detalle" aria-expanded={abiertaEsta}
                      onClick={() => setAbierta((x) => (x === p.postulacionId ? undefined : p.postulacionId))}>
                      {abiertaEsta ? 'Ocultar preselección' : 'Ver preselección'}
                    </button>
                  </td>
                  <td>{e ? <span className={`pill ${e.clase}`}>{e.texto}</span> : p.estadoVacante}</td>
                  <td><span className="pill pill-neutra">{p.canal}</span></td>
                  <td className="mono" style={{ fontSize: 13 }}>{fecha.format(new Date(p.recibidaEn))}</td>
                  <td><AbrirHojaDeVida postulacionId={p.postulacionId} /></td>
                </tr>
                {abiertaEsta && (
                  <tr className="fila-explicacion"><td colSpan={5}>
                    <div className="fila-explicacion-cuerpo"><PreseleccionPostulante postulacionId={p.postulacionId} /></div>
                  </td></tr>
                )}
              </Fragment>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
