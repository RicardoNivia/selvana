import { useQuery } from '@tanstack/react-query'
import { type CSSProperties, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiGet } from '../../shared/api/client'
import { IconoCerrar, IconoCheck, IconoFlecha } from '../../shared/ui/Iconos'

interface PrimerosPasosDatos {
  pasos: { clave: string; titulo: string; detalle: string; enlace: string; hecho: boolean }[]
  hechos: number
  diasDePrueba: number | null
}

const claveOculto = (empresa: string) => `selvana.primeros-pasos.${empresa}`
const leerOculto = (empresa: string) => { try { return localStorage.getItem(claveOculto(empresa)) === '1' } catch { return false } }

/**
 * Recorrido guiado de una empresa nueva (sprint 10): cinco pasos que se marcan solos según los datos.
 * Se muestra mientras falte alguno; quien quiera lo oculta (queda guardado en este navegador).
 */
export function PrimerosPasos({ empresaId }: { empresaId: string }) {
  const [oculto, setOculto] = useState(() => leerOculto(empresaId))
  const datos = useQuery({
    queryKey: ['primeros-pasos', empresaId],
    queryFn: ({ signal }) => apiGet<PrimerosPasosDatos>('/api/v1/core/primeros-pasos', signal),
    enabled: !oculto,
  })
  if (oculto || !datos.data || datos.data.hechos === datos.data.pasos.length) return null
  const d = datos.data
  const siguiente = d.pasos.find((p) => !p.hecho)
  const ocultar = () => { try { localStorage.setItem(claveOculto(empresaId), '1') } catch { /* sin almacenamiento: solo por esta visita */ } setOculto(true) }

  return (
    <section className="tarjeta primeros-pasos" aria-label="Primeros pasos">
      <header>
        <span className="primeros-avance" style={{ '--avance': d.hechos / d.pasos.length } as CSSProperties} aria-hidden="true"><b className="mono">{d.hechos}/{d.pasos.length}</b></span>
        <div>
          <h2>Primeros pasos en Selvana</h2>
          <p className="ayuda">{d.diasDePrueba !== null ? `Le quedan ${d.diasDePrueba} ${d.diasDePrueba === 1 ? 'día' : 'días'} de prueba gratuita. ` : ''}Cada paso se marca solo cuando lo hace.</p>
        </div>
        <button type="button" className="aviso-avance-cerrar" aria-label="Ocultar los primeros pasos" title="Ocultar" onClick={ocultar}><IconoCerrar /></button>
      </header>
      <ol>
        {d.pasos.map((p, i) => (
          <li key={p.clave} className={`${p.hecho ? 'hecho' : ''} ${p === siguiente ? 'siguiente' : ''}`} style={{ '--i': i } as CSSProperties}>
            <span className="primeros-marca" aria-label={p.hecho ? 'Hecho' : 'Pendiente'}>{p.hecho ? <IconoCheck /> : <span className="mono">{i + 1}</span>}</span>
            <div><b>{p.titulo}</b><span>{p.detalle}</span></div>
            {!p.hecho && <Link to={p.enlace} className={`boton boton-pequeno ${p === siguiente ? 'boton-primario' : 'boton-secundario'}`}>{p === siguiente ? 'Empezar' : 'Ir'} <IconoFlecha /></Link>}
          </li>
        ))}
      </ol>
    </section>
  )
}
