import { Fragment, useState } from 'react'
import { Boton } from '../../shared/ui/Boton'
import { Chispa, IconoAlerta, IconoCheck, IconoDocumento, IconoEscudo, IconoSubir } from '../../shared/ui/Iconos'
import { usePuede } from '../sesion/api'
import {
  abrirDocumento, type Documento, estadosDocumento, type Proceso, useAprobarDocumento, useAprobarSinObservaciones, useConceptoMedico, useRechazarDocumento,
  useSubirDocumento,
} from './api'

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const conceptos: [string, string][] = [['Apto', 'Apto'], ['AptoConRestricciones', 'Apto con restricciones'], ['NoApto', 'No apto']]

/** Documentos de ingreso: lo que subió el candidato (leído por la IA, lo aprueba RRHH) y lo que carga la empresa. */
export function DocumentosIngreso({ p }: { p: Proceso }) {
  const aprueba = usePuede('AprobarBandeja')
  const enBloque = useAprobarSinObservaciones(p.id)
  const docs = p.documentos
  const candidato = docs.filter((d) => !d.loCargaEmpresa)
  const empresa = docs.filter((d) => d.loCargaEmpresa)
  const limpios = docs.filter((d) => d.estado === 'PorRevisar' && d.lectura?.legible && d.lectura.tipoCorrecto && d.lectura.coincideTitular === true).length
  const obligatorios = docs.filter((d) => d.obligatorio)
  const aprobados = obligatorios.filter((d) => d.estado === 'Aprobado').length
  const editable = p.etapa === 'Documentos'
  const avance = obligatorios.length ? Math.round((aprobados / obligatorios.length) * 100) : 0

  return (
    <section className="tarjeta vin-panel" aria-label="Documentos de ingreso">
      <header>
        <div>
          <h2>Documentos de ingreso</h2>
          <p className="vin-sub">El candidato los sube desde el mismo enlace de la oferta.</p>
        </div>
        {aprueba && editable && limpios > 0 && (
          <Boton variante="primario" cargando={enBloque.isPending} textoCargando="Aprobando…" onClick={() => enBloque.mutate()}>
            Aprobar {limpios} sin observaciones
          </Boton>
        )}
      </header>
      <div className="vin-docs-avance">
        <span className="mono">{aprobados} de {obligatorios.length}</span> obligatorios aprobados
        <span className="vin-docs-barra" role="progressbar" aria-valuenow={avance} aria-valuemin={0} aria-valuemax={100} aria-label="Documentos obligatorios aprobados">
          <span style={{ width: `${avance}%` }} />
        </span>
      </div>
      {enBloque.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{enBloque.error.message}</p>}

      <h3 className="vin-docs-grupo">Del candidato <span className="mono">{candidato.length}</span></h3>
      <ul className="vin-docs">{candidato.map((d) => <FilaDocumento key={d.tipo} p={p} d={d} editable={editable} aprueba={aprueba} />)}</ul>
      <h3 className="vin-docs-grupo">De la empresa <span className="mono">{empresa.length}</span></h3>
      <ul className="vin-docs">{empresa.map((d) => <FilaEmpresa key={d.tipo} p={p} d={d} editable={editable} />)}</ul>
      <p className="ficha-nota"><IconoEscudo />Los archivos se guardan cifrados. El examen médico nunca pasa por la IA y su concepto solo lo ve quien tiene permiso de datos sensibles.</p>
    </section>
  )
}

function Subir({ p, d, etiqueta }: { p: Proceso; d: Documento; etiqueta: string }) {
  const subir = useSubirDocumento(p.id)
  return (
    <>
      <label className="boton boton-secundario boton-pequeno vin-subir" aria-busy={subir.isPending || undefined} title={`Subir ${d.nombre} por el candidato`}>
        {subir.isPending ? <span className="indicador" aria-hidden="true" /> : <IconoSubir />}
        {subir.isPending ? 'Subiendo…' : etiqueta}
        <input type="file" accept="application/pdf,image/jpeg,image/png,image/webp" hidden disabled={subir.isPending}
          onChange={(e) => { const f = e.target.files?.[0]; if (f) subir.mutate({ tipo: d.tipo, archivo: f }); e.target.value = '' }} />
      </label>
      {subir.error && <span role="alert" className="vin-error">{subir.error.message}</span>}
    </>
  )
}

function Nombre({ d }: { d: Documento }) {
  return (
    <span className="vin-doc-nombre">
      <b>{d.nombre}</b>{!d.obligatorio && <span className="vin-doc-opcional">opcional</span>}
    </span>
  )
}

