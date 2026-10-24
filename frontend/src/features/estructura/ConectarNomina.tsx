import { useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../../shared/api/client'
import { Boton } from '../../shared/ui/Boton'
import { Esqueleto, EsqueletoCifras } from '../../shared/ui/Esqueleto'
import { Chispa, IconoAlerta, IconoCheck, IconoSubir } from '../../shared/ui/Iconos'
import { type Importacion, type Propuesta, useAplicarImportacion, useCambiarPropuesta, useImportacionActual, useImportarNomina } from './api'

const conexiones = [
  { titulo: 'SINCO', texto: 'Conector nativo: lee empleados, contratos, cargos y novedades cada noche.', tag: 'Faltan credenciales', clase: 'pill-neutra', activa: false },
  { titulo: 'API REST', texto: 'Su ERP envía los datos por la API abierta de Selvana (OpenAPI) y webhooks.', tag: 'Pronto', clase: 'pill-neutra', activa: false },
  { titulo: 'Archivo programado', texto: 'Un CSV que su ERP deja cada día en una carpeta SFTP.', tag: 'Pronto', clase: 'pill-neutra', activa: false },
  { titulo: 'Carga manual', texto: 'Suba un CSV de empleados activos; Selvana reconoce las columnas.', tag: 'Disponible', clase: 'pill-ok', activa: true },
]

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const numero = new Intl.NumberFormat('es-CO')

export function ConectarNomina() {
  const importacion = useImportacionActual()
  const importar = useImportarNomina()
  const archivo = useRef<HTMLInputElement>(null)
  const [origen, setOrigen] = useState<'archivo' | 'ejemplo'>()

  const subir = (f: File | undefined, desde: 'archivo' | 'ejemplo') => {
    if (!f) return
    setOrigen(desde)
    importar.mutate(f)
  }
  const usarEjemplo = async () => {
    setOrigen('ejemplo')
    const respuesta = await fetch('/ejemplos/nomina-ejemplo.csv')
    subir(new File([await respuesta.blob()], 'nomina-ejemplo.csv', { type: 'text/csv' }), 'ejemplo')
  }

  return (
    <>
      <header className="encabezado">
        <h1>Conectar nómina</h1>
        <p>Selvana no reemplaza su nómina: la lee. Elija cómo conectarse; la IA organiza lo que traiga y usted aprueba antes de que cambie algo.</p>
      </header>

      <section className="opciones-conexion" aria-label="Tipo de conexión">
        {conexiones.map((c) => (
          <div key={c.titulo} className={`opcion${c.activa ? ' activa' : ''}`}>
            <span className="titulo">
              <b>{c.titulo}</b>
              <span className={`pill ${c.clase}`}>{c.tag}</span>
            </span>
            <p>{c.texto}</p>
          </div>
        ))}
      </section>

      <section className="zona-archivo" aria-label="Subir nómina">
        <p>
          Columnas: <span className="mono">documento; cargo_codigo; cargo; area; centro_costo_codigo; centro_costo; sede</span>. No se necesitan
          nombres: la estructura no guarda datos personales.
        </p>
        <input ref={archivo} type="file" accept=".csv,text/csv" hidden onChange={(e) => subir(e.target.files?.[0], 'archivo')} />
        <Boton variante="primario" icono={<IconoSubir />} cargando={importar.isPending && origen === 'archivo'} textoCargando="Leyendo…"
          disabled={importar.isPending} onClick={() => archivo.current?.click()}>
          Subir archivo
        </Boton>
        <Boton cargando={importar.isPending && origen === 'ejemplo'} textoCargando="Leyendo…" disabled={importar.isPending} onClick={() => void usarEjemplo()}>
          Usar nómina de ejemplo
        </Boton>
      </section>

      {importar.error && (
        <div role="alert" className="aviso aviso-error">
          <IconoAlerta />
          {importar.error instanceof ApiError ? importar.error.message : 'No se pudo leer el archivo. Intente de nuevo.'}
        </div>
      )}

      {importacion.isPending && (
        <>
          <Esqueleto alto={44} />
          <EsqueletoCifras />
          <div className="tarjeta" style={{ padding: 18, display: 'flex', flexDirection: 'column', gap: 14 }}>
            <Esqueleto alto={18} ancho="45%" />
            {[0, 1, 2, 3].map((i) => <Esqueleto key={i} alto={40} />)}
            <Esqueleto alto={44} ancho={220} />
          </div>
        </>
      )}
      {importacion.isError && (
        <div role="alert" className="aviso aviso-error">
          <IconoAlerta />
          No hay conexión con la API. Revise que esté corriendo en el puerto 5000.
        </div>
      )}
      {importacion.data === null && (
        <div className="tarjeta vacio">
          <h2>Todavía no hay nómina</h2>
          <p>Suba el archivo de empleados activos o use la nómina de ejemplo para ver cómo la IA organiza la estructura.</p>
        </div>
      )}
      {importacion.data && <Revision importacion={importacion.data} />}
    </>
  )
}

function Revision({ importacion }: { importacion: Importacion }) {
  const cambiar = useCambiarPropuesta(importacion.id)
  const aplicar = useAplicarImportacion(importacion.id)
  const { conteos } = importacion
  const organizando = importacion.estado === 'OrganizandoConIa'
  const aplicada = importacion.estado === 'Aplicada'
  const incluidas = importacion.propuestas.filter((p) => p.incluida).length
  const repetidos = conteos.cargos - conteos.cargosTrasUnir

  return (
    <>
      <div className="aviso aviso-ok" style={{ background: 'var(--color-superficie)', border: '1px solid var(--color-borde)', fontWeight: 400, fontSize: 13 }}>
        <IconoCheck />
        <span>
          <b>{importacion.fuente}</b> · leída el <span className="mono">{fecha.format(new Date(importacion.leidaEn))}</span> ·{' '}
          <span className="mono">{numero.format(conteos.empleados)}</span> empleados
        </span>
      </div>

      <section className="rejilla" aria-label="Lo que se leyó">
        <Cifra nombre="Sedes" valor={String(conteos.sedes)} nota={conteos.nombresSedes.join(', ')} />
        <Cifra nombre="Áreas" valor={String(conteos.areas)} nota="Sin cambios" />
        <Cifra nombre="Centros de costo" valor={String(conteos.centrosCosto)} nota="Leídos de la nómina" />
        <Cifra
          nombre="Cargos"
          valor={repetidos > 0 ? `${conteos.cargos} → ${conteos.cargosTrasUnir}` : String(conteos.cargos)}
          nota={repetidos > 0 ? `${repetidos} nombres repetidos` : 'Sin repetidos'}
          clase={repetidos > 0 ? 'ia' : ''}
        />
        <Cifra
          nombre="Empleados"
          valor={numero.format(conteos.empleados)}
          nota={importacion.errores.length > 0 ? `${importacion.errores.length} con error` : 'Sin errores'}
          clase={importacion.errores.length > 0 ? 'alerta' : ''}
        />
      </section>

      {organizando ? (
        <div className="estado-agente" role="status">
          <span className="orbe trabajando" />
          <div>
            <b>El agente organizador está trabajando</b>
            <div style={{ fontSize: 13, color: 'var(--color-texto-suave)' }}>Revisa {conteos.cargos} nombres de cargo para encontrar los que son el mismo cargo.</div>
          </div>
          <span className="pill pill-ia" style={{ marginLeft: 'auto' }}>IA</span>
        </div>
      ) : (
        <section className="bloque-ia panel-decision" aria-label="Propuestas antes de aplicar">
          <div className="bloque-ia-cabeza">
            <Chispa />
            {aplicada ? 'Así quedó la estructura con lo que usted aprobó' : 'La IA ordenó lo que trajo la nómina. Revise antes de aplicar.'}
            <span className="derecha">
              {incluidas} de {importacion.propuestas.length} incluidas{importacion.modeloIa ? ` · ${importacion.modeloIa}` : ''}
            </span>
          </div>

          {importacion.mensajeIa && (
            <div className="aviso aviso-alerta" style={{ borderRadius: 0 }}>
              <IconoAlerta />
              {importacion.mensajeIa}
            </div>
          )}
          {importacion.propuestas.length === 0 && !importacion.mensajeIa && (
            <div className="fila"><div className="cuerpo"><span>No hay nada que unir ni corregir: la nómina viene ordenada.</span></div></div>
          )}
          {importacion.propuestas.map((p) => (
            <FilaPropuesta
              key={p.id}
              propuesta={p}
              bloqueada={aplicada || cambiar.isPending}
              cargando={cambiar.isPending && cambiar.variables?.propuestaId === p.id}
              onCambiar={() => cambiar.mutate({ propuestaId: p.id, incluida: !p.incluida })}
            />
          ))}

          {importacion.errores.map((e) => (
            <div key={e} role="note" className="aviso aviso-alerta" style={{ borderRadius: 0, borderTop: '1px solid var(--color-borde)' }}>
              <IconoAlerta />
              {e}
            </div>
          ))}

          {aplicada ? (
            <div role="status" className="aviso aviso-ok" style={{ borderRadius: 0 }}>
              <IconoCheck />
              Estructura aplicada por {importacion.aplicadaPor} el {importacion.aplicadaEn && fecha.format(new Date(importacion.aplicadaEn))}. La IA está redactando el perfil de cada cargo.
              <Link to="/consola/empresa/estructura" style={{ marginLeft: 'auto' }}>Ver estructura y cargos</Link>
            </div>
          ) : (
            <div className="decision">
              <Boton variante="primario" icono={<IconoCheck />} cargando={aplicar.isPending} textoCargando="Aplicando…" onClick={() => aplicar.mutate()}>
                {`Aplicar estructura (${incluidas} cambios)`}
              </Boton>
              {aplicar.error && <span role="alert" style={{ color: 'var(--color-error)', fontSize: 13 }}>{aplicar.error.message}</span>}
              <span className="registro">Se registran la sugerencia y tu decisión</span>
            </div>
          )}
        </section>
      )}
    </>
  )
}

function Cifra({ nombre, valor, nota, clase = '' }: { nombre: string; valor: string; nota: string; clase?: string }) {
  return (
    <div className="cifra">
      <span>{nombre}</span>
      <b>{valor}</b>
      <small className={clase}>{nota}</small>
    </div>
  )
}

function FilaPropuesta({ propuesta, bloqueada, cargando, onCambiar }: { propuesta: Propuesta; bloqueada: boolean; cargando: boolean; onCambiar: () => void }) {
  return (
    <div className={`fila${propuesta.incluida ? '' : ' excluida'}`}>
      <div className="cuerpo">
        <b>{propuesta.titulo}</b>
        <span>{propuesta.porque}</span>
      </div>
      <span className={`pill ${propuesta.origen === 'Ia' ? 'pill-ia' : 'pill-neutra'}`}>
        {propuesta.origen === 'Ia' ? <><Chispa />IA</> : 'Regla'}
      </span>
      <span className="cita">{propuesta.evidencia}</span>
      <Boton
        pequeno
        aria-pressed={propuesta.incluida}
        disabled={bloqueada}
        cargando={cargando}
        textoCargando="Guardando…"
        onClick={onCambiar}
        style={propuesta.incluida ? { borderColor: 'var(--color-primario)', color: 'var(--color-primario)' } : undefined}
      >
        {propuesta.incluida ? 'Incluida' : 'Excluida'}
      </Boton>
    </div>
  )
}
