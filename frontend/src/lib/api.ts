import axios, { AxiosError } from 'axios'
import type { ApiResponse } from '@/types/api'

/** Shared axios instance. Dev: Vite proxies /api → the .NET host (see vite.config.ts). */
export const http = axios.create({
  baseURL: '/api/v1',
  headers: { 'Content-Type': 'application/json' },
})

const TOKEN_KEY = 'accountrack.accessToken'

export function setAuthToken(token: string | null): void {
  if (token) {
    localStorage.setItem(TOKEN_KEY, token)
  } else {
    localStorage.removeItem(TOKEN_KEY)
  }
}

export function getAuthToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

const MUTATING_METHODS = new Set(['post', 'put', 'patch'])

http.interceptors.request.use((config) => {
  const token = getAuthToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  // Idempotency-Key on mutating requests (ADR-0021): one key per logical send, so a transport
  // retry of the same request replays rather than double-posts. Callers may set their own.
  const method = (config.method ?? 'get').toLowerCase()
  if (MUTATING_METHODS.has(method) && !config.headers['Idempotency-Key']) {
    config.headers['Idempotency-Key'] = crypto.randomUUID()
  }
  return config
})

// On 401, drop the token and bounce to login (refresh-token rotation is a later enhancement).
let onUnauthorized: (() => void) | null = null
export function setUnauthorizedHandler(handler: () => void): void {
  onUnauthorized = handler
}

http.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ApiResponse<unknown>>) => {
    if (error.response?.status === 401) {
      setAuthToken(null)
      onUnauthorized?.()
    }
    return Promise.reject(error)
  },
)

/** Unwraps the `{ success, data }` envelope, throwing the message on failure. */
export async function unwrap<T>(promise: Promise<{ data: ApiResponse<T> }>): Promise<T> {
  const { data } = await promise
  if (data.success) {
    return data.data
  }
  throw new Error(data.message || 'Request failed')
}
