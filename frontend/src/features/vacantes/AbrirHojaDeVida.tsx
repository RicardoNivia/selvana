import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { apiArchivo } from '../../shared/api/client'
import { Boton } from '../../shared/ui/Boton'
import { IconoCandado } from '../../shared/ui/Iconos'
import { usePuede } from '../sesion/api'

const motivos = ['Revisión de la postulación', 'Preselección de la terna', 'Verificar datos del candidato', 'Respuesta a una solicitud del titular']

/**
 * Abre la hoja de vida en otra pestaña. Antes pide el motivo: cada apertura queda en la bitácora.
 * La pestaña se abre en el mismo clic (si no, el navegador la bloquea) y el archivo llega después.
 */
export function AbrirHojaDeVida({ postulacionId }: { postulacionId: string }) {
  const puede = usePuede('VerHojasDeVida')
  const [abierto, setAbierto] = useState(false)
  const [motivo, setMotivo] = useState(motivos[0]!)
  const abrir = useMutation({
    mutationFn: async (ventana: Window | null) => {
      try {
        const archivo = await apiArchivo(`/api/v1/reclutamiento/postulaciones/${postulacionId}/hoja-de-vida`, { motivo })
        const url = URL.createObjectURL(archivo)
        if (ventana) ventana.location.href = url
        else window.location.href = url
        setTimeout(() => URL.revokeObjectURL(url), 60_000)
      } catch (e) {
        ventana?.close()
        throw e
      }
    },
    onSuccess: () => setAbierto(false),
  })

  if (!puede) return <span className="ayuda-suave">Sin acceso</span>

  if (!abierto) {
    return <Boton pequeno icono={<IconoCandado />} onClick={() => setAbierto(true)}>Ver</Boton>
  }

  return (
    <div className="abrir-hv">
      <select className="entrada" value={motivo} onChange={(e) => setMotivo(e.target.value)} aria-label="Motivo para abrir la hoja de vida">
        {motivos.map((m) => <option key={m}>{m}</option>)}
      </select>
      <span className="abrir-hv-acciones">
        <Boton pequeno variante="primario" cargando={abrir.isPending} textoCargando="Abriendo…" onClick={() => abrir.mutate(window.open('', '_blank'))}>Abrir</Boton>
        <Boton pequeno onClick={() => setAbierto(false)} disabled={abrir.isPending}>Cancelar</Boton>
      </span>
      <span className="ayuda-suave">Queda en la bitácora con su nombre y el motivo.</span>
      {abrir.error && <span role="alert" className="error-formulario">{abrir.error.message}</span>}
    </div>
  )
}
