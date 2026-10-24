import { type FormEvent, useState } from 'react'
import { Link } from 'react-router-dom'
import { EsqueletoFilas } from '../../shared/ui/Esqueleto'
import { IconoAlerta, IconoBuscar } from '../../shared/ui/Iconos'
import { Paginacion } from '../../shared/ui/Paginacion'
import { usePaginacion } from '../../shared/ui/paginado'
import { duracion, useBancoTalento } from './api'

const ejemplos = ['montacarguistas en Funza', 'auxiliar de bodega con licencia C1', 'servicio al cliente en Bogotá', 'cajeras']
const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' })

/** Banco de talento: todas las personas que se han postulado, buscables en lenguaje natural. */
export function BancoTalento() {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const [texto, setTexto] = useState('')
  const [buscar, setBuscar] = useState('')
  const banco = useBancoTalento({ buscar, pagina, tamano })
  const buscarAhora = (t: string) => {
    setTexto(t)
    setBuscar(t.trim())
    setPagina(1)
  }

  return (
    <>
      <header className="encabezado">
        <h1>Candidatos</h1>
        <p>Su banco de talento: todas las personas que se han postulado. Búsquelas como lo diría en voz alta; la cédula completa solo se ve al abrir la hoja de vida.</p>
      </header>

      <form className="tarjeta banco-buscador" onSubmit={(e: FormEvent) => { e.preventDefault(); buscarAhora(texto) }}>
        <label className="banco-entrada">
          <IconoBuscar />
          <input type="search" value={texto} onChange={(e) => setTexto(e.target.value)} placeholder="Ej.: montacarguistas con licencia C1 en Mosquera" aria-label="Buscar en el banco de talento" />
        </label>
        <button type="submit" className="boton boton-primario">Buscar</button>
        <div className="banco-ejemplos">
          {ejemplos.map((x) => <button key={x} type="button" className="chip" onClick={() => buscarAhora(x)}>{x}</button>)}
        </div>
        {buscar && banco.data && (
          <p className="banco-entendido">
            Buscó: {banco.data.entendido.map((x) => <span key={x} className="pill pill-neutra">{x}</span>)}
            <button type="button" className="enlace-boton" onClick={() => buscarAhora('')}>Limpiar</button>
          </p>
        )}
      </form>

      <section className="tarjeta" style={{ padding: 0 }}>
        {banco.isPending ? <EsqueletoFilas filas={8} /> : banco.data && banco.data.resultados.total === 0 ? (
          <div className="vacio">
            <h2>{buscar ? 'Nadie coincide con esa búsqueda' : 'Todavía no hay candidatos'}</h2>
            <p>{buscar ? 'Pruebe con menos palabras o sin la ciudad. Busca en cargos, empresas, habilidades, cursos, estudios y ciudad; si nada coincide, busca por nombre.' : 'Cuando alguien se postule en el portal de empleo, aparecerá aquí.'}</p>
          </div>
        ) : (
          <div className="tabla-envoltura">
            <table className="tabla">
              <thead><tr><th>Candidato</th><th>Último cargo</th><th>Experiencia</th><th>Habilidades</th><th>Postulaciones</th></tr></thead>
              <tbody>
                {banco.data?.resultados.items.map((c) => (
                  <tr key={c.id}>
                    <td>
                      <Link to={`/consola/candidatos/${c.id}`} style={{ fontWeight: 600 }}>{c.nombre}</Link>
                      <span className="mono banco-sub">{c.documento} · {c.ciudad}</span>
                    </td>
                    <td>{c.ultimoCargo || '—'}</td>
                    <td>
                      {duracion(c.mesesExperiencia)}
                      {c.alertas > 0 && <span className="banco-alerta"><IconoAlerta />{c.alertas} {c.alertas === 1 ? 'alerta' : 'alertas'} de la HV</span>}
                    </td>
                    <td><span className="banco-habilidades">{c.habilidades.length ? c.habilidades.map((h) => <span key={h} className="pill pill-neutra">{h}</span>) : '—'}</span></td>
                    <td className="mono">{c.postulaciones}{c.ultimaPostulacion && <span className="banco-sub">{fecha.format(new Date(c.ultimaPostulacion))}</span>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {banco.data && banco.data.resultados.total > 0 && (
          <Paginacion pagina={banco.data.resultados} cargando={banco.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />
        )}
      </section>
    </>
  )
}
