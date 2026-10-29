import type { SVGProps } from 'react'

type P = SVGProps<SVGSVGElement>

const linea = { fill: 'none', stroke: 'currentColor', strokeWidth: 1.6, strokeLinecap: 'round', strokeLinejoin: 'round' } as const

/** Isotipo de Selvana: dos hojas (empresa y persona) y la semilla violeta (la IA). */
export function Isotipo(props: P) {
  return (
    <svg viewBox="0 0 48 48" aria-hidden="true" {...props}>
      <path d="M24 24C24 12 31 5 43 5C43 17 36 24 24 24Z" fill="var(--color-primario)" />
      <path d="M24 24C24 36 17 43 5 43C5 31 12 24 24 24Z" fill="var(--color-primario)" />
      <circle cx="11" cy="12" r="5.5" fill="var(--color-ia)" />
    </svg>
  )
}

export function Chispa(props: P) {
  return (
    <svg viewBox="0 0 16 16" aria-hidden="true" {...props}>
      <path d="M8 0.5c.4 3.6 1.9 5.1 5.5 5.5-3.6.4-5.1 1.9-5.5 5.5-.4-3.6-1.9-5.1-5.5-5.5C6.1 5.6 7.6 4.1 8 .5Z" fill="currentColor" />
    </svg>
  )
}

