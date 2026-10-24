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

  return (
    <section className="tarjeta vin-panel" aria-label="Documentos de ingreso">
      <header>
        <div>
          <h2>Documentos de ingreso</h2>
          <p className="vin-sub">{aprobados} de {obligatorios.length} obligatorios aprobados. El candidato los sube desde el mismo enlace de la oferta.</p>
        </div>
        {aprueba && editable && limpios > 0 && (
          <Boton variante="primario" cargando={enBloque.isPending} textoCargando="Aprobando…" onClick={() => enBloque.mutate()}>
            Aprobar {limpios} sin observaciones
          </Boton>
        )}
      </header>
      {enBloque.error && <p role="alert" className="aviso aviso-error"><IconoAlerta />{enBloque.error.message}</p>}

      <h3>Del candidato</h3>
      <div className="vin-docs">{candidato.map((d) => <FilaDocumento key={d.tipo} p={p} d={d} editable={editable} aprueba={aprueba} />)}</div>
      <h3>De la empresa</h3>
      <div className="vin-docs">{empresa.map((d) => <FilaEmpresa key={d.tipo} p={p} d={d} editable={editable} />)}</div>
      <p className="ficha-nota"><IconoEscudo />Los archivos se guardan cifrados. El examen médico nunca pasa por la IA y su concepto solo lo ve quien tiene permiso de datos sensibles.</p>
    </section>
  )
}

function Subir({ p, d, etiqueta }: { p: Proceso; d: Documento; etiqueta: string }) {
  const subir = useSubirDocumento(p.id)
  return (
    <label className="boton boton-secundario boton-pequeno vin-subir" aria-busy={subir.isPending || undefined}>
      {subir.isPending ? <span className="indicador" aria-hidden="true" /> : <IconoSubir />}
      {subir.isPending ? 'Subiendo…' : etiqueta}
      <input type="file" accept="application/pdf,image/jpeg,image/png,image/webp" hidden disabled={subir.isPending}
        onChange={(e) => { const f = e.target.files?.[0]; if (f) subir.mutate({ tipo: d.tipo, archivo: f }); e.target.value = '' }} />
      {subir.error && <span role="alert" className="vin-error">{subir.error.message}</span>}
    </label>
  )
}

