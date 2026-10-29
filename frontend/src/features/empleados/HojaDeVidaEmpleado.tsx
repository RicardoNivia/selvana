import { useMutation, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { apiArchivo, apiGet } from '../../shared/api/client'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoAlerta, IconoCandado, IconoDocumento } from '../../shared/ui/Iconos'
import { AbrirHojaDeVida } from '../vacantes/AbrirHojaDeVida'

interface HojaDeVida {
  fuente: 'Postulación' | 'Portal'
  nombre: string
  ciudad: string
  linkedIn: string | null
  experiencia: { cargo: string; empresa: string; desde: string; hasta: string }[]
  formacion: { titulo: string; institucion: string; anio: string }[]
  cursos: string[]
  habilidades: string[]
  postulacionId: string | null
  vacante: string | null
  nombreArchivo: string | null
  fecha: string | null
}

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' })

/**
 * La hoja de vida del empleado: la de su postulación en esta empresa o la de su portal, si la compartió (Ley 1581).
 * Ver los datos no abre el archivo; abrirlo pide el motivo y queda en la bitácora.
 */
export function HojaDeVidaEmpleado({ empleadoId, irACarpeta }: { empleadoId: string; irACarpeta: () => void }) {
  const hoja = useQuery({
    queryKey: ['empleados', 'hoja-de-vida', empleadoId],
    queryFn: ({ signal }) => apiGet<{ hoja: HojaDeVida | null }>(`/api/v1/expediente/empleados/${empleadoId}/hoja-de-vida`, signal),
  })

  if (hoja.isPending) return <div className="emp-resumen"><Esqueleto alto={140} /><Esqueleto alto={200} /></div>
  if (hoja.isError) return <p role="alert" className="aviso aviso-error"><IconoAlerta />{hoja.error.message}</p>

  const h = hoja.data.hoja
  if (!h) {
    return (
      <section className="tarjeta emp-bloque">
        <h2>Aún no hay hoja de vida</h2>
        <p className="ayuda">Esta persona no llegó por una vacante de la empresa y no ha compartido la hoja de vida de su portal. Puede pedirle que la comparta desde
          «Mi empleo» en su portal, o subirla usted a la carpeta con el tipo «Hoja de vida».</p>
        <div><Boton onClick={irACarpeta}>Ir a la carpeta</Boton></div>
      </section>
    )
  }

  return (
    <div className="emp-resumen">
      <section className="tarjeta emp-bloque">
        <header>
          <h2>{h.nombre}</h2>
          <span className="pill pill-neutra">{h.fuente === 'Postulación' ? `De su postulación${h.vacante ? ` a «${h.vacante}»` : ''}` : 'Compartida desde su portal'}</span>
        </header>
        <p className="ayuda">
          {h.ciudad || 'Sin ciudad'}{h.fecha && <> · actualizada el <span className="mono">{fecha.format(new Date(h.fecha))}</span></>}
          {h.linkedIn && <> · <a href={h.linkedIn} target="_blank" rel="noreferrer">Perfil de LinkedIn</a></>}
        </p>
        <div className="hv-archivo">
          <IconoDocumento />
          {h.fuente === 'Postulación' && h.postulacionId
            ? <><span>Archivo de la hoja de vida</span><AbrirHojaDeVida postulacionId={h.postulacionId} /></>
            : h.nombreArchivo ? <><span>{h.nombreArchivo}</span><AbrirCompartida empleadoId={empleadoId} /></> : <span className="ayuda">Sin archivo: la escribió en su perfil.</span>}
        </div>
      </section>

      <div className="emp-grilla">
        <section className="tarjeta emp-bloque">
          <h2>Experiencia</h2>
          {h.experiencia.length === 0 ? <p className="ayuda">Sin experiencia registrada.</p> : (
            <ol className="hv-lista">
              {h.experiencia.map((x, i) => <li key={i}><b>{x.cargo}</b><span>{x.empresa}</span><span className="mono">{x.desde} – {x.hasta}</span></li>)}
            </ol>
          )}
        </section>
        <section className="tarjeta emp-bloque">
          <h2>Formación</h2>
          {h.formacion.length === 0 ? <p className="ayuda">Sin estudios registrados.</p> : (
            <ol className="hv-lista">{h.formacion.map((x, i) => <li key={i}><b>{x.titulo}</b><span>{x.institucion}</span>{x.anio && <span className="mono">{x.anio}</span>}</li>)}</ol>
          )}
          {h.cursos.length > 0 && <><h2>Cursos</h2><ul className="hv-chips">{h.cursos.map((c) => <li key={c}>{c}</li>)}</ul></>}
          {h.habilidades.length > 0 && <><h2>Habilidades</h2><ul className="hv-chips">{h.habilidades.map((c) => <li key={c}>{c}</li>)}</ul></>}
        </section>
      </div>
    </div>
  )
}

const motivos = ['Gestión del expediente', 'Plan de carrera o vacante interna', 'Verificar datos del empleado', 'Respuesta a una solicitud del titular']

/** Abre el archivo que el empleado compartió desde su portal; pide el motivo y queda en la bitácora. */
function AbrirCompartida({ empleadoId }: { empleadoId: string }) {
  const [abierto, setAbierto] = useState(false)
  const [motivo, setMotivo] = useState(motivos[0]!)
  const abrir = useMutation({
    mutationFn: async (ventana: Window | null) => {
      try {
        const archivo = await apiArchivo(`/api/v1/expediente/empleados/${empleadoId}/hoja-de-vida/abrir`, { motivo })
        const url = URL.createObjectURL(archivo)
        if (ventana) ventana.location.href = url
        setTimeout(() => URL.revokeObjectURL(url), 60_000)
      } catch (e) {
        ventana?.close()
        throw e
      }
    },
    onSuccess: () => setAbierto(false),
  })

  if (!abierto) return <Boton pequeno icono={<IconoCandado />} onClick={() => setAbierto(true)}>Ver</Boton>
  return (
    <div className="abrir-hv">
      <select className="entrada" value={motivo} onChange={(e) => setMotivo(e.target.value)} aria-label="Motivo para abrir la hoja de vida">
        {motivos.map((m) => <option key={m}>{m}</option>)}
      </select>
      <span className="abrir-hv-acciones">
        <Boton pequeno variante="primario" cargando={abrir.isPending} textoCargando="Abriendo…" onClick={() => abrir.mutate(window.open('', '_blank'))}>Abrir</Boton>
        <Boton pequeno onClick={() => setAbierto(false)} disabled={abrir.isPending}>Cancelar</Boton>
      </span>
      <span className="ayuda-suave">Queda en la bitácora con su nombre y el motivo.</span>
      {abrir.error && <span role="alert" className="error-formulario">{abrir.error.message}</span>}
    </div>
  )
}
