/** Cómo sale la oferta en el portal de empleo: con o sin el nombre de la empresa, y con o sin el salario. */
export function OpcionesPublicacion({ empresaConfidencial, salarioOculto, onCambiar, disabled }: {
  empresaConfidencial: boolean
  salarioOculto: boolean
  onCambiar: (v: { empresaConfidencial: boolean; salarioOculto: boolean }) => void
  disabled?: boolean
}) {
  return (
    <fieldset style={{ border: 0, padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: 8 }}>
      <legend style={{ fontWeight: 600, fontSize: 14, marginBottom: 8 }}>Publicación en el portal</legend>
      <label className="interruptor">
        <input type="checkbox" checked={empresaConfidencial} disabled={disabled}
          onChange={(e) => onCambiar({ empresaConfidencial: e.target.checked, salarioOculto })} />
        Oferta confidencial: no mostrar el nombre de la empresa
      </label>
      <label className="interruptor">
        <input type="checkbox" checked={salarioOculto} disabled={disabled}
          onChange={(e) => onCambiar({ empresaConfidencial, salarioOculto: e.target.checked })} />
        No mostrar el salario (se ve «A convenir»)
      </label>
      <span className="ayuda">Selvana conserva el salario para el ranking y la oferta. En la autorización de datos el candidato sigue viendo quién trata sus datos, como exige la Ley 1581.</span>
    </fieldset>
  )
}
