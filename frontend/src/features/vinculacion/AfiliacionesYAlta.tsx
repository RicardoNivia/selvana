import { useState } from 'react'
import { Boton } from '../../shared/ui/Boton'
import { Chispa, IconoAlerta, IconoCheck, IconoDocumento, IconoEscudo } from '../../shared/ui/Iconos'
import { usePuede } from '../sesion/api'
import { type Afiliacion, descargarAlta, type EstadoAfiliacion, fechaCorta, type Proceso, useActualizarAfiliacion, useCrearEmpleado, useEditarAlta } from './api'
import { AlertasContrato } from './CondicionesFormulario'

const estados: Record<EstadoAfiliacion, { texto: string; clase: string }> = {
  Pendiente: { texto: 'Pendiente', clase: 'pill-neutra' },
  Solicitada: { texto: 'Radicada', clase: 'pill-alerta' },
  Confirmada: { texto: 'Confirmada', clase: 'pill-ok' },
  NoAplica: { texto: 'No aplica', clase: 'pill-neutra' },
}
const romanos = ['', 'I', 'II', 'III', 'IV', 'V']

/** Afiliaciones (EPS, pensión, ARL, caja) y datos del alta; con todo listo, una persona crea el empleado. */
export function AfiliacionesYAlta({ p }: { p: Proceso }) {
  const aprueba = usePuede('AprobarBandeja')
  const crear = useCrearEmpleado(p.id)
  const [confirma, setConfirma] = useState(false)
  const [descargando, setDescargando] = useState(false)
  const editable = p.etapa === 'Afiliaciones'
  const creado = p.alta.empleadoCreadoEn !== null

  return (
    <div className="vin-alta">
      <section className="tarjeta vin-panel" aria-label="Afiliaciones">
        <header><h2>Afiliaciones</h2></header>
        {p.afiliaciones.length === 0 ? <p className="vin-sub">Se preparan cuando los documentos obligatorios estén aprobados.</p> : (
          <div className="vin-docs">{p.afiliaciones.map((a) => <FilaAfiliacion key={a.tipo} p={p} a={a} editable={editable} />)}</div>
        )}
      </section>

      <div className="vin-grilla vin-grilla-alta">
        <DatosDelAlta p={p} editable={editable} />
        <section className="tarjeta vin-panel" aria-label="Crear empleado">
          <header><h2>Crear el empleado</h2>{creado && <span className="pill pill-ok">Creado</span>}</header>
          {creado ? (
            <>
              <p className="aviso aviso-ok"><IconoCheck />Creado por {p.alta.empleadoCreadoPor}. El evento quedó listo para Empleados y el archivo de alta para la nómina.</p>
              <Boton icono={<IconoDocumento />} cargando={descargando} textoCargando="Descargando…"
                onClick={async () => { setDescargando(true); try { await descargarAlta(p.id, p.codigo) } finally { setDescargando(false) } }}>
                Descargar archivo de alta (CSV)
              </Boton>
              <p className="ficha-nota"><IconoEscudo />Formato canónico de Selvana: lo carga cualquier ERP. El conector directo con su nómina llega con las integraciones.</p>
            </>
          ) : (
            <>
              <AlertasContrato alertas={p.alta.alertas} />
              {p.alta.requiereConfirmarAlertas && (
                <label className="vin-confirmar">
                  <input type="checkbox" checked={confirma} onChange={(e) => setConfirma(e.target.checked)} />
                  Revisé las alertas de antecedentes o del examen médico y el cargo es compatible.
                </label>
              )}
              {crear.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{crear.error.message}</p>}
              {aprueba && editable && (
                <div className="decision">
                  <Boton variante="primario" disabled={!p.alta.listo || (p.alta.requiereConfirmarAlertas && !confirma)} cargando={crear.isPending} textoCargando="Creando…"
                    onClick={() => crear.mutate(confirma)}>Crear el empleado</Boton>
                  <span className="registro">Afecta a una persona: queda en la bitácora con su nombre</span>
                </div>
              )}
            </>
          )}
        </section>
      </div>
    </div>
  )
}