function FilaDocumento({ p, d, editable, aprueba }: { p: Proceso; d: Documento; editable: boolean; aprueba: boolean }) {
  const aprobar = useAprobarDocumento(p.id)
  const rechazar = useRechazarDocumento(p.id)
  const [motivo, setMotivo] = useState<string | null>(null)
  const l = d.lectura
  const sugerencia = d.estado === 'PorRevisar' && l

  return (
    <article className={`vin-doc ${sugerencia ? 'vin-doc-ia' : ''}`}>
      <header>
        <IconoDocumento />
        <b>{d.nombre}</b>{!d.obligatorio && <span className="vin-sub">opcional</span>}
        <span className={`pill ${estadosDocumento[d.estado].clase}`}>{estadosDocumento[d.estado].texto}</span>
      </header>
      {d.nombreArchivo && (
        <p className="vin-sub">
          <button type="button" className="enlace-boton" onClick={() => void abrirDocumento(p.id, d.tipo)}>{d.nombreArchivo}</button>
          {' '}· subido por {d.subidoPor}{d.subidoEn ? ` · ${fecha.format(new Date(d.subidoEn))}` : ''}{d.versiones > 1 ? ` · versión ${d.versiones}` : ''}
        </p>
      )}
      {d.estado === 'Leyendo' && <p className="vin-sub"><span className="orbe trabajando" /> El lector de documentos lo está leyendo…</p>}
      {l && d.estado !== 'Aprobado' && (
        <div className="vin-lectura">
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
      {d.estado === 'Rechazado' && <p className="vin-sub">Rechazado por {d.revisadoPor}: «{d.motivoRechazo}». El candidato lo ve en su enlace.</p>}
      {d.estado === 'Aprobado' && <p className="vin-sub"><IconoCheck /> Aprobado por {d.revisadoPor}</p>}

      {editable && (
        <div className="vin-acciones">
          {aprueba && d.estado === 'PorRevisar' && (
            <>
              <Boton pequeno variante="primario" cargando={aprobar.isPending} textoCargando="Aprobando…" onClick={() => aprobar.mutate(d.tipo)}>Aprobar</Boton>
              {motivo === null && <Boton pequeno onClick={() => setMotivo('')}>Rechazar</Boton>}
            </>
          )}
          {d.estado !== 'Aprobado' && d.estado !== 'Leyendo' && <Subir p={p} d={d} etiqueta={d.nombreArchivo ? 'Subir otro' : 'Subir por el candidato'} />}
        </div>
      )}
      {motivo !== null && (
        <form className="vin-rechazo" onSubmit={(e) => { e.preventDefault(); rechazar.mutate({ tipo: d.tipo, motivo: motivo.trim() }, { onSuccess: () => setMotivo(null) }) }}>
          <input className="entrada" value={motivo} onChange={(e) => setMotivo(e.target.value)} placeholder="La foto salió borrosa: tómala con buena luz" autoFocus aria-label="Motivo del rechazo" />
          <Boton type="submit" pequeno disabled={!motivo.trim()} cargando={rechazar.isPending} textoCargando="Rechazando…">Rechazar</Boton>
          <Boton pequeno onClick={() => setMotivo(null)}>Cancelar</Boton>
        </form>
      )}
      {(aprobar.error ?? rechazar.error) && <p role="alert" className="aviso aviso-error"><IconoAlerta />{(aprobar.error ?? rechazar.error)!.message}</p>}
    </article>
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

  return (
    <article className="vin-doc">
      <header>
        <IconoDocumento /><b>{d.nombre}</b>
        <span className={`pill ${estadosDocumento[d.estado].clase}`}>{estadosDocumento[d.estado].texto}</span>
      </header>
      {d.estado === 'Aprobado' ? (
        <>
          <p className="vin-sub">
            {medico && !sensibles ? d.nombreArchivo : <button type="button" className="enlace-boton" onClick={() => void abrirDocumento(p.id, d.tipo)}>{d.nombreArchivo}</button>}
            {' '}· registrado por {d.revisadoPor}{d.revisadoEn ? ` · ${fecha.format(new Date(d.revisadoEn))}` : ''}
          </p>
          {d.antecedentes && <p className={d.antecedentes === 'ConAnotaciones' ? 'vin-alerta-texto' : 'vin-sub'}>{d.antecedentes === 'ConAnotaciones' ? 'Con anotaciones: revise antes de crear el empleado' : 'Sin anotaciones'}</p>}
          {medico && (
            <p className={d.conceptoRequiereRevision ? 'vin-alerta-texto' : 'vin-sub'}>
              {d.conceptoRequiereRevision ? 'El concepto pide revisión (restricciones o no apto).' : 'Concepto sin restricciones.'}{' '}
              {sensibles && (verConcepto.data
                ? <b>{conceptos.find((c) => c[0] === verConcepto.data.concepto)?.[1]}{verConcepto.data.restricciones ? ` · ${verConcepto.data.restricciones}` : ''}</b>
                : <button type="button" className="enlace-boton" disabled={verConcepto.isPending} onClick={() => verConcepto.mutate()}>{verConcepto.isPending ? 'Abriendo…' : 'Ver concepto (dato sensible)'}</button>)}
            </p>
          )}
        </>
      ) : editable ? (
        <form className="vin-empresa" onSubmit={(e) => {
          e.preventDefault()
          if (archivo) subir.mutate(medico ? { tipo: d.tipo, archivo, concepto: resultado, restricciones: restricciones.trim() || null } : { tipo: d.tipo, archivo, antecedentes: resultado })
        }}>
          <input type="file" accept="application/pdf,image/jpeg,image/png" onChange={(e) => setArchivo(e.target.files?.[0] ?? null)} aria-label={`Archivo de ${d.nombre}`} />
          <select className="selector" value={resultado} onChange={(e) => setResultado(e.target.value)} aria-label="Resultado">
            {(medico ? conceptos : [['SinAnotaciones', 'Sin anotaciones'], ['ConAnotaciones', 'Con anotaciones']]).map(([v, t]) => <option key={v} value={v}>{t}</option>)}
          </select>
          {medico && resultado !== 'Apto' && <input className="entrada" value={restricciones} onChange={(e) => setRestricciones(e.target.value)} placeholder="Restricciones (se guardan cifradas)" aria-label="Restricciones" />}
          <Boton type="submit" pequeno variante="primario" disabled={!archivo} cargando={subir.isPending} textoCargando="Registrando…">Registrar</Boton>
          {subir.error && <span role="alert" className="vin-error">{subir.error.message}</span>}
        </form>
      ) : <p className="vin-sub">Pendiente.</p>}
    </article>
  )
}
