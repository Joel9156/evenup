import { useAuthStore } from '@/stores/authStore'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5143'

export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

interface ApiFetchOptions extends Omit<RequestInit, 'body'> {
  body?: unknown
  auth?: boolean
}

// Thin wrapper around fetch: attaches the JWT (if present), serializes/deserializes JSON,
// and turns non-2xx responses into a typed ApiError instead of forcing every call site to
// check response.ok itself.
export async function apiFetch<T>(path: string, options: ApiFetchOptions = {}): Promise<T> {
  const { body, auth = true, headers, ...rest } = options
  const token = auth ? useAuthStore.getState().token : null

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...rest,
    headers: {
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...headers,
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })

  if (response.status === 204) {
    return undefined as T
  }

  const text = await response.text()
  const data = text ? JSON.parse(text) : undefined

  if (!response.ok) {
    const message = extractErrorMessage(data) ?? `Request failed with status ${response.status}`
    throw new ApiError(response.status, message)
  }

  return data as T
}

// Handles both our own `{ message }` error shape and ASP.NET Core's default
// ProblemDetails/validation-error shape (`{ title, errors: { field: [msg] } }`).
function extractErrorMessage(data: unknown): string | undefined {
  if (!data || typeof data !== 'object') {
    return undefined
  }

  const obj = data as Record<string, unknown>

  if (typeof obj.message === 'string') {
    return obj.message
  }

  if (obj.errors && typeof obj.errors === 'object') {
    const firstError = Object.values(obj.errors as Record<string, unknown>)[0]
    if (Array.isArray(firstError) && typeof firstError[0] === 'string') {
      return firstError[0]
    }
  }

  if (typeof obj.title === 'string') {
    return obj.title
  }

  return undefined
}
