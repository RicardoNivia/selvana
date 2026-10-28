import { useEffect } from 'react'
import { createPortal } from 'react-dom'
import { Boton } from '../../shared/ui/Boton'
import { IconoCerrar, IconoDocumento, Isotipo } from '../../shared/ui/Iconos'
import type { MiCuenta } from './api'

/**
 * Hoja de vida con la marca, armada con lo que la persona guardó en su perfil, para descargarla en PDF o imprimirla.
 * Sin foto, edad, estado civil ni dirección exacta: solo lo que sirve para evaluar el trabajo.
 */
export function HojaDeVidaPdf({ cuenta, onCerrar }: { cuenta: MiCuenta; onCerrar: () => void }) {
  const d = cuenta.datos
  const cursos = d.cursos.filter(Boolean)
  const habilidades = d.habilidades.filter(Boolean)

  useEffect(() => {
    document.body.classList.add('mp-con-cv')
    const tecla = (e: KeyboardEvent) => e.key === 'Escape' && onCerrar()
    // El papel siempre en claro: el modo oscuro imprimiría texto claro sobre hoja blanca
    const raiz = document.documentElement
    let temaAnterior: string | null = null
    const antes = () => { temaAnterior = raiz.getAttribute('data-theme'); raiz.setAttribute('data-theme', 'light') }
    const despues = () => (temaAnterior ? raiz.setAttribute('data-theme', temaAnterior) : raiz.removeAttribute('data-theme'))
    window.addEventListener('keydown', tecla)
    window.addEventListener('beforeprint', antes)
    window.addEventListener('afterprint', despues)
    return () => {
      document.body.classList.remove('mp-con-cv')
      window.removeEventListener('keydown', tecla)
      window.removeEventListener('beforeprint', antes)
      window.removeEventListener('afterprint', despues)
    }
  }, [onCerrar])

  return createPortal(
    <div className="mp-cv-fondo" role="dialog" aria-modal="true" aria-label="Tu hoja de vida en PDF" onClick={onCerrar}>
      <div className="mp-cv-marco" onClick={(e) => e.stopPropagation()}>
        <header className="mp-cv-barra">
          <span><IconoDocumento />Vista previa de tu hoja de vida</span>
          <Boton variante="primario" onClick={() => window.print()}>Descargar en PDF</Boton>
          <button type="button" className="mp-quitar" aria-label="Cerrar" onClick={onCerrar}><IconoCerrar /></button>
        </header>
        <p className="mp-cv-ayuda">En la ventana de impresión elige «Guardar como PDF».</p>

        <article className="mp-cv">
          <header className="mp-cv-cabeza">
            <div>
              <h1>{d.nombre}</h1>
              {d.experiencia[0]?.cargo && <p className="mp-cv-cargo">{d.experiencia[0].cargo}</p>}
            </div>
            <Isotipo width={28} height={28} />
          </header>
          <ul className="mp-cv-contacto">
            {d.celular && <li><small>Celular</small><span className="mono">{d.celular}</span></li>}
            <li><small>Correo</small><span>{cuenta.correo}</span></li>
            {d.ciudad && <li><small>Ciudad</small><span>{d.ciudad}</span></li>}
            {d.linkedIn && <li><small>LinkedIn</small><span>{d.linkedIn}</span></li>}
          </ul>

          {d.experiencia.length > 0 && (
            <section>
              <h2>Experiencia</h2>
              <ol className="mp-cv-linea">
                {d.experiencia.map((x, i) => (
                  <li key={i}><b>{x.cargo}</b><span>{x.empresa}</span><span className="mono">{[x.desde, x.hasta].filter(Boolean).join(' – ')}</span></li>
                ))}
              </ol>
            </section>
          )}
          {d.formacion.length > 0 && (
            <section>
              <h2>Estudios</h2>
              <ol className="mp-cv-linea">
                {d.formacion.map((x, i) => <li key={i}><b>{x.titulo}</b><span>{x.institucion}</span><span className="mono">{x.anio}</span></li>)}
              </ol>
            </section>
          )}
          {cursos.length > 0 && (
            <section><h2>Cursos y certificaciones</h2><ul className="mp-cv-lista">{cursos.map((x) => <li key={x}>{x}</li>)}</ul></section>
          )}
          {habilidades.length > 0 && (
            <section><h2>Habilidades</h2><ul className="mp-cv-chips">{habilidades.map((x) => <li key={x}>{x}</li>)}</ul></section>
          )}
          <footer className="mp-cv-pie">Hoja de vida generada en Selvana empleos.</footer>
        </article>
      </div>
    </div>,
    document.body,
  )
}