export const IconoBandeja = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M3 11l2-7h10l2 7v5H3v-5Zm0 0h4l1 2h4l1-2h4" /></svg>
export const IconoVacantes = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><rect x="3" y="6" width="14" height="10" rx="2" /><path d="M7 6V4h6v2" /></svg>
export const IconoPersonas = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><circle cx="8" cy="7" r="3" /><path d="M2.5 16c.6-3 2.8-4.5 5.5-4.5s4.9 1.5 5.5 4.5M13 4.5a3 3 0 0 1 0 5.5M15 11.8c1.4.6 2.2 1.9 2.5 4.2" /></svg>
export const IconoConexion = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M7 3v4M13 3v4M5 7h10v3a5 5 0 0 1-10 0V7ZM10 15v2" /></svg>
export const IconoEstructura = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><rect x="7.5" y="2.5" width="5" height="4" rx="1" /><rect x="2.5" y="13.5" width="5" height="4" rx="1" /><rect x="12.5" y="13.5" width="5" height="4" rx="1" /><path d="M10 6.5v3.5M5 13.5V10h10v3.5" /></svg>
export const IconoUsuarios = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><circle cx="8" cy="7" r="3" /><path d="M2.5 16c.6-3 2.8-4.5 5.5-4.5 1.2 0 2.3.3 3.2.9M14.5 11l3 1.2v2.3c0 1.6-1.3 2.9-3 3.5-1.7-.6-3-1.9-3-3.5v-2.3l3-1.2Z" /></svg>
export const IconoCandado = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><rect x="4" y="9" width="12" height="8.5" rx="2" /><path d="M7 9V6.5a3 3 0 0 1 6 0V9M10 12.5v2" /></svg>
export const IconoCheck = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} strokeWidth={2} {...p}><path d="M4.5 10.5l3.5 3.5 7.5-8" /></svg>
export const IconoAlerta = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M10 3l8 14H2L10 3Z M10 8v4 M10 14.5v.01" /></svg>
export const IconoSubir = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M10 14V4M6 8l4-4 4 4M4 16h12" /></svg>
export const IconoEscudo = (p: P) => <svg viewBox="0 0 16 16" aria-hidden="true" {...linea} strokeWidth={1.5} {...p}><path d="M8 1l6 2.5v4c0 3.5-2.6 6.4-6 7.5-3.4-1.1-6-4-6-7.5v-4L8 1Z" /></svg>
export const IconoBuscar = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><circle cx="9" cy="9" r="5.5" /><path d="M13 13l4 4" /></svg>
export const IconoUbicacion = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M10 17.5s5.5-5 5.5-9.5a5.5 5.5 0 1 0-11 0c0 4.5 5.5 9.5 5.5 9.5Z" /><circle cx="10" cy="8" r="2" /></svg>
export const IconoReloj = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><circle cx="10" cy="10" r="7" /><path d="M10 6v4l2.5 2" /></svg>
export const IconoDinero = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><rect x="2.5" y="5" width="15" height="10" rx="2" /><circle cx="10" cy="10" r="2.2" /><path d="M5.5 8v4M14.5 8v4" /></svg>
export const IconoFlecha = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M4 10h12M11 5l5 5-5 5" /></svg>
export const IconoCompartir = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M8 11.5 12 8M8.5 8.5l3.5 3" /><circle cx="6" cy="10" r="2.2" /><circle cx="14" cy="6" r="2.2" /><circle cx="14" cy="14" r="2.2" /></svg>
export const IconoDocumento = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M5 2.5h6.5L15 6v11.5H5v-15Z" /><path d="M11.5 2.5V6H15M7.5 10h5M7.5 13h5" /></svg>
export const IconoCupos = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><circle cx="7" cy="7" r="2.5" /><circle cx="13.5" cy="7.5" r="2" /><path d="M2.5 15.5c.5-2.5 2.3-3.8 4.5-3.8s4 1.3 4.5 3.8M12 12c2 0 3.5 1 4 3.5" /></svg>
export const IconoBirrete = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M2 7.5 10 4l8 3.5-8 3.5-8-3.5Z" /><path d="M5.5 9.2V13c0 1.2 2 2.5 4.5 2.5s4.5-1.3 4.5-2.5V9.2M18 7.5v4.5" /></svg>
export const IconoHerramienta = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M12.5 3.5a3.5 3.5 0 0 0-3.2 4.9L3.8 13.9a1.6 1.6 0 0 0 2.3 2.3l5.5-5.5a3.5 3.5 0 0 0 4.9-3.2l-2.1 2.1-2.1-.6-.6-2.1 2.1-2.1a3.5 3.5 0 0 0-1.3-.3Z" /></svg>
export const IconoIdioma = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><circle cx="10" cy="10" r="7" /><path d="M3 10h14M10 3c2 2 3 4.3 3 7s-1 5-3 7c-2-2-3-4.3-3-7s1-5 3-7Z" /></svg>
export const IconoInsignia = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><circle cx="10" cy="8" r="5" /><path d="M7.5 12.3 6.5 18l3.5-2 3.5 2-1-5.7M8 8l1.4 1.4L12.2 6.6" /></svg>
export const IconoManos = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M2.5 9.5 6 6l3 1.5L11.5 6l6 3.5M9 7.5l-2.5 2.6a1.3 1.3 0 0 0 1.9 1.8l1.8-1.6 4 3.2M13.5 13.5l-1.7 1.5M11 15.7l-.9.8M6 6 2.5 12.5l2 1.8" /></svg>
export const IconoContraer = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><rect x="2.5" y="3.5" width="15" height="13" rx="2" /><path d="M7.5 3.5v13M13.5 8l-2 2 2 2" /></svg>
export const IconoExpandir = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><rect x="2.5" y="3.5" width="15" height="13" rx="2" /><path d="M7.5 3.5v13M11.5 8l2 2-2 2" /></svg>
export const IconoSol = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><circle cx="10" cy="10" r="3.5" /><path d="M10 2.5v1.5M10 16v1.5M2.5 10H4M16 10h1.5M4.7 4.7l1 1M14.3 14.3l1 1M4.7 15.3l1-1M14.3 5.7l1-1" /></svg>
export const IconoLuna = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M16.5 12A7 7 0 0 1 8 3.5a7 7 0 1 0 8.5 8.5Z" /></svg>
export const IconoPantalla = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><rect x="2.5" y="3.5" width="15" height="10" rx="2" /><path d="M7 17h6M10 13.5V17" /></svg>
export const IconoSalir = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M8 4H5a1.5 1.5 0 0 0-1.5 1.5v9A1.5 1.5 0 0 0 5 16h3M13 6.5 16.5 10 13 13.5M16.5 10H8" /></svg>
export const IconoChat = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M3.5 5.5A2 2 0 0 1 5.5 3.5h9a2 2 0 0 1 2 2v6a2 2 0 0 1-2 2H9l-3.5 3v-3a2 2 0 0 1-2-2v-6Z" /><path d="M7 8h6M7 10.5h4" /></svg>
export const IconoPrueba = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><rect x="4" y="3.5" width="12" height="14" rx="2" /><path d="M7.5 3.5V2.5h5v1M7 9l1.5 1.5L11 8M7 13.5h6" /></svg>
export const IconoMas = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M10 4v12M4 10h12" /></svg>
export const IconoCerrar = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M5 5l10 10M15 5 5 15" /></svg>
export const IconoInicio = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M3 9 10 3l7 6v7.5a1 1 0 0 1-1 1h-3.5V12h-5v5.5H4a1 1 0 0 1-1-1V9Z" /></svg>
export const IconoEdificio = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M3.5 17.5V5l6-2.5V17.5M9.5 7.5h7v10M2 17.5h16M6 7.5v.01M6 10.5v.01M6 13.5v.01M12.5 10.5h1M12.5 13.5h1" /></svg>
export const IconoClima = (p: P) => <svg viewBox="0 0 20 20" aria-hidden="true" {...linea} {...p}><path d="M10 17.5c-3.6-2.4-6-4.9-6-7.8A3.4 3.4 0 0 1 10 7.6a3.4 3.4 0 0 1 6 2.1c0 2.9-2.4 5.4-6 7.8Z" /><path d="M10 2.5v2M4.2 4.4l1.4 1.4M15.8 4.4l-1.4 1.4" /></svg>
