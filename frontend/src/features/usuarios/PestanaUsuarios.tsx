import { type FormEvent, useCallback, useEffect, useState } from 'react'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Chispa, IconoCheck } from '../../shared/ui/Iconos'
import { Buscador, Paginacion } from '../../shared/ui/Paginacion'
import { usePaginacion } from '../../shared/ui/paginado'
import { useCargos } from '../estructura/api'
import { nombreRol, useSesion } from '../sesion/api'
import {
  type EmpresaUsuaria, type InvitacionEmitida, type Usuario, enlaceInvitacion, rolesInvitables,
  useCambiarUsuario, useInvitar, useRenovarInvitacion, useSugerenciaRol, useUsuarios,
} from './api'

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const filtrosRol: [string, string][] = [['', 'Todos'], ...rolesInvitables.map((r) => [r, nombreRol[r] ?? r] as [string, string])]

export function PestanaUsuarios({ temporal, empresas }: { temporal: boolean; empresas: EmpresaUsuaria[] }) {
  const { pagina, setPagina, tamano, setTamano } = usePaginacion()
  const [buscar, setBuscar] = useState('')
  const [rol, setRol] = useState('')
  const [editando, setEditando] = useState<string>()
  const datos = useUsuarios({ pagina, tamano, buscar, rol })
  const alBuscar = useCallback((t: string) => { setBuscar(t); setPagina(1) }, [setPagina])
  const activas = empresas.filter((e) => e.activa)

  return (
    <div className="usuarios-columnas">
      <section className="tarjeta" aria-label="Personas con acceso">
        <div className="barra-filtros">
          <Buscador etiqueta="Buscar nombre, correo o cargo" onBuscar={alBuscar} />
          <div className="segmentado" role="group" aria-label="Filtrar por rol">
            {filtrosRol.map(([v, t]) => (
              <button key={v || 'todos'} type="button" aria-pressed={rol === v} onClick={() => { setRol(v); setPagina(1) }}>{t}</button>
            ))}
          </div>
        </div>
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead>
              <tr>
                <th>Persona</th>
                <th>Rol</th>
                {temporal && <th>Empresas usuarias</th>}
                <th>Último ingreso</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {datos.isPending
                ? Array.from({ length: 5 }, (_, i) => (
                    <tr key={i}>
                      <td><Esqueleto ancho="70%" /><Esqueleto ancho="50%" alto={11} style={{ marginTop: 6 }} /></td>
                      <td><Esqueleto ancho={90} alto={20} redondo /></td>
                      {temporal && <td><Esqueleto ancho="60%" /></td>}
                      <td><Esqueleto ancho={100} /></td>
                      <td><Esqueleto ancho={60} alto={30} /></td>
                    </tr>
                  ))
                : datos.data?.items.map((u) =>
                    editando === u.id
                      ? <FilaEdicion key={u.id} usuario={u} temporal={temporal} empresas={activas} onCerrar={() => setEditando(undefined)} />
                      : <FilaUsuario key={u.id} usuario={u} temporal={temporal} onEditar={() => setEditando(u.id)} />,
                  )}
            </tbody>
          </table>
        </div>
        {datos.data && datos.data.total === 0 && <p className="vacio-tabla">No hay personas con ese filtro.</p>}
        {datos.data && <Paginacion pagina={datos.data} cargando={datos.isPlaceholderData} onCambiar={setPagina} onCambiarTamano={setTamano} />}
      </section>

      <PanelInvitar temporal={temporal} empresas={activas} />
    </div>
  )
}

