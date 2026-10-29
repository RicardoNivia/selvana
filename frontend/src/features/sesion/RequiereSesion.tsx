import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { useSesion } from './api'
import '../../app/riel.css'

/** La consola solo se ve con sesión; sin ella lleva al inicio de sesión y vuelve aquí después. */
export function RequiereSesion({ children }: { children: ReactNode }) {
  const sesion = useSesion()
  const ubicacion = useLocation()

  if (sesion.isPending) {
    return (
      <div className="consola" aria-busy="true">
        <nav className="riel" aria-label="Principal">
          <span className="riel-marca" />
          <div className="riel-menu riel-esqueleto">
            {Array.from({ length: 6 }, (_, i) => <span key={i} />)}
          </div>
        </nav>
        <main className="contenido">
          <Esqueleto alto={32} ancho="40%" />
          <Esqueleto alto={240} />
        </main>
      </div>
    )
  }

  if (sesion.isError) {
    return (
      <main className="ingresar-panel" style={{ minHeight: '100vh' }}>
        <div className="aviso aviso-alerta" role="alert">
          No pudimos conectar con Selvana. Revise que la API esté en marcha e intente de nuevo.
        </div>
      </main>
    )
  }

  if (!sesion.data) {
    return <Navigate to={`/ingresar?volver=${encodeURIComponent(ubicacion.pathname + ubicacion.search)}`} replace />
  }

  return children
}
