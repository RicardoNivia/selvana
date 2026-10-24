import { useState } from 'react'
import { useSesion } from '../sesion/api'
import { useEmpresasUsuarias, useUsuarios } from './api'
import { PestanaEmpresas } from './PestanaEmpresas'
import { PestanaPermisos } from './PestanaPermisos'
import { PestanaUsuarios } from './PestanaUsuarios'

type Pestana = 'usuarios' | 'permisos' | 'empresas'

/** Pantalla 8 · Usuarios y roles (historias 185 y 186). */
export function UsuariosRoles() {
  const { data: sesion } = useSesion()
  const [pestana, setPestana] = useState<Pestana>('usuarios')
  const usuarios = useUsuarios({ pagina: 1, buscar: '', rol: '' })
  const empresas = useEmpresasUsuarias()
  const temporal = Boolean(sesion?.empresaTemporal)

  const pestanas: [Pestana, string][] = [
    ['usuarios', `Usuarios${usuarios.data ? ` · ${usuarios.data.total}` : ''}`],
    ['permisos', 'Roles y permisos'],
    ...(temporal ? [['empresas', `Empresas usuarias${empresas.data ? ` · ${empresas.data.length}` : ''}`] as [Pestana, string]] : []),
  ]

  return (
    <>
      <header className="encabezado">
        <h1>Usuarios y roles</h1>
        <p>
          {temporal
            ? 'Cada persona ve solo las empresas usuarias que tiene asignadas. Los datos de una nunca aparecen en otra.'
            : 'Dé a cada persona el acceso que necesita para su trabajo, sin entrar en permisos técnicos.'}
        </p>
      </header>

      <div role="tablist" aria-label="Usuarios y roles" className="pestanas">
        {pestanas.map(([clave, texto]) => (
          <button key={clave} type="button" role="tab" className="pestana" aria-selected={pestana === clave} onClick={() => setPestana(clave)}>
            {texto}
          </button>
        ))}
      </div>

      {pestana === 'usuarios' && <PestanaUsuarios temporal={temporal} empresas={empresas.data ?? []} />}
      {pestana === 'permisos' && <PestanaPermisos />}
      {pestana === 'empresas' && temporal && <PestanaEmpresas />}
    </>
  )
}
