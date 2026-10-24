import { type RefObject, useEffect, useState } from 'react'

/** true si la persona pidió menos movimiento en su sistema. */
export function useMovimientoReducido() {
  const [reducido, setReducido] = useState(() => typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches)
  useEffect(() => {
    const consulta = window.matchMedia('(prefers-reduced-motion: reduce)')
    const cambiar = () => setReducido(consulta.matches)
    consulta.addEventListener('change', cambiar)
    return () => consulta.removeEventListener('change', cambiar)
  }, [])
  return reducido
}

/** Marca con la clase «revelado» cada elemento [data-revelar] dentro de la raíz cuando entra en pantalla (una sola vez). */
export function useRevelarAlDesplazar(raiz: RefObject<HTMLElement | null>) {
  useEffect(() => {
    const elementos = raiz.current?.querySelectorAll<HTMLElement>('[data-revelar]') ?? []
    if (!('IntersectionObserver' in window)) {
      elementos.forEach((e) => e.classList.add('revelado'))
      return
    }
    const observador = new IntersectionObserver(
      (entradas) => entradas.forEach((entrada) => {
        if (entrada.isIntersecting) {
          entrada.target.classList.add('revelado')
          observador.unobserve(entrada.target)
        }
      }),
      { threshold: 0.18, rootMargin: '0px 0px -8% 0px' },
    )
    elementos.forEach((e) => observador.observe(e))
    return () => observador.disconnect()
  }, [raiz])
}

/** true desde la primera vez que el elemento se ve en pantalla. */
export function useEnVista(ref: RefObject<HTMLElement | null>) {
  const [visto, setVisto] = useState(false)
  useEffect(() => {
    const elemento = ref.current
    if (!elemento || visto) return
    const observador = new IntersectionObserver(([e]) => { if (e?.isIntersecting) setVisto(true) }, { threshold: 0.4 })
    observador.observe(elemento)
    return () => observador.disconnect()
  }, [ref, visto])
  return visto
}

/** Cuenta de «desde» a «hasta» en «ms» con salida suave cuando «activo» pasa a true. */
export function useContador(desde: number, hasta: number, activo: boolean, ms = 1600) {
  const reducido = useMovimientoReducido()
  const [valor, setValor] = useState(desde)
  useEffect(() => {
    if (!activo) return
    if (reducido) {
      const t = setTimeout(() => setValor(hasta), 0)
      return () => clearTimeout(t)
    }
    let marco = 0
    const inicio = performance.now()
    const paso = (ahora: number) => {
      const avance = Math.min(1, (ahora - inicio) / ms)
      const suave = 1 - Math.pow(1 - avance, 3)
      setValor(Math.round(desde + (hasta - desde) * suave))
      if (avance < 1) marco = requestAnimationFrame(paso)
    }
    marco = requestAnimationFrame(paso)
    return () => cancelAnimationFrame(marco)
  }, [activo, desde, hasta, ms, reducido])
  return valor
}

/** Reloj de la escena: avanza en pasos de «tick» ms y vuelve a cero al llegar a «ciclo» pasos. Quieto en «final» con movimiento reducido. */
export function useEscena(ciclo: number, final: number, tick = 100) {
  const reducido = useMovimientoReducido()
  const [t, setT] = useState(0)
  useEffect(() => {
    if (reducido) return
    const id = setInterval(() => setT((v) => (v + 1) % ciclo), tick)
    return () => clearInterval(id)
  }, [ciclo, tick, reducido])
  return reducido ? final : t
}

/** Escribe la posición del puntero en variables CSS (--px, --py) del elemento, para el brillo que sigue al cursor. */
export function usePuntero(ref: RefObject<HTMLElement | null>) {
  useEffect(() => {
    const elemento = ref.current
    if (!elemento) return
    const mover = (e: PointerEvent) => {
      const caja = elemento.getBoundingClientRect()
      elemento.style.setProperty('--px', `${e.clientX - caja.left}px`)
      elemento.style.setProperty('--py', `${e.clientY - caja.top}px`)
    }
    elemento.addEventListener('pointermove', mover)
    return () => elemento.removeEventListener('pointermove', mover)
  }, [ref])
}

/** Avance de lectura de la página (0 a 1) y si ya se bajó un poco. */
export function useDesplazamiento() {
  const [estado, setEstado] = useState({ avance: 0, bajo: false })
  useEffect(() => {
    let marco = 0
    const medir = () => {
      cancelAnimationFrame(marco)
      marco = requestAnimationFrame(() => {
        const alto = document.documentElement.scrollHeight - window.innerHeight
        setEstado({ avance: alto > 0 ? window.scrollY / alto : 0, bajo: window.scrollY > 12 })
      })
    }
    medir()
    window.addEventListener('scroll', medir, { passive: true })
    window.addEventListener('resize', medir)
    return () => {
      cancelAnimationFrame(marco)
      window.removeEventListener('scroll', medir)
      window.removeEventListener('resize', medir)
    }
  }, [])
  return estado
}

/**
 * Reloj de una escena que solo corre mientras el elemento está en pantalla (se pausa al salir) y se repite cada «ciclo» pasos.
 * Con movimiento reducido queda quieto en «final» (el estado terminado de la escena).
 */
export function useEscenaEnVista(ref: RefObject<HTMLElement | null>, ciclo: number, final: number, tick = 100) {
  const reducido = useMovimientoReducido()
  const [t, setT] = useState(0)
  const [visible, setVisible] = useState(false)
  useEffect(() => {
    const elemento = ref.current
    if (!elemento) return
    if (!('IntersectionObserver' in window)) {
      const id = setTimeout(() => setVisible(true), 0)
      return () => clearTimeout(id)
    }
    const observador = new IntersectionObserver(([e]) => setVisible(Boolean(e?.isIntersecting)), { threshold: 0.3 })
    observador.observe(elemento)
    return () => observador.disconnect()
  }, [ref])
  useEffect(() => {
    if (reducido || !visible) return
    const id = setInterval(() => setT((v) => (v + 1) % ciclo), tick)
    return () => clearInterval(id)
  }, [ciclo, tick, reducido, visible])
  return reducido ? final : t
}

/** Avance suave (0 a 1) entre los pasos «desde» y «hasta» de una escena. */
export function tramo(t: number, desde: number, hasta: number) {
  const x = Math.min(1, Math.max(0, (t - desde) / Math.max(1, hasta - desde)))
  return 1 - Math.pow(1 - x, 3)
}
