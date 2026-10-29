import { useState } from 'react'

export type Tema = 'sistema' | 'claro' | 'oscuro'

const claveTema = 'selvana.tema'
const siguiente: Record<Tema, Tema> = { sistema: 'claro', claro: 'oscuro', oscuro: 'sistema' }

export const nombreTema: Record<Tema, string> = { sistema: 'Tema del sistema', claro: 'Tema claro', oscuro: 'Tema oscuro' }

/** El tema es una preferencia de cada persona en su navegador; sin almacenamiento sigue al sistema. */
function leerTema(): Tema {
  try {
    const t = localStorage.getItem(claveTema)
    return t === 'claro' || t === 'oscuro' ? t : 'sistema'
  } catch {
    return 'sistema'
  }
}

/** Sin `data-theme` los tokens siguen a `prefers-color-scheme` (ver tokens.css). */
function aplicarTema(tema: Tema) {
  const raiz = document.documentElement
  if (tema === 'sistema') raiz.removeAttribute('data-theme')
  else raiz.setAttribute('data-theme', tema === 'oscuro' ? 'dark' : 'light')
}

export function useTema() {
  const [tema, setTema] = useState(leerTema)
  const elegir = (nuevo: Tema) => {
    aplicarTema(nuevo)
    try {
      if (nuevo === 'sistema') localStorage.removeItem(claveTema)
      else localStorage.setItem(claveTema, nuevo)
    } catch {
      // Sin almacenamiento solo se pierde la preferencia
    }
    setTema(nuevo)
  }
  const alternar = () => elegir(siguiente[tema])
  return { tema, alternar, elegir }
}
