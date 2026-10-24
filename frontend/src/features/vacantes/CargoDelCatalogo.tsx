import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Boton } from '../../shared/ui/Boton'
import { useCrearCargo } from '../estructura/api'
import { usePuede } from '../sesion/api'
import { type CargoCatalogo, type Vacante, useCargosCatalogo } from './api'

const estadoPerfil: Record<string, { texto: string; clase: string }> = {
  Aprobado: { texto: 'Perfil aprobado', clase: 'pill-ok' },
  BorradorIa: { texto: 'Perfil en borrador IA', clase: 'pill-ia' },
  SinPerfil: { texto: 'Sin perfil', clase: 'pill-neutra' },
}

export function PillPerfil({ estado }: { estado: string }) {
  const e = estadoPerfil[estado] ?? estadoPerfil.SinPerfil
  return <span className={`pill ${e.clase}`}>{e.texto}</span>
}

/** Elegir de qué cargo del catálogo parte la vacante (opcional): la IA usa su perfil; sin cargo, redacta desde cero. */
export function ElegirCargo({ elegido, onElegir }: { elegido: CargoCatalogo | null; onElegir: (c: CargoCatalogo | null) => void }) {
  const [buscar, setBuscar] = useState('')
  const [abierto, setAbierto] = useState(false)
  const cargos = useCargosCatalogo(buscar, abierto)

  if (elegido) {
    return (
      <div className="cargo-elegido">
        <span>Parte del cargo <b>{elegido.nombre}</b> · {elegido.area}</span>
        <PillPerfil estado={elegido.estadoPerfil} />
        <button type="button" className="enlace-boton" onClick={() => onElegir(null)}>Quitar</button>
      </div>
    )
  }

  return (
    <div className="cargo-buscador">
      <input
        type="search"
        className="entrada"
        placeholder="Cargo del catálogo (opcional): auxiliar, cajero…"
        aria-label="Buscar cargo del catálogo"
        value={buscar}
        onFocus={() => setAbierto(true)}
        onBlur={() => setTimeout(() => setAbierto(false), 150)}
        onChange={(e) => setBuscar(e.target.value)}
      />
      {abierto && (
        <ul className="cargo-opciones" role="listbox">
          {cargos.isPending && <li className="cargo-vacio">Buscando…</li>}
          {cargos.data?.length === 0 && <li className="cargo-vacio">No hay cargos con ese nombre: la IA redacta la vacante desde cero.</li>}
          {cargos.data?.map((c) => (
            <li key={c.id}>
              <button type="button" role="option" aria-selected={false} onMouseDown={(e) => e.preventDefault()} onClick={() => { onElegir(c); setAbierto(false) }}>
                <span><b>{c.nombre}</b> · {c.area}{c.empleados > 0 && <span className="mono"> · {c.empleados} en nómina</span>}</span>
                <PillPerfil estado={c.estadoPerfil} />
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

/** En la vacante: de qué cargo del catálogo parte o, si se redactó desde cero, la opción de agregar el cargo al catálogo. */
export function CargoDeLaVacante({ vacante: v }: { vacante: Vacante }) {
  const puede = usePuede('ConfigurarEstructura')
  const crear = useCrearCargo()
  const [area, setArea] = useState('')
  const [agregando, setAgregando] = useState(false)
  const c = v.cargoCatalogo
  if (!v.cargo && !c) return null

  return (
    <section className="tarjeta requisicion" aria-label="Cargo del catálogo">
      <h3>Cargo</h3>
      {c?.vinculado && (
        <p className="cargo-elegido">La vacante parte del cargo <b>{c.nombre}</b> del catálogo <PillPerfil estado={c.estadoPerfil} /></p>
      )}
      {c && !c.vinculado && <p className="cargo-elegido">Redactada desde cero. El cargo <b>{c.nombre}</b> ya está en el catálogo <PillPerfil estado={c.estadoPerfil} /></p>}
      {!c && (
        <>
          <p className="requisicion-pedida">Redactada desde cero: «{v.cargo}» no está en el catálogo de cargos.</p>
          {puede && !agregando && !crear.isSuccess && <Boton pequeno onClick={() => setAgregando(true)}>Agregar «{v.cargo}» al catálogo</Boton>}
          {agregando && !crear.isSuccess && (
            <form className="formulario" onSubmit={(e) => { e.preventDefault(); crear.mutate({ nombre: v.cargo, area: area.trim() }) }}>
              <label>Área del cargo<input className="entrada" value={area} onChange={(e) => setArea(e.target.value)} placeholder="Logística" autoFocus /></label>
              <div className="decision" style={{ padding: 0, borderTop: 0 }}>
                <Boton type="submit" variante="primario" disabled={!area.trim()} cargando={crear.isPending} textoCargando="Agregando…">Agregar al catálogo</Boton>
                <Boton onClick={() => setAgregando(false)}>Cancelar</Boton>
              </div>
            </form>
          )}
          {crear.isSuccess && <p className="aviso aviso-ok">Quedó en el catálogo. La IA redacta su perfil y RRHH lo aprueba en la bandeja; <Link to="/consola/empresa/estructura">ver la estructura</Link>.</p>}
          {crear.error && <span role="alert" className="error-formulario">{crear.error.message}</span>}
        </>
      )}
    </section>
  )
}
