import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoCandado } from '../../shared/ui/Iconos'
import { nombreRol } from '../sesion/api'
import { textoAlcance, useMatrizPermisos } from './api'

const nombreRolCompleto = (rol: string) => (rol === 'Candidato' ? 'Candidato' : nombreRol[rol] ?? rol)

/** Matriz rol × acción: la misma que usa la API para autorizar. Se lee sin conocimientos técnicos. */
export function PestanaPermisos() {
  const matriz = useMatrizPermisos()

  return (
    <section className="tarjeta" aria-label="Roles y permisos">
      <div className="barra-filtros">
        <b>Qué puede hacer cada rol</b>
        <span className="ayuda-suave">La API autoriza con esta misma tabla. «Su equipo», «Su vacante» y «Lo suyo» limitan hasta dónde llega el permiso.</span>
      </div>
      <div className="tabla-envoltura">
        <table className="tabla tabla-permisos">
          <thead>
            <tr>
              <th>Qué puede hacer</th>
              {(matriz.data?.roles ?? ['', '', '', '', '', '']).map((r, i) => <th key={r || i} className="centro">{r ? nombreRolCompleto(r) : <Esqueleto ancho={60} />}</th>)}
            </tr>
          </thead>
          <tbody>
            {matriz.isPending
              ? Array.from({ length: 8 }, (_, i) => (
                  <tr key={i}><td><Esqueleto ancho={`${55 + (i % 3) * 12}%`} /></td>{Array.from({ length: 6 }, (_, j) => <td key={j}><Esqueleto ancho={28} style={{ margin: '0 auto' }} /></td>)}</tr>
                ))
              : matriz.data?.permisos.map((p) => (
                  <tr key={p.accion} className={p.sensible ? 'fila-sensible' : ''}>
                    <td>
                      <b>{p.texto}</b>
                      {p.nota && <span className="nota-sensible"><IconoCandado />{p.nota}</span>}
                    </td>
                    {p.celdas.map((c) => (
                      <td key={c.rol} className={`centro alcance alcance-${c.alcance}`}>{textoAlcance[c.alcance] ?? c.alcance}</td>
                    ))}
                  </tr>
                ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}
