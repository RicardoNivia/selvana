import { Esqueleto } from '../../shared/ui/Esqueleto'
import { useBateriaCargo, useGuardarBateriaCargo } from './api'
import { EditorBateria } from './EditorBateria'

const fecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' })

/** En el perfil del cargo: qué pruebas psicotécnicas aplica. Cada vacante de este cargo la hereda. */
export function PruebasDelCargo({ cargo }: { cargo: string }) {
  const bateria = useBateriaCargo(cargo)
  const guardar = useGuardarBateriaCargo()

  return (
    <div className="campo pruebas-cargo">
      <span>Pruebas psicotécnicas</span>
      {bateria.isPending ? <Esqueleto alto={120} /> : bateria.data && (
        <>
          {!bateria.data.esSugerida && bateria.data.actualizadaPor && (
            <span className="sub-celda">Configuradas por {bateria.data.actualizadaPor}{bateria.data.actualizadaEn ? ` · ${fecha.format(new Date(bateria.data.actualizadaEn))}` : ''}. Las vacantes nuevas de este cargo las heredan.</span>
          )}
          <EditorBateria
            key={`${cargo}-${bateria.data.actualizadaEn ?? 'sugerida'}`}
            inicial={bateria.data.pruebas}
            sugerida={bateria.data.esSugerida}
            razon={bateria.data.razon}
            guardando={guardar.isPending}
            error={guardar.error?.message}
            etiquetaGuardar={bateria.data.esSugerida ? 'Guardar para este cargo' : 'Guardar cambios'}
            onGuardar={(pruebas) => guardar.mutate({ cargo, pruebas })}
          />
        </>
      )}
    </div>
  )
}
