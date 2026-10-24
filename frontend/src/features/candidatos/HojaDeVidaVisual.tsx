import type { ReactNode } from 'react'
import { IconoAlerta, IconoBirrete, IconoHerramienta, IconoIdioma, IconoInsignia, IconoManos, IconoVacantes } from '../../shared/ui/Iconos'
import { type CandidatoDetalle, type GrupoHabilidad, duracion } from './api'

const tituloAlerta: Record<string, string> = {
  FechasSuperpuestas: 'Fechas que se cruzan', Hueco: 'Hueco en la experiencia', FechaImposible: 'Fecha imposible',
  SinFechas: 'Experiencia sin fechas', SinExperiencia: 'Sin experiencia',
}

const conjuntos: Record<GrupoHabilidad, { titulo: string; ayuda: string; icono: ReactNode }> = {
  Oficio: { titulo: 'Del oficio', ayuda: 'Lo que sabe hacer en el trabajo', icono: <IconoHerramienta /> },
  Herramientas: { titulo: 'Herramientas y software', ayuda: 'Programas y sistemas que maneja', icono: <IconoVacantes /> },
  Certificaciones: { titulo: 'Licencias, cursos y certificaciones', ayuda: 'Verifique la vigencia antes de vincular', icono: <IconoInsignia /> },
  Idiomas: { titulo: 'Idiomas', ayuda: 'Nivel declarado por el candidato', icono: <IconoIdioma /> },
  Blandas: { titulo: 'Habilidades blandas', ayuda: 'Las declaró el candidato: confírmelas en la preselección o en las pruebas', icono: <IconoManos /> },
}
const orden: GrupoHabilidad[] = ['Oficio', 'Certificaciones', 'Herramientas', 'Idiomas', 'Blandas']

function Bloque({ icono, titulo, cuenta, children }: { icono: ReactNode; titulo: string; cuenta?: string; children: ReactNode }) {
  return (
    <section className="hv-bloque">
      <header className="hv-bloque-cabeza">
        <span className="hv-icono" aria-hidden="true">{icono}</span>
        <h3>{titulo}</h3>
        {cuenta && <span className="mono hv-cuenta">{cuenta}</span>}
      </header>
      {children}
    </section>
  )
}

/** La hoja de vida por bloques: resumen, alertas, experiencia, formación y conjuntos de habilidades. */
export function HojaDeVidaVisual({ c }: { c: CandidatoDetalle }) {
  const actual = c.experiencia.find((e) => e.actual)
  const totalHabilidades = c.conjuntosHabilidades.reduce((n, g) => n + g.habilidades.length, 0)
  const grupos = orden.map((g) => c.conjuntosHabilidades.find((x) => x.grupo === g)).filter((g) => g !== undefined)

  return (
    <div className="hv">
      <div className="hv-resumen">
        <div className="hv-cifra"><span>Experiencia</span><b>{c.mesesExperiencia > 0 ? duracion(c.mesesExperiencia) : '—'}</b></div>
        <div className="hv-cifra"><span>{actual ? 'Trabaja hoy como' : 'Último cargo'}</span><b className="hv-cifra-texto">{(actual ?? c.experiencia[0])?.cargo ?? '—'}</b></div>
        <div className="hv-cifra"><span>Empleos</span><b>{c.experiencia.length}</b></div>
        <div className="hv-cifra"><span>Habilidades</span><b>{totalHabilidades}</b><small>en {grupos.length} {grupos.length === 1 ? 'conjunto' : 'conjuntos'}</small></div>
      </div>

      {c.alertas.length > 0 && (
        <section className="hv-alertas" aria-label="Para revisar en la hoja de vida">
          <header><IconoAlerta /><b>Para revisar con el candidato</b><span className="mono hv-cuenta">{c.alertas.length}</span></header>
          <ul>
            {c.alertas.map((a) => (
              <li key={a.texto}><b>{tituloAlerta[a.tipo] ?? a.tipo}.</b> {a.texto} <span className="cita">{a.evidencia}</span></li>
            ))}
          </ul>
          <small>Son avisos para confirmar, no motivos de descarte.</small>
        </section>
      )}

      <Bloque icono={<IconoVacantes />} titulo="Experiencia" cuenta={c.experiencia.length ? String(c.experiencia.length) : undefined}>
        {c.experiencia.length === 0 ? <p className="hv-vacio">No registra experiencia laboral.</p> : (
          <ol className="hv-empleos">
            {c.experiencia.map((e, i) => (
              <li key={i} className={`hv-empleo ${e.actual ? 'actual' : ''}`}>
                <span className="hv-empleo-punto" aria-hidden="true" />
                <div className="hv-empleo-cuerpo">
                  <div className="hv-empleo-titulo">
                    <b>{e.cargo || 'Cargo sin nombre'}</b>
                    {e.actual && <span className="pill pill-ok">Actual</span>}
                  </div>
                  <span className="hv-empresa">{e.empresa || 'Empresa no indicada'}</span>
                  <div className="hv-empleo-meta">
                    <span className="mono">{e.desde || '¿?'} – {e.hasta || '¿?'}</span>
                    {e.meses !== null && <span className="hv-duracion">{duracion(e.meses)}</span>}
                    {e.cargoNormalizado !== e.cargo && <span className="pill pill-neutra" title="Cargo normalizado contra el catálogo de Selvana">{e.cargoNormalizado}</span>}
                  </div>
                </div>
              </li>
            ))}
          </ol>
        )}
      </Bloque>

      <Bloque icono={<IconoBirrete />} titulo="Formación" cuenta={c.formacion.length ? String(c.formacion.length) : undefined}>
        {c.formacion.length === 0 ? <p className="hv-vacio">No registra estudios.</p> : (
          <div className="hv-estudios">
            {c.formacion.map((f, i) => (
              <div key={i} className="hv-estudio">
                <b>{f.titulo}</b>
                <span>{f.institucion || 'Institución no indicada'}</span>
                {f.anio && <span className="mono">{f.anio}</span>}
              </div>
            ))}
          </div>
        )}
      </Bloque>

      <Bloque icono={<IconoHerramienta />} titulo="Habilidades" cuenta={totalHabilidades ? String(totalHabilidades) : undefined}>
        {grupos.length === 0 ? <p className="hv-vacio">La hoja de vida no menciona habilidades.</p> : (
          <div className="hv-conjuntos">
            {grupos.map((g) => {
              const info = conjuntos[g.grupo]
              return (
                <div key={g.grupo} className={`hv-conjunto ${g.grupo === 'Blandas' ? 'blandas' : ''}`}>
                  <header>
                    <span className="hv-icono hv-icono-pequeno" aria-hidden="true">{info.icono}</span>
                    <b>{info.titulo}</b>
                    <span className="mono hv-cuenta">{g.habilidades.length}</span>
                  </header>
                  <div className="hv-chips">
                    {g.habilidades.map((h) => (
                      <span key={h.nombre} className="hv-chip" title={h.menciona !== h.nombre ? `En la HV: «${h.menciona}»` : undefined}>
                        {h.nombre}
                        {g.grupo === 'Blandas' && h.menciona.toLowerCase() !== h.nombre.toLowerCase() && <span className="hv-menciona">«{h.menciona}»</span>}
                      </span>
                    ))}
                  </div>
                  <small>{info.ayuda}</small>
                </div>
              )
            })}
          </div>
        )}
        <small className="hv-nota">Normalizadas contra el catálogo de Selvana: «bodeguero» y «auxiliar de bodega» cuentan igual. Edad, género, estado civil y foto no se extraen.</small>
      </Bloque>
    </div>
  )
}
