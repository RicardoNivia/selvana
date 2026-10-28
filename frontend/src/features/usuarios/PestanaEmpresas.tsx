import { type FormEvent, useState } from 'react'
import { AutocompletarMunicipio } from '../../shared/ui/AutocompletarMunicipio'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { IconoCheck, IconoUsuarios } from '../../shared/ui/Iconos'
import { iniciales } from '../sesion/api'
import { type EmpresaUsuaria, formatoNit, normalizarNit, useAgregarEmpresaUsuaria, useEditarEmpresaUsuaria, useEmpresasUsuarias } from './api'

/** Empresas usuarias de la EST: cada una es un cliente cuyos datos no se mezclan con los de otro. */
export function PestanaEmpresas() {
  const empresas = useEmpresasUsuarias()
  const [agregando, setAgregando] = useState(false)

  return (
    <section aria-label="Empresas usuarias" className="empresas-usuarias">
      <div className="rejilla-empresas">
        {empresas.isPending
          ? Array.from({ length: 4 }, (_, i) => (
              <div key={i} className="tarjeta tarjeta-empresa">
                <div className="tarjeta-empresa-cabeza"><Esqueleto alto={40} ancho={40} /><div style={{ flex: 1 }}><Esqueleto ancho="70%" /><Esqueleto ancho="40%" alto={11} style={{ marginTop: 6 }} /></div></div>
                <Esqueleto ancho="60%" /><Esqueleto ancho="45%" alto={20} redondo />
              </div>
            ))
          : empresas.data?.map((e) => <TarjetaEmpresa key={e.id} empresa={e} />)}

        {agregando ? <FormularioEmpresa onCerrar={() => setAgregando(false)} /> : (
          <button type="button" className="tarjeta tarjeta-agregar" onClick={() => setAgregando(true)}>
            <span>+</span>
            <b>Agregar empresa usuaria</b>
            <span className="ayuda">Con el NIT validamos el dígito de verificación de la DIAN.</span>
          </button>
        )}
      </div>
      {empresas.isError && <p role="alert" className="aviso aviso-alerta">{empresas.error.message}</p>}
    </section>
  )
}

function TarjetaEmpresa({ empresa: e }: { empresa: EmpresaUsuaria }) {
  const [editando, setEditando] = useState(false)
  if (editando) return <FormularioEmpresa empresa={e} onCerrar={() => setEditando(false)} />
  return (
    <article className={`tarjeta tarjeta-empresa ${e.activa ? '' : 'inactiva'}`}>
      <div className="tarjeta-empresa-cabeza">
        <span className="logo-empresa" aria-hidden="true">{iniciales(e.nombre)}</span>
        <div>
          <b>{e.nombre}</b>
          <span className="mono sub-celda">NIT {formatoNit(e.nit)}</span>
        </div>
      </div>
      <span className="sub-celda">{[e.sector, e.ciudad].filter(Boolean).join(' · ') || 'Sin sector ni ciudad'}</span>
      <span className="tarjeta-empresa-pie">
        <span className="pill pill-neutra"><IconoUsuarios />{e.usuarios} {e.usuarios === 1 ? 'persona con acceso' : 'personas con acceso'}</span>
        {!e.activa && <span className="pill pill-neutra">Inactiva</span>}
        <Boton pequeno onClick={() => setEditando(true)}>Editar</Boton>
      </span>
    </article>
  )
}

function FormularioEmpresa({ empresa, onCerrar }: { empresa?: EmpresaUsuaria; onCerrar: () => void }) {
  const agregar = useAgregarEmpresaUsuaria()
  const editar = useEditarEmpresaUsuaria(empresa?.id ?? '')
  const [nombre, setNombre] = useState(empresa?.nombre ?? '')
  const [nit, setNit] = useState(empresa ? formatoNit(empresa.nit) : '')
  const [sector, setSector] = useState(empresa?.sector ?? '')
  const [ciudad, setCiudad] = useState(empresa?.ciudad ?? '')
  // La ciudad se elige de la DIVIPOLA; la que ya estaba guardada cuenta como elegida
  const [ciudadElegida, setCiudadElegida] = useState(Boolean(empresa?.ciudad))
  const [activa, setActiva] = useState(empresa?.activa ?? true)
  const nitValido = normalizarNit(nit)
  const nitEscrito = nit.replace(/\D/g, '').length >= 9
  const mutacion = empresa ? editar : agregar

  const guardar = (ev: FormEvent) => {
    ev.preventDefault()
    if (ciudad.trim() && !ciudadElegida) return
    if (empresa) editar.mutate({ nombre, sector, ciudad, activa }, { onSuccess: onCerrar })
    else agregar.mutate({ nombre, nit: nitValido ?? nit, sector, ciudad }, { onSuccess: onCerrar })
  }

  return (
    <form className="tarjeta tarjeta-empresa formulario" onSubmit={guardar}>
      <b>{empresa ? 'Editar empresa usuaria' : 'Nueva empresa usuaria'}</b>
      <label>Razón social<input className="entrada" value={nombre} onChange={(e) => setNombre(e.target.value)} required autoFocus /></label>
      <label>NIT
        <input className="entrada mono" value={nit} onChange={(e) => setNit(e.target.value)} disabled={Boolean(empresa)} placeholder="900.412.887-9" inputMode="numeric" required />
        {!empresa && nitEscrito && (nitValido
          ? <span className="nota-ok"><IconoCheck />Dígito de verificación correcto: {formatoNit(nitValido)}</span>
          : <span className="error-formulario">El dígito de verificación no coincide. Revise el NIT en el RUT.</span>)}
        {empresa && <span className="ayuda">El NIT no cambia.</span>}
      </label>
      <div className="dos-campos">
        <label>Sector<input className="entrada" value={sector} onChange={(e) => setSector(e.target.value)} placeholder="BPO y contact center" /></label>
        <label>
          Ciudad o municipio
          <AutocompletarMunicipio valor={{ codigo: ciudadElegida ? 'elegida' : null, ciudad }}
            onElegir={(m, texto) => { setCiudad(m?.municipio ?? texto); setCiudadElegida(Boolean(m)) }} />
        </label>
      </div>
      {empresa && <label className="interruptor"><input type="checkbox" checked={activa} onChange={(e) => setActiva(e.target.checked)} />Activa (si se desactiva, nadie la ve en la consola)</label>}
      {mutacion.error && <span role="alert" className="error-formulario">{mutacion.error.message}</span>}
      <span className="enlace-acciones">
        <Boton type="submit" variante="primario" cargando={mutacion.isPending} textoCargando="Guardando…" disabled={!nombre.trim() || (!empresa && !nitValido)}>
          {empresa ? 'Guardar' : 'Agregar'}
        </Boton>
        <Boton onClick={onCerrar} disabled={mutacion.isPending}>Cancelar</Boton>
      </span>
    </form>
  )
}
