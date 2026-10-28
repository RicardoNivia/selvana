import { useRef, useState } from 'react'
import { Boton } from '../../shared/ui/Boton'
import { IconoAlerta, IconoCheck, IconoDocumento, IconoSubir } from '../../shared/ui/Iconos'
import { descargarPlantillaPlanta, fechaCorta, pesos, useCargarPlanta, usePrevisualizarPlanta } from './api'

/**
 * Carga de la planta que ya existía: primero se revisa el archivo (no guarda nada) y luego una persona confirma.
 * Los que ya son empleados activos no se tocan; las filas con errores se reportan con su número.
 */
export function CargaPlanta({ onListo }: { onListo: () => void }) {
  const archivo = useRef<HTMLInputElement>(null)
  const [elegido, setElegido] = useState<File>()
  const revisar = usePrevisualizarPlanta()
  const cargar = useCargarPlanta()
  const p = revisar.data

  const elegir = (f?: File) => {
    if (!f) return
    setElegido(f)
    cargar.reset()
    revisar.mutate(f)
  }

  return (
    <section className="tarjeta emp-formulario emp-planta" aria-label="Cargar la planta">
      <header className="emp-planta-cabeza">
        <h2>Cargar la planta desde un archivo</h2>
        <button type="button" className="enlace-boton" onClick={descargarPlantillaPlanta}>Descargar la plantilla (CSV)</button>
      </header>
      <p className="ayuda">Obligatorios: documento, nombre, cargo, salario y fecha de ingreso. El resto (área, sede, contrato, afiliaciones, contacto) es opcional.
        Desde Excel: «Guardar como» → CSV. Primero verá qué se va a crear; nada se guarda hasta que confirme.</p>

      <div className="emp-zona" onDragOver={(e) => e.preventDefault()} onDrop={(e) => { e.preventDefault(); elegir(e.dataTransfer.files[0]) }}>
        <input ref={archivo} type="file" accept=".csv,text/csv" hidden onChange={(e) => { elegir(e.target.files?.[0]); e.target.value = '' }} />
        <span className="emp-documento-icono"><IconoDocumento /></span>
        <span className="emp-documento-texto"><b>{elegido?.name ?? 'Suelte aquí el archivo de la planta'}</b><span>CSV con coma o punto y coma · hasta 5.000 filas</span></span>
        <Boton icono={<IconoSubir />} cargando={revisar.isPending} textoCargando="Revisando…" onClick={() => archivo.current?.click()}>{elegido ? 'Otro archivo' : 'Elegir archivo'}</Boton>
      </div>

      {revisar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{revisar.error.message}</p>}

      {p && !cargar.data && (
        <>
          <div className="emp-cifras emp-planta-cifras">
            <div className="emp-cifra activa"><b>{p.porCrear}</b><span>se crean</span></div>
            <div className="emp-cifra"><b>{p.yaExisten}</b><span>ya existen (no se tocan)</span></div>
            <div className="emp-cifra"><b>{p.repetidas}</b><span>repetidas en el archivo</span></div>
            <div className="emp-cifra alerta"><b>{p.errores.length}</b><span>filas con errores</span></div>
          </div>
          {p.errores.length > 0 && (
            <ul className="emp-alertas">{p.errores.slice(0, 10).map((e) => <li key={e}><IconoAlerta /><span>{e}</span></li>)}
              {p.errores.length > 10 && <li><span>…y {p.errores.length - 10} más.</span></li>}</ul>
          )}
          {p.muestra.length > 0 && (
            <div className="tabla-envoltura">
              <table className="tabla">
                <thead><tr><th>Fila</th><th>Persona</th><th>Cargo</th><th>Contrato</th><th style={{ textAlign: 'right' }}>Salario</th><th>Ingreso</th><th>Resultado</th></tr></thead>
                <tbody>
                  {p.muestra.map((f) => (
                    <tr key={f.fila}>
                      <td className="mono">{f.fila}</td>
                      <td>{f.nombre}<span className="emp-sub mono">CC {f.documento}</span></td>
                      <td>{f.cargo}{f.sede && <span className="emp-sub">{f.sede}</span>}</td>
                      <td style={{ color: 'var(--color-texto-suave)' }}>{f.tipoContrato}</td>
                      <td className="num">{pesos.format(f.salarioCop)}</td>
                      <td className="mono" style={{ fontSize: 13 }}>{fechaCorta(f.fechaIngreso)}</td>
                      <td><span className={`pill ${f.resultado === 'Se crea' ? 'pill-ok' : 'pill-neutra'}`}>{f.resultado}</span></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {p.leidas > p.muestra.length && <span className="ayuda">Se muestran las primeras {p.muestra.length} de {p.leidas} filas leídas.</span>}
          {cargar.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{cargar.error.message}</p>}
          <div className="decision">
            <Boton variante="primario" disabled={p.porCrear === 0 || !elegido} cargando={cargar.isPending} textoCargando="Creando…" onClick={() => elegido && cargar.mutate(elegido)}>
              Crear {p.porCrear} {p.porCrear === 1 ? 'empleado' : 'empleados'}
            </Boton>
            <Boton onClick={onListo} disabled={cargar.isPending}>Cancelar</Boton>
            <span className="registro">Queda en la bitácora</span>
          </div>
        </>
      )}

      {cargar.data && (
        <>
          <p className="aviso aviso-ok"><IconoCheck />Creamos {cargar.data.creados} {cargar.data.creados === 1 ? 'empleado' : 'empleados'}
            {cargar.data.omitidos ? ` · ${cargar.data.omitidos} ya estaban` : ''}{cargar.data.conErrores ? ` · ${cargar.data.conErrores} filas con errores no se cargaron` : ''}.</p>
          <div className="vin-acciones"><Boton onClick={onListo}>Cerrar</Boton></div>
        </>
      )}
    </section>
  )
}
