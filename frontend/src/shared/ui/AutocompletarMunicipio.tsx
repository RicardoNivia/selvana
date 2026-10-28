import { useQuery } from '@tanstack/react-query'
import { useEffect, useId, useState } from 'react'
import { apiGet } from '../api/client'

/** Municipio de la DIVIPOLA del DANE, con su subregión. */
export interface Municipio {
  codigo: string
  municipio: string
  codigoDepartamento: string
  departamento: string
  subregion: string | null
}

export function useMunicipios(buscar: string, activo = true) {
  return useQuery({
    queryKey: ['municipios', buscar],
    queryFn: ({ signal }) => apiGet<Municipio[]>(`/api/v1/core/municipios?buscar=${encodeURIComponent(buscar)}&tamano=8`, signal),
    enabled: activo && buscar.trim().length >= 2,
    staleTime: Infinity,
  })
}

const sinTildes = (t: string) => t.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase().trim()

/** Busca el municipio que corresponde a un texto libre (por ejemplo, la ciudad que escribió la IA); null si no hay uno claro. */
export async function resolverMunicipio(texto: string): Promise<Municipio | null> {
  const t = sinTildes(texto.split(',')[0] ?? '')
  if (t.length < 2) return null
  const opciones = await apiGet<Municipio[]>(`/api/v1/core/municipios?buscar=${encodeURIComponent(t)}&tamano=8`)
  const exactos = opciones.filter((m) => sinTildes(m.municipio) === t || sinTildes(m.municipio).replace(/, d\.c\.$/, '') === t)
  return exactos.length === 1 ? exactos[0] : null
}

/**
 * Campo de ciudad o municipio: se escribe y se elige de la DIVIPOLA (municipio · departamento · subregión · código DANE).
 * Si la IA propuso la ciudad, el campo va en violeta hasta que la persona elige.
 */
export function AutocompletarMunicipio({ valor, texto, onElegir, ia, disabled, requerido }: {
  valor: { codigo: string | null; ciudad: string }
  texto?: string
  onElegir: (m: Municipio | null, textoLibre: string) => void
  ia?: boolean
  disabled?: boolean
  requerido?: boolean
}) {
  const id = useId()
  const [escrito, setEscrito] = useState(valor.ciudad)
  const [abierto, setAbierto] = useState(false)
  const [buscar, setBuscar] = useState('')
  useEffect(() => setEscrito(valor.ciudad), [valor.ciudad])
  useEffect(() => {
    const t = setTimeout(() => setBuscar(escrito), 200)
    return () => clearTimeout(t)
  }, [escrito])
  const opciones = useMunicipios(buscar, abierto)
  const sinElegir = Boolean(valor.ciudad) && !valor.codigo

  return (
    <div className="cargo-buscador">
      <input
        className={`entrada${ia ? ' entrada-ia' : ''}`}
        role="combobox"
        aria-expanded={abierto}
        aria-controls={`${id}-opciones`}
        aria-label={texto ?? 'Ciudad o municipio'}
        placeholder="Escriba y elija: Funza, Medellín…"
        value={escrito}
        disabled={disabled}
        required={requerido}
        onFocus={() => setAbierto(true)}
        onBlur={() => setTimeout(() => setAbierto(false), 150)}
        onChange={(e) => { setEscrito(e.target.value); setAbierto(true); onElegir(null, e.target.value) }}
      />
      {sinElegir && !abierto && <span className="ayuda" style={{ color: 'var(--color-alerta)' }}>Elija el municipio de la lista para guardar su código DANE.</span>}
      {abierto && buscar.trim().length >= 2 && (
        <ul className="cargo-opciones" role="listbox" id={`${id}-opciones`}>
          {opciones.isPending && <li className="cargo-vacio">Buscando…</li>}
          {opciones.data?.length === 0 && <li className="cargo-vacio">No hay municipios con ese nombre.</li>}
          {opciones.data?.map((m) => (
            <li key={m.codigo}>
              <button type="button" role="option" aria-selected={m.codigo === valor.codigo} onMouseDown={(e) => e.preventDefault()}
                onClick={() => { setEscrito(m.municipio); setAbierto(false); onElegir(m, m.municipio) }}>
                <span><b>{m.municipio}</b> · {m.departamento}{m.subregion && m.subregion !== m.departamento ? ` · ${m.subregion}` : ''}</span>
                <span className="mono" style={{ fontSize: 12, color: 'var(--color-texto-tenue)' }}>{m.codigo}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