function FilaUsuario({ usuario: u, temporal, onEditar }: { usuario: Usuario; temporal: boolean; onEditar: () => void }) {
  const renovar = useRenovarInvitacion()
  const [enlace, setEnlace] = useState<InvitacionEmitida>()
  return (
    <tr className={u.estado === 'Desactivado' ? 'inactivo' : ''}>
      <td>
        <b>{u.nombre}</b>
        <span className="sub-celda">{u.correo}{u.cargoNombre ? ` · ${u.cargoNombre}` : ''}</span>
        {enlace && <EnlaceInvitacion invitacion={enlace} compacto />}
        {renovar.error && <span role="alert" className="error-formulario">{renovar.error.message}</span>}
      </td>
      <td>
        <span className="pill pill-neutra">{nombreRol[u.rol] ?? u.rol}</span>
        {u.rolCorregido && <span className="sub-celda">Cambió el rol sugerido ({nombreRol[u.rolSugeridoIa ?? ''] ?? u.rolSugeridoIa})</span>}
      </td>
      {temporal && <td className="celda-empresas">{u.todasLasEmpresasUsuarias ? 'Todas' : u.empresasUsuarias.map((e) => e.nombre).join(', ') || '—'}</td>}
      <td>
        {u.estado === 'Desactivado' ? <span className="pill pill-neutra">Desactivado</span>
          : u.ultimoIngreso ? <span className="mono" style={{ fontSize: 13 }}>{fecha.format(new Date(u.ultimoIngreso))}</span>
          : <span className={`pill ${u.invitacionVigente ? 'pill-alerta' : 'pill-error'}`}>{u.invitacionVigente ? 'Invitación enviada' : 'Invitación vencida'}</span>}
      </td>
      <td className="acciones-fila">
        {!u.ultimoIngreso && u.estado !== 'Desactivado' && (
          <Boton pequeno cargando={renovar.isPending} textoCargando="Generando…" onClick={() => renovar.mutate(u.id, { onSuccess: setEnlace })}>Nuevo enlace</Boton>
        )}
        <Boton pequeno onClick={onEditar}>Editar</Boton>
      </td>
    </tr>
  )
}

function FilaEdicion({ usuario: u, temporal, empresas, onCerrar }: { usuario: Usuario; temporal: boolean; empresas: EmpresaUsuaria[]; onCerrar: () => void }) {
  const cambiar = useCambiarUsuario(u.id)
  const { data: sesion } = useSesion()
  const [nombre, setNombre] = useState(u.nombre)
  const [rol, setRol] = useState(u.rol)
  const [todas, setTodas] = useState(u.todasLasEmpresasUsuarias)
  const [elegidas, setElegidas] = useState(u.empresasUsuarias.map((e) => e.id))
  const [activo, setActivo] = useState(u.estado !== 'Desactivado')
  const esUsted = sesion?.usuarioId === u.id

  const guardar = (e: FormEvent) => {
    e.preventDefault()
    cambiar.mutate({ nombre, rol, todasLasEmpresasUsuarias: todas, empresasUsuarias: todas ? [] : elegidas, activo }, { onSuccess: onCerrar })
  }

  return (
    <tr className="fila-edicion">
      <td colSpan={temporal ? 5 : 4}>
        <form className="formulario" onSubmit={guardar}>
          <div className="dos-campos">
            <label>Nombre<input className="entrada" value={nombre} onChange={(e) => setNombre(e.target.value)} required /></label>
            <label>Rol
              <select className="entrada" value={rol} onChange={(e) => setRol(e.target.value)}>
                {rolesInvitables.map((r) => <option key={r} value={r}>{nombreRol[r]}</option>)}
              </select>
            </label>
          </div>
          {temporal && <SelectorEmpresas empresas={empresas} todas={todas} elegidas={elegidas} onTodas={setTodas} onElegidas={setElegidas} />}
          <label className="interruptor">
            <input type="checkbox" checked={activo} disabled={esUsted} onChange={(e) => setActivo(e.target.checked)} />
            Acceso activo {esUsted && <span className="ayuda">(no puede desactivar su propio acceso)</span>}
          </label>
          {cambiar.error && <span role="alert" className="error-formulario">{cambiar.error.message}</span>}
          <div className="decision" style={{ padding: '8px 0 0', borderTop: 0 }}>
            <Boton type="submit" variante="primario" icono={<IconoCheck />} cargando={cambiar.isPending} textoCargando="Guardando…">Guardar cambios</Boton>
            <Boton onClick={onCerrar} disabled={cambiar.isPending}>Cancelar</Boton>
            <span className="registro">Queda registrado quién lo cambió</span>
          </div>
        </form>
      </td>
    </tr>
  )
}

