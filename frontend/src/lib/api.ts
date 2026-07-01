import axios, { AxiosError } from 'axios'
import type { ApiResponse, AuthResponse } from '@/types/api'

/** Shared axios instance. Dev: Vite proxies /api → the .NET host (see vite.config.ts). */
export const http = axios.create({
  baseURL: '/api/v1',
  headers: { 'Content-Type': 'application/json' },
})

const ACCESS_KEY = 'accountrack.accessToken'
const REFRESH_KEY = 'accountrack.refreshToken'

export function setAuthToken(token: string | null): void {
  if (token) {
    localStorage.setItem(ACCESS_KEY, token)
  } else {
    localStorage.removeItem(ACCESS_KEY)
  }
}

export function getAuthToken(): string | null {
  return localStorage.getItem(ACCESS_KEY)
}

export function setRefreshToken(token: string | null): void {
  if (token) {
    localStorage.setItem(REFRESH_KEY, token)
  } else {
    localStorage.removeItem(REFRESH_KEY)
  }
}

export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_KEY)
}

/** Streams any authenticated GET endpoint to a browser download (PDF, etc.). */
export async function downloadFile(path: string, fileName: string): Promise<void> {
  const res = await fetch(`/api/v1${path}`, {
    headers: { Authorization: `Bearer ${getAuthToken() ?? ''}` },
  })
  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  a.click()
  URL.revokeObjectURL(url)
}

export type ExportFormat = 'csv' | 'xlsx'

/** Streams an export endpoint to a browser download in the chosen format (ADR-0031). */
export async function downloadExport(path: string, baseName: string, format: ExportFormat = 'xlsx'): Promise<void> {
  const sep = path.includes('?') ? '&' : '?'
  const res = await fetch(`/api/v1${path}${sep}format=${format}`, {
    headers: { Authorization: `Bearer ${getAuthToken() ?? ''}` },
  })
  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `${baseName}.${format}`
  a.click()
  URL.revokeObjectURL(url)
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
  // Let the browser set the multipart boundary for file uploads (don't force JSON).
  if (config.data instanceof FormData) {
    delete config.headers['Content-Type']
  }
  return config
})

// Called when the session can't be recovered (no/expired refresh token) — clears + bounces to login.
let onUnauthorized: (() => void) | null = null
export function setUnauthorizedHandler(handler: () => void): void {
  onUnauthorized = handler
}

// Called after a successful silent refresh so the app can persist the rotated tokens + user.
let onSessionRefreshed: ((auth: AuthResponse) => void) | null = null
export function setSessionRefreshedHandler(handler: (auth: AuthResponse) => void): void {
  onSessionRefreshed = handler
}

// A single in-flight refresh shared across concurrent 401s, so we exchange the refresh token once.
let refreshInFlight: Promise<string | null> | null = null

async function refreshAccessToken(): Promise<string | null> {
  const refreshToken = getRefreshToken()
  if (!refreshToken) return null
  try {
    // Bare axios (no interceptors) so a 401 here can't recurse into this handler.
    const res = await axios.post<ApiResponse<AuthResponse>>('/api/v1/auth/refresh', { refreshToken })
    const payload = res.data
    const auth = payload.success ? payload.data : null
    if (!auth?.accessToken) return null
    setAuthToken(auth.accessToken)
    setRefreshToken(auth.refreshToken) // rotation: the previous refresh token is now spent
    onSessionRefreshed?.(auth)
    return auth.accessToken
  } catch {
    return null
  }
}

function forceLogout(): void {
  setAuthToken(null)
  setRefreshToken(null)
  onUnauthorized?.()
}

// On a 401 for a normal request, silently exchange the refresh token and retry once; only bounce to
// login when there is no valid refresh token (rotating refresh tokens — ADR-0003/SECURITY.md).
http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiResponse<unknown>>) => {
    const original = error.config as (typeof error.config & { _retried?: boolean }) | undefined
    const status = error.response?.status
    const isAuthCall = typeof original?.url === 'string' && original.url.includes('/auth/')

    if (status === 401 && original && !isAuthCall) {
      if (!original._retried) {
        original._retried = true

        // If another request already rotated the token since this one was sent, just retry with the
        // current token — don't spend the (now-rotated) refresh token a second time.
        const sentToken = original.headers?.Authorization
        const current = getAuthToken()
        if (sentToken && current && sentToken !== `Bearer ${current}`) {
          return http(original)
        }

        refreshInFlight ??= refreshAccessToken().finally(() => { refreshInFlight = null })
        const newToken = await refreshInFlight
        if (newToken) {
          return http(original) // the request interceptor re-attaches the fresh access token
        }
      }
      forceLogout()
    }

    return Promise.reject(error)
  },
)

/**
 * True when a rejected request is an optimistic-concurrency conflict (ADR-0021). Matches on the
 * `CONCURRENCY_CONFLICT` error code, not the 409 status — other conflicts (e.g. a duplicate code on
 * create) also return 409 but must not be reported as "changed by someone else".
 */
export function isConflict(error: unknown): boolean {
  const data = (error as { response?: { data?: { error?: { code?: string } } } })?.response?.data
  return data?.error?.code === 'CONCURRENCY_CONFLICT'
}

/** Unwraps the `{ success, data }` envelope, throwing the message on failure. */
export async function unwrap<T>(promise: Promise<{ data: ApiResponse<T> }>): Promise<T> {
  const { data } = await promise
  if (data.success) {
    return data.data
  }
  throw new Error(data.message || 'Request failed')
}
