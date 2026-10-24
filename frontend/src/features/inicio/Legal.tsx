import { Link, Navigate, useParams } from 'react-router-dom'
import { IconoAlerta, IconoEscudo } from '../../shared/ui/Iconos'
import { LogoAnimado } from '../../shared/ui/LogoAnimado'
import { actualizado, documentos } from './documentosLegales'
import { PieLegal } from './PieLegal'

/** Páginas legales públicas (/legal/:documento). */
export function Legal() {
  const { documento = '' } = useParams()
  const d = documentos[documento]
  if (!d) return <Navigate to="/legal/tratamiento-de-datos" replace />

  return (
    <div className="inicio legal">
      <header className="legal-cabecera">
        <div className="inicio-ancho legal-cabecera-fila">
          <Link to="/" aria-label="Selvana, inicio"><LogoAnimado tamano={28} palabra /></Link>
          <Link to="/ingresar" className="boton boton-secundario boton-pequeno">Ingresar</Link>
        </div>
      </header>
      <main className="inicio-ancho legal-cuerpo">
        <nav className="legal-indice" aria-label="Documentos legales">
          {Object.entries(documentos).map(([clave, x]) => (
            <Link key={clave} to={`/legal/${clave}`} aria-current={clave === documento ? 'page' : undefined}>{x.titulo}</Link>
          ))}
        </nav>
        <article className="legal-documento">
          <h1>{d.titulo}</h1>
          <p className="legal-resumen">{d.resumen}</p>
          <p className="aviso aviso-alerta"><IconoAlerta />Versión preliminar del {actualizado}, pendiente de revisión jurídica antes de la operación comercial.</p>
          {d.secciones.map((s) => (
            <section key={s.titulo}>
              <h2>{s.titulo}</h2>
              {s.parrafos.map((p) => <p key={p}>{p}</p>)}
            </section>
          ))}
          <p className="legal-nota"><IconoEscudo />Última actualización: {actualizado}.</p>
        </article>
      </main>
      <PieLegal />
    </div>
  )
}
