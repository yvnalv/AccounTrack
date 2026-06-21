import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { http, setAuthToken, unwrap } from '@/lib/api'
import type { AuthResponse } from '@/types/api'

const USER_KEY = 'accountrack.user'

interface SessionUser {
  userId: string
  email: string
  fullName: string
  roles: string[]
  permissions: string[]
  companyIds: string[]
}

function loadUser(): SessionUser | null {
  const raw = localStorage.getItem(USER_KEY)
  return raw ? (JSON.parse(raw) as SessionUser) : null
}

export const useAuthStore = defineStore('auth', () => {
  const user = ref<SessionUser | null>(loadUser())
  const isAuthenticated = computed(() => user.value !== null)

  function setSession(auth: AuthResponse) {
    setAuthToken(auth.accessToken)
    const session: SessionUser = {
      userId: auth.userId,
      email: auth.email,
      fullName: auth.fullName,
      roles: auth.roles,
      permissions: auth.permissions,
      companyIds: auth.companyIds,
    }
    user.value = session
    localStorage.setItem(USER_KEY, JSON.stringify(session))
  }

  async function login(email: string, password: string) {
    const auth = await unwrap<AuthResponse>(http.post('/auth/login', { email, password }))
    setSession(auth)
  }

  async function register(payload: {
    organizationName: string
    companyName: string
    functionalCurrency: string
    fullName: string
    email: string
    password: string
  }) {
    const auth = await unwrap<AuthResponse>(http.post('/auth/register', payload))
    setSession(auth)
  }

  function clear() {
    user.value = null
    setAuthToken(null)
    localStorage.removeItem(USER_KEY)
  }

  function has(permission: string): boolean {
    return user.value?.permissions.includes(permission) ?? false
  }

  return { user, isAuthenticated, login, register, clear, has }
})