function FilaDocumento({ p, d, editable, aprueba }: { p: Proceso; d: Documento; editable: boolean; aprueba: boolean }) {
  const aprobar = useAprobarDocumento(p.id)
  const rechazar = useRechazarDocumento(p.id)
  const [motivo, setMotivo] = useState<string | null>(null)
  const l = d.lectura
  const sugerencia = d.estado === 'PorRevisar' && l

  return (
    <li className={`vin-doc ${sugerencia ? 'vin-doc-ia' : ''} ${d.estado === 'Aprobado' ? 'vin-doc-listo' : ''}`}>
      <div className="vin-doc-fila">
        <span className="vin-doc-icono">{d.estado === 'Aprobado' ? <IconoCheck /> : <IconoDocumento />}</span>
        <div className="vin-doc-texto">
          <Nombre d={d} />
          <span className="vin-doc-meta">
            {d.estado === 'Leyendo' ? <><span className="orbe trabajando" /> El lector de documentos lo está leyendo…</>
              : d.nombreArchivo ? (
                <>
                  <button type="button" className="enlace-boton" onClick={() => void abrirDocumento(p.id, d.tipo)}>{d.nombreArchivo}</button>
                  {' '}· {d.estado === 'Aprobado' ? `aprobado por ${d.revisadoPor}` : `subido por ${d.subidoPor}${d.subidoEn ? ` · ${fecha.format(new Date(d.subidoEn))}` : ''}`}
                  {d.versiones > 1 ? ` · versión ${d.versiones}` : ''}
                </>
              ) : 'Aún no lo sube'}
          </span>
        </div>
        <span className={`pill ${estadosDocumento[d.estado].clase}`}>{estadosDocumento[d.estado].texto}</span>
        <div className="vin-doc-acciones">
          {editable && aprueba && d.estado === 'PorRevisar' && motivo === null && (
            <>
              <Boton pequeno onClick={() => setMotivo('')}>Rechazar</Boton>
              <Boton pequeno icono={<IconoCheck />} cargando={aprobar.isPending} textoCargando="Aprobando…" onClick={() => aprobar.mutate(d.tipo)}>Aprobar</Boton>
            </>
          )}
          {editable && d.estado !== 'Aprobado' && d.estado !== 'Leyendo' && <Subir p={p} d={d} etiqueta={d.nombreArchivo ? 'Reemplazar' : 'Subir'} />}
        </div>
      </div>

      {l && d.estado !== 'Aprobado' && (
        <div className="vin-doc-detalle vin-lectura">
          <span className="vin-etiqueta"><Chispa width={12} height={12} /> Lectura de la IA · {l.tipoDetectado || 'sin tipo'}</span>
          <ul className="vin-chequeos">
            <li className={l.tipoCorrecto ? 'ok' : 'mal'}>{l.tipoCorrecto ? '+' : '−'} {l.tipoCorrecto ? 'Es el documento pedido' : 'No parece el documento pedido'}</li>
            <li className={l.legible ? 'ok' : 'mal'}>{l.legible ? '+' : '−'} {l.legible ? 'Se lee bien' : 'No se lee bien'}</li>
            <li className={l.coincideTitular === true ? 'ok' : 'mal'}>{l.coincideTitular === true ? '+' : '−'} {l.coincideTitular === true ? 'Es del titular' : l.coincideTitular === false ? 'El número o el nombre NO coinciden con el candidato' : 'No trae número ni nombre para comparar'} <span className="cita">Reglas</span></li>
            {l.observaciones.map((o) => <li key={o} className="mal">− {o}</li>)}
          </ul>
          {l.campos.length > 0 && <dl className="vin-datos vin-campos">{l.campos.map((c) => <Fragment key={c.campo}><dt>{c.campo}</dt><dd className="mono">{c.valor}</dd></Fragment>)}</dl>}
        </div>
      )}
      {d.estado === 'Rechazado' && <p className="vin-doc-detalle vin-sub">Rechazado por {d.revisadoPor}: «{d.motivoRechazo}». El candidato lo ve en su enlace.</p>}
      {motivo !== null && (
        <form className="vin-doc-detalle vin-rechazo" onSubmit={(e) => { e.preventDefault(); rechazar.mutate({ tipo: d.tipo, motivo: motivo.trim() }, { onSuccess: () => setMotivo(null) }) }}>
          <input className="entrada" value={motivo} onChange={(e) => setMotivo(e.target.value)} placeholder="La foto salió borrosa: tómala con buena luz" autoFocus aria-label="Motivo del rechazo" />
          <Boton pequeno onClick={() => setMotivo(null)}>Cancelar</Boton>
          <Boton type="submit" pequeno disabled={!motivo.trim()} cargando={rechazar.isPending} textoCargando="Rechazando…">Rechazar</Boton>
        </form>
      )}
      {(aprobar.error ?? rechazar.error) && <p role="alert" className="vin-doc-detalle aviso aviso-error"><IconoAlerta />{(aprobar.error ?? rechazar.error)!.message}</p>}
    </li>
  )
}