function FilaAfiliacion({ p, a, editable }: { p: Proceso; a: Afiliacion; editable: boolean }) {
  const guardar = useActualizarAfiliacion(p.id)
  const [v, setV] = useState({ estado: a.estado === 'Pendiente' ? 'Confirmada' as EstadoAfiliacion : a.estado, entidad: a.entidad, radicado: a.radicado ?? '',
    fechaEfectiva: a.fechaEfectiva ?? (a.tipo === 'Arl' ? diaAnterior(p.condiciones.fechaIngreso) : p.condiciones.fechaIngreso), claseRiesgo: a.claseRiesgo })
  const [editando, setEditando] = useState(false)
  const sugerida = a.sugeridaDe !== null && a.estado === 'Pendiente'
  const aprendiz = p.condiciones.tipo === 'Aprendizaje'

  return (
    <article className={`vin-doc ${sugerida ? 'vin-doc-ia' : ''}`}>
      <header><b>{a.nombre}</b><span className={`pill ${estados[a.estado].clase}`}>{estados[a.estado].texto}</span></header>
      {!editando ? (
        <>
          <p className={sugerida ? 'vin-sugerido' : ''}>
            {sugerida && <Chispa width={12} height={12} />} {a.entidad || <span className="vin-sub">Sin entidad</span>}
            {a.tipo === 'Arl' && a.claseRiesgo && <> · clase <b className="mono">{romanos[a.claseRiesgo]}</b></>}
          </p>
          {sugerida && <span className="vin-sub">{a.tipo === 'Arl' ? `Clase sugerida: ${p.alta.claseRiesgoRazon}` : `Leído del ${a.sugeridaDe?.toLowerCase()}`}</span>}
          {a.fechaEfectiva && a.estado !== 'Pendiente' && <span className="vin-sub">Desde el {fechaCorta(a.fechaEfectiva)}{a.radicado ? ` · radicado ${a.radicado}` : ''}{a.por ? ` · ${a.por}` : ''}</span>}
          {editable && <div className="vin-acciones"><Boton pequeno onClick={() => setEditando(true)}>{a.estado === 'Pendiente' ? 'Registrar' : 'Cambiar'}</Boton></div>}
        </>
      ) : (
        <form className="vin-afiliacion" onSubmit={(e) => {
          e.preventDefault()
          guardar.mutate({ tipo: a.tipo, estado: v.estado, entidad: v.entidad.trim() || null, radicado: v.radicado.trim() || null, fechaEfectiva: v.fechaEfectiva || null,
            claseRiesgo: a.tipo === 'Arl' ? v.claseRiesgo : null }, { onSuccess: () => setEditando(false) })
        }}>
          <label>Estado
            <select className="selector" value={v.estado} onChange={(e) => setV({ ...v, estado: e.target.value as EstadoAfiliacion })}>
              <option value="Solicitada">Radicada (espera confirmación)</option>
              <option value="Confirmada">Confirmada</option>
              {a.tipo === 'Pension' && aprendiz && <option value="NoAplica">No aplica</option>}
            </select>
          </label>
          {v.estado !== 'NoAplica' && (
            <>
              <label>Entidad<input className="entrada" value={v.entidad} onChange={(e) => setV({ ...v, entidad: e.target.value })} placeholder={a.tipo === 'Eps' ? 'Sura EPS' : a.tipo === 'Arl' ? 'Positiva' : ''} /></label>
              <label>Cubre desde<input className="entrada" type="date" value={v.fechaEfectiva} onChange={(e) => setV({ ...v, fechaEfectiva: e.target.value })} /></label>
              <label>Radicado<input className="entrada mono" value={v.radicado} onChange={(e) => setV({ ...v, radicado: e.target.value })} placeholder="Opcional" /></label>
              {a.tipo === 'Arl' && (
                <label>Clase de riesgo
                  <select className="selector" value={v.claseRiesgo ?? ''} onChange={(e) => setV({ ...v, claseRiesgo: Number(e.target.value) || null })}>
                    {[1, 2, 3, 4, 5].map((n) => <option key={n} value={n}>Clase {romanos[n]}</option>)}
                  </select>
                </label>
              )}
            </>
          )}
          <div className="vin-acciones">
            <Boton type="submit" pequeno variante="primario" cargando={guardar.isPending} textoCargando="Guardando…">Guardar</Boton>
            <Boton pequeno onClick={() => setEditando(false)}>Cancelar</Boton>
          </div>
          {guardar.error && <span role="alert" className="vin-error">{guardar.error.message}</span>}
        </form>
      )}
    </article>
  )
}

function diaAnterior(iso: string) {
  const d = new Date(`${iso.slice(0, 10)}T12:00:00`)
  d.setDate(d.getDate() - 1)
  return d.toISOString().slice(0, 10)
}

function DatosDelAlta({ p, editable }: { p: Proceso; editable: boolean }) {
  const guardar = useEditarAlta(p.id)
  const a = p.alta
  const [v, setV] = useState({ area: a.area, centroCosto: a.centroCosto, sede: a.sede, banco: a.banco, tipoCuenta: a.tipoCuenta, numeroCuenta: a.numeroCuenta })
  const sugerido = (campo: string) => a.sugeridosPorIa.includes(campo)
  const campo = (clave: keyof typeof v, etiqueta: string, ia?: string, mono = false) => (
    <label className={ia && sugerido(ia) ? 'vin-campo-ia' : ''}>
      {etiqueta}{ia && sugerido(ia) && <span className="vin-sub"><Chispa width={11} height={11} /> leído del certificado</span>}
      <input className={`entrada ${mono ? 'mono' : ''}`} value={v[clave]} disabled={!editable} onChange={(e) => setV({ ...v, [clave]: e.target.value })} />
    </label>
  )

  return (
    <section className="tarjeta vin-panel" aria-label="Datos del alta">
      <header><h2>Datos del alta</h2>{a.editadoPor && <span className="vin-sub">Confirmados por {a.editadoPor}</span>}</header>
      <div className="vin-condiciones">
        {campo('area', 'Área')}
        {campo('centroCosto', 'Centro de costo')}
        {campo('sede', 'Sede')}
        {campo('banco', 'Banco', 'banco')}
        {campo('tipoCuenta', 'Tipo de cuenta', 'tipo de cuenta')}
        {campo('numeroCuenta', 'Número de cuenta', 'número de cuenta', true)}
      </div>
      {guardar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{guardar.error.message}</p>}
      {editable && (
        <div className="vin-acciones">
          <Boton variante="primario" cargando={guardar.isPending} textoCargando="Guardando…" onClick={() => guardar.mutate(v)}>Confirmar datos del alta</Boton>
          <span className="vin-sub">Lo que leyó la IA queda confirmado con su nombre.</span>
        </div>
      )}
    </section>
  )
}
