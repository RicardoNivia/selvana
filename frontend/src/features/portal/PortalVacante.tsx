import { type CSSProperties, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoCheck, IconoCompartir, IconoCupos, IconoDinero, IconoEscudo, IconoFlecha, IconoReloj, IconoUbicacion } from '../../shared/ui/Iconos'
import { useMiPortal } from '../portal-persona/api'
import { rangoSalario, useVacantePublica } from './api'

const v = (i: number) => ({ '--i': i }) as CSSProperties
const iniciales = (nombre: string) => nombre.split(/\s+/).filter((p) => p.length > 2).slice(0, 2).map((p) => p[0]).join('').toUpperCase()

export function PortalVacante() {
  const { codigo = '' } = useParams()
  const vacante = useVacantePublica(codigo)
  const miPortal = useMiPortal()
  const [copiado, setCopiado] = useState(false)

  if (vacante.isPending) {
    return (
      <main className="portal-ancho portal-detalle" aria-busy="true">
        <div className="portal-detalle-cabeza"><Esqueleto alto={56} ancho={56} /><div style={{ flex: 1, display: 'grid', gap: 10 }}><Esqueleto alto={14} ancho="30%" /><Esqueleto alto={34} ancho="70%" /></div></div>
        <div className="portal-detalle-columnas">
          <div className="portal-detalle-cuerpo"><Esqueleto alto={18} ancho="40%" /><Esqueleto alto={140} /><Esqueleto alto={18} ancho="30%" /><Esqueleto alto={90} /></div>
          <aside className="portal-detalle-lateral"><Esqueleto alto={260} /></aside>
        </div>
      </main>
    )
  }

  if (vacante.isError) {
    return (
      <main className="portal-ancho portal-detalle">
        <div className="portal-vacio">
          <h2>Esta vacante no existe</h2>
          <p>Puede que el enlace esté mal escrito o que la empresa la haya retirado.</p>
          <Link to="/empleos" className="boton boton-primario">Ver vacantes abiertas</Link>
        </div>
      </main>
    )
  }

  const x = vacante.data
  const compartir = async () => {
    const url = window.location.href
    try {
      if (navigator.share) await navigator.share({ title: x.titulo, text: `${x.titulo} · ${x.empresa}`, url })
      else await navigator.clipboard.writeText(url)
      setCopiado(true)
      setTimeout(() => setCopiado(false), 2400)
    } catch {
      // La persona cerró el diálogo de compartir: no hay nada que hacer
    }
  }

  // Con sesión del portal, si ya se postuló no se ofrece postularse otra vez
  const yaPostulado = miPortal.data?.postulaciones.some((p) => p.vacanteCodigo === x.codigo)
  const accion = yaPostulado ? (
    <Link to="/mi-portal" className="boton boton-secundario portal-postular">Ya te postulaste · ver en mi portal</Link>
  ) : x.recibePostulaciones ? (
    <Link to={`/empleos/${x.codigo}/postular`} className="boton boton-primario portal-postular portal-brillo">Postularme en 2 minutos <IconoFlecha /></Link>
  ) : (
    <p className="aviso aviso-alerta">Esta vacante ya cerró y no recibe postulaciones.</p>
  )

  return (
    <main className="portal-detalle">
      <div className="portal-detalle-banda">
        <div className="portal-ancho">
          <Link to="/empleos" className="portal-volver"><IconoFlecha />Todas las vacantes</Link>
          <div className="portal-detalle-cabeza">
            <span className="portal-avatar portal-avatar-grande" aria-hidden="true">{iniciales(x.empresa)}</span>
            <div>
              <span className="portal-tarjeta-empresa">{x.empresa}</span>
              <h1>{x.titulo}</h1>
              <ul className="portal-datos">
                <li><IconoUbicacion />{x.ciudad}</li>
                {x.jornada && <li><IconoReloj />{x.jornada}</li>}
                <li><IconoCupos />{x.cupos} {x.cupos === 1 ? 'cupo' : 'cupos'}</li>
                <li className="mono portal-codigo">{x.codigo}</li>
              </ul>
            </div>
          </div>
        </div>
      </div>

      <div className="portal-ancho portal-detalle-columnas">
        <article className="portal-detalle-cuerpo">
          <section className="portal-bloque" style={v(0)}>
            <h2>Sobre el trabajo</h2>
            <p className="portal-descripcion">{x.descripcion}</p>
          </section>
          {x.requisitos.length > 0 && (
            <section className="portal-bloque" style={v(1)}>
              <h2>Lo que buscamos</h2>
              <ul className="portal-requisitos">
                {x.requisitos.map((r, i) => <li key={r} style={v(i)}><span className="portal-requisito-check"><IconoCheck /></span>{r}</li>)}
              </ul>
            </section>
          )}
          {x.preguntasFiltro.length > 0 && (
            <section className="portal-bloque" style={v(2)}>
              <h2>Te preguntaremos</h2>
              <ul className="portal-preguntas">{x.preguntasFiltro.map((p) => <li key={p}>{p}</li>)}</ul>
            </section>
          )}
        </article>

        <aside className="portal-detalle-lateral">
          <div className="portal-lateral-tarjeta">
            <span className="portal-lateral-etiqueta">Salario</span>
            <span className="portal-lateral-salario"><IconoDinero /><span className="mono">{rangoSalario(x.salarioMinimo, x.salarioMaximo)}</span></span>
            {accion}
            <button type="button" className="boton boton-secundario portal-compartir" onClick={() => void compartir()}>
              {copiado ? <><IconoCheck />Enlace copiado</> : <><IconoCompartir />Compartir vacante</>}
            </button>
            <ol className="portal-despues">
              <li><b>Subes tu hoja de vida</b> y la leemos por ti.</li>
              <li><b>Confirmas tus datos</b> y respondes {x.preguntasFiltro.length || 'unas'} preguntas.</li>
              <li><b>La empresa te contacta</b> si sigues en el proceso.</li>
            </ol>
            <span className="portal-lateral-nota"><IconoEscudo />Tu hoja de vida se guarda cifrada y solo la ve {x.empresa}.</span>
          </div>
        </aside>
      </div>

      {x.recibePostulaciones && (
        <div className="portal-barra-movil">
          <span><b className="mono">{rangoSalario(x.salarioMinimo, x.salarioMaximo)}</b>{x.ciudad}</span>
          <Link to={`/empleos/${x.codigo}/postular`} className="boton boton-primario">Postularme</Link>
        </div>
      )}
    </main>
  )
}