function FilaEmpresa({ p, d, editable }: { p: Proceso; d: Documento; editable: boolean }) {
  const subir = useSubirDocumento(p.id)
  const verConcepto = useConceptoMedico(p.id)
  const sensibles = usePuede('VerDatosSensibles')
  const [resultado, setResultado] = useState(d.tipo === 'Antecedentes' ? 'SinAnotaciones' : 'Apto')
  const [restricciones, setRestricciones] = useState('')
  const [archivo, setArchivo] = useState<File | null>(null)
  const medico = d.tipo === 'ExamenMedico'
  const aprobado = d.estado === 'Aprobado'

  return (
    <li className={`vin-doc ${aprobado ? 'vin-doc-listo' : ''}`}>
      <div className="vin-doc-fila">
        <span className="vin-doc-icono">{aprobado ? <IconoCheck /> : <IconoDocumento />}</span>
        <div className="vin-doc-texto">
          <Nombre d={d} />
          <span className="vin-doc-meta">
            {aprobado ? (
              <>
                {medico && !sensibles ? d.nombreArchivo : <button type="button" className="enlace-boton" onClick={() => void abrirDocumento(p.id, d.tipo)}>{d.nombreArchivo}</button>}
                {' '}· registrado por {d.revisadoPor}{d.revisadoEn ? ` · ${fecha.format(new Date(d.revisadoEn))}` : ''}
              </>
            ) : medico ? 'Lo registra la empresa con el concepto del médico' : 'Lo registra la empresa con el resultado de la consulta'}
          </span>
        </div>
        <span className={`pill ${estadosDocumento[d.estado].clase}`}>{estadosDocumento[d.estado].texto}</span>
        <div className="vin-doc-acciones" />
      </div>

      {aprobado ? (
        <>
          {d.antecedentes && <p className={`vin-doc-detalle ${d.antecedentes === 'ConAnotaciones' ? 'vin-alerta-texto' : 'vin-sub'}`}>{d.antecedentes === 'ConAnotaciones' ? 'Con anotaciones: revise antes de crear el empleado' : 'Sin anotaciones'}</p>}
          {medico && (
            <p className={`vin-doc-detalle ${d.conceptoRequiereRevision ? 'vin-alerta-texto' : 'vin-sub'}`}>
              {d.conceptoRequiereRevision ? 'El concepto pide revisión (restricciones o no apto).' : 'Concepto sin restricciones.'}{' '}
              {sensibles && (verConcepto.data
                ? <b>{conceptos.find((c) => c[0] === verConcepto.data.concepto)?.[1]}{verConcepto.data.restricciones ? ` · ${verConcepto.data.restricciones}` : ''}</b>
                : <button type="button" className="enlace-boton" disabled={verConcepto.isPending} onClick={() => verConcepto.mutate()}>{verConcepto.isPending ? 'Abriendo…' : 'Ver concepto (dato sensible)'}</button>)}
            </p>
          )}
        </>
      ) : editable && (
        <form className="vin-doc-detalle vin-empresa" onSubmit={(e) => {
          e.preventDefault()
          if (archivo) subir.mutate(medico ? { tipo: d.tipo, archivo, concepto: resultado, restricciones: restricciones.trim() || null } : { tipo: d.tipo, archivo, antecedentes: resultado })
        }}>
          <label className={`vin-archivo ${archivo ? 'elegido' : ''}`}>
            <IconoSubir />
            <span>{archivo ? archivo.name : 'Elegir archivo (PDF o imagen)'}</span>
            <input type="file" accept="application/pdf,image/jpeg,image/png" hidden onChange={(e) => setArchivo(e.target.files?.[0] ?? null)} aria-label={`Archivo de ${d.nombre}`} />
          </label>
          <select className="selector" value={resultado} onChange={(e) => setResultado(e.target.value)} aria-label="Resultado">
            {(medico ? conceptos : [['SinAnotaciones', 'Sin anotaciones'], ['ConAnotaciones', 'Con anotaciones']]).map(([v, t]) => <option key={v} value={v}>{t}</option>)}
          </select>
          {medico && resultado !== 'Apto' && <input className="entrada" value={restricciones} onChange={(e) => setRestricciones(e.target.value)} placeholder="Restricciones (se guardan cifradas)" aria-label="Restricciones" />}
          <Boton type="submit" pequeno disabled={!archivo} cargando={subir.isPending} textoCargando="Registrando…">Registrar</Boton>
          {subir.error && <span role="alert" className="vin-error">{subir.error.message}</span>}
        </form>
      )}
    </li>
  )
}