function SelectorEmpresas({ empresas, todas, elegidas, onTodas, onElegidas }: {
  empresas: EmpresaUsuaria[]; todas: boolean; elegidas: string[]; onTodas: (v: boolean) => void; onElegidas: (v: string[]) => void
}) {
  return (
    <fieldset className="selector-empresas">
      <legend>Empresas usuarias que verá</legend>
      <label className="interruptor"><input type="checkbox" checked={todas} onChange={(e) => onTodas(e.target.checked)} />Todas, también las que se creen después</label>
      {!todas && (
        <div className="selector-empresas-lista">
          {empresas.map((e) => {
            const on = elegidas.includes(e.id)
            return (
              <button key={e.id} type="button" className="chip-empresa" aria-pressed={on}
                onClick={() => onElegidas(on ? elegidas.filter((x) => x !== e.id) : [...elegidas, e.id])}>
                {on && <IconoCheck />}{e.nombre}
              </button>
            )
          })}
          {empresas.length === 0 && <span className="ayuda">Aún no hay empresas usuarias activas.</span>}
        </div>
      )}
      {!todas && elegidas.length === 0 && <span className="ayuda">Elija al menos una o marque «Todas».</span>}
    </fieldset>
  )
}

function PanelInvitar({ temporal, empresas }: { temporal: boolean; empresas: EmpresaUsuaria[] }) {
  const invitar = useInvitar()
  const [correo, setCorreo] = useState('')
  const [nombre, setNombre] = useState('')
  const [cargo, setCargo] = useState<{ id: string; nombre: string }>()
  const sugerencia = useSugerenciaRol(cargo?.id)
  const [rol, setRol] = useState<string>('')
  const [rolTocado, setRolTocado] = useState(false)
  const [todas, setTodas] = useState(!temporal)
  const [elegidas, setElegidas] = useState<string[]>([])
  const [emitida, setEmitida] = useState<InvitacionEmitida>()

  // Mientras la persona no elija un rol a mano, se usa el que sugiere Selvana para el cargo
  const sugerido = sugerencia.data?.rol
  const rolFinal = rolTocado ? rol : sugerido ?? rol
  const cambioSugerido = Boolean(sugerido && rolTocado && rol !== sugerido)

  const limpiar = () => {
    setCorreo(''); setNombre(''); setCargo(undefined); setRol(''); setRolTocado(false); setTodas(!temporal); setElegidas([])
  }

  const enviar = (e: FormEvent) => {
    e.preventDefault()
    invitar.mutate({
      correo: correo.trim(), nombre: nombre.trim(), rol: rolFinal, cargoId: cargo?.id, todasLasEmpresasUsuarias: todas, empresasUsuarias: todas ? [] : elegidas,
      rolSugerido: sugerido, razonSugerencia: sugerencia.data?.razon,
    }, { onSuccess: (r) => { setEmitida(r); limpiar() } })
  }

  const listo = correo.trim() && nombre.trim() && rolFinal && (todas || elegidas.length > 0)

  return (
    <aside className="tarjeta panel-invitar" aria-label="Invitar a una persona">
      <h2>Invitar a una persona</h2>
      {emitida && <EnlaceInvitacion invitacion={emitida} onCerrar={() => setEmitida(undefined)} />}
      <form className="formulario" onSubmit={enviar}>
        <label>Correo de trabajo<input className="entrada" type="email" value={correo} onChange={(e) => setCorreo(e.target.value)} placeholder="nombre@empresa.co" required /></label>
        <label>Nombre<input className="entrada" value={nombre} onChange={(e) => setNombre(e.target.value)} required /></label>
        <BuscadorCargo cargo={cargo} onElegir={(c) => { setCargo(c); setRolTocado(false) }} />

        <div className="campo-rol">
          <span className="etiqueta-campo">Rol</span>
          <div className="roles-invitar" role="radiogroup" aria-label="Rol">
            {rolesInvitables.map((r) => {
              const on = rolFinal === r
              const ia = on && r === sugerido && !rolTocado
              return (
                <button key={r} type="button" role="radio" aria-checked={on} className={`${on ? 'on' : ''} ${ia ? 'ia' : ''}`}
                  onClick={() => { setRol(r); setRolTocado(true) }}>
                  {ia && <Chispa />}{nombreRol[r]}
                </button>
              )
            })}
          </div>
          {sugerencia.isFetching && <span className="ayuda"><span className="indicador" aria-hidden="true" /> Buscando el rol para ese cargo…</span>}
          {sugerencia.data && !cambioSugerido && (
            <span className="nota-sugerencia">
              <Chispa />Sugerido: <b>{nombreRol[sugerencia.data.rol]}</b>. {sugerencia.data.razon} <span className="cita">{sugerencia.data.evidencia}</span>
            </span>
          )}
          {cambioSugerido && <span className="ayuda">Cambió el rol que sugirió Selvana ({nombreRol[sugerido!]}). Queda registrado.</span>}
        </div>

        {temporal && <SelectorEmpresas empresas={empresas} todas={todas} elegidas={elegidas} onTodas={setTodas} onElegidas={setElegidas} />}

        {invitar.error && <span role="alert" className="error-formulario">{invitar.error.message}</span>}
        <Boton type="submit" variante="primario" cargando={invitar.isPending} textoCargando="Invitando…" disabled={!listo}>Invitar</Boton>
        <span className="ayuda">Recibirá un enlace para crear su contraseña. Mientras conectamos el correo, cópielo y envíeselo usted.</span>
      </form>
    </aside>
  )
}

