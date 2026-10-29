const baseUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5000'

/** Dirección completa de la API, para enlaces de descarga que abren en el navegador (llevan la cookie de sesión). */
export const urlApi = (path: string) => `${baseUrl}${path}`

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

/** Error con el ProblemDetails que devuelve la API; `message` trae el texto para mostrar a la persona. */
export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | null

  constructor(status: number, problem: ProblemDetails | null) {
    const validacion = problem?.errors ? Object.values(problem.errors).flat().join(' ') : ''
    super(problem?.detail || validacion || problem?.title || `La API respondió ${status}`)
    this.status = status
    this.problem = problem
  }
}

/** fetch que, si la API no responde (apagada o sin red), devuelve un error que se entiende en vez de «Failed to fetch». */
async function conectar(url: string, init: RequestInit): Promise<Response> {
  try {
    return await fetch(url, init)
  } catch (e) {
    if (e instanceof DOMException && e.name === 'AbortError') throw e
    throw new ApiError(0, { detail: 'No pudimos conectar con Selvana. Revise su conexión a internet e intente de nuevo; si sigue igual, el servidor puede estar apagado.' })
  }
}

async function leer<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw new ApiError(response.status, (await response.json().catch(() => null)) as ProblemDetails | null)
  }
  if (response.status === 204) return undefined as T
  const texto = await response.text()
  return (texto ? JSON.parse(texto) : null) as T
}

export async function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  return leer<T>(await conectar(`${baseUrl}${path}`, { signal, credentials: 'include', headers: { Accept: 'application/json' } }))
}

export async function apiSend<T = void>(method: 'POST' | 'PATCH' | 'PUT' | 'DELETE', path: string, body?: unknown): Promise<T> {
  return leer<T>(
    await conectar(`${baseUrl}${path}`, {
      method,
      credentials: 'include',
      headers: body === undefined ? { Accept: 'application/json' } : { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    }),
  )
}

export async function apiUpload<T>(path: string, form: FormData): Promise<T> {
  return leer<T>(await conectar(`${baseUrl}${path}`, { method: 'POST', body: form, credentials: 'include', headers: { Accept: 'application/json' } }))
}

/** POST que devuelve un archivo (por ejemplo, abrir una hoja de vida con registro en la bitácora). */
export async function apiArchivo(path: string, body: unknown): Promise<Blob> {
  const respuesta = await conectar(`${baseUrl}${path}`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!respuesta.ok) {
    throw new ApiError(respuesta.status, (await respuesta.json().catch(() => null)) as ProblemDetails | null)
  }
  return respuesta.blob()
}