/** Busca el cargo en la estructura (la que llegó de la nómina) para sugerir el rol. */
function BuscadorCargo({ cargo, onElegir }: { cargo?: { id: string; nombre: string }; onElegir: (c?: { id: string; nombre: string }) => void }) {
  const [texto, setTexto] = useState('')
  const [buscar, setBuscar] = useState('')
  const [abierto, setAbierto] = useState(false)
  useEffect(() => {
    const t = setTimeout(() => setBuscar(texto.trim()), 250)
    return () => clearTimeout(t)
  }, [texto])
  const cargos = useCargos({ pagina: 1, buscar, estado: '' })
  const opciones = (cargos.data?.items ?? []).filter((c) => c.activo).slice(0, 6)

  if (cargo) {
    return (
      <div className="campo-cargo">
        <span className="etiqueta-campo">Cargo en la nómina</span>
        <span className="cargo-elegido"><b>{cargo.nombre}</b><button type="button" onClick={() => { onElegir(undefined); setTexto('') }}>Cambiar</button></span>
      </div>
    )
  }

  return (
    <label className="campo-cargo">
      Cargo en la nómina <span className="ayuda">opcional · con él Selvana sugiere el rol</span>
      <input className="entrada" value={texto} placeholder="Buscar cargo, p. ej. Jefe de bodega" role="combobox" aria-expanded={abierto && opciones.length > 0}
        onChange={(e) => { setTexto(e.target.value); setAbierto(true) }} onFocus={() => setAbierto(true)} onBlur={() => setTimeout(() => setAbierto(false), 150)} />
      {abierto && texto.trim() && (
        <ul className="opciones-cargo" role="listbox">
          {cargos.isFetching && opciones.length === 0 && <li className="ayuda">Buscando…</li>}
          {!cargos.isFetching && opciones.length === 0 && <li className="ayuda">No hay cargos con ese nombre en la estructura.</li>}
          {opciones.map((c) => (
            <li key={c.id} role="option" aria-selected={false}>
              <button type="button" onMouseDown={(e) => e.preventDefault()} onClick={() => { onElegir({ id: c.id, nombre: c.nombre }); setAbierto(false) }}>
                <b>{c.nombre}</b><span>{c.area} · {c.empleados} {c.empleados === 1 ? 'persona' : 'personas'}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </label>
  )
}

function EnlaceInvitacion({ invitacion, compacto, onCerrar }: { invitacion: InvitacionEmitida; compacto?: boolean; onCerrar?: () => void }) {
  const [copiado, setCopiado] = useState(false)
  const enlace = enlaceInvitacion(invitacion.token)
  const copiar = async () => {
    try {
      await navigator.clipboard.writeText(enlace)
      setCopiado(true)
    } catch {
      setCopiado(false)
    }
  }
  return (
    <div className={`enlace-invitacion ${compacto ? 'compacto' : ''}`} role="status">
      {!compacto && <b><IconoCheck />Invitación creada</b>}
      <span className="mono enlace-texto">{enlace}</span>
      <span className="enlace-acciones">
        <Boton pequeno variante="primario" onClick={() => void copiar()}>{copiado ? 'Copiado' : 'Copiar enlace'}</Boton>
        {onCerrar && <Boton pequeno onClick={onCerrar}>Listo</Boton>}
      </span>
      <span className="ayuda">Vence el {new Intl.DateTimeFormat('es-CO', { dateStyle: 'long' }).format(new Date(invitacion.vence))}. Solo se muestra esta vez.</span>
    </div>
  )
}
