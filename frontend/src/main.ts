import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { i18n } from './i18n'
import { useThemeStore } from './stores/theme'
import { useAuthStore } from './stores/auth'
import { setUnauthorizedHandler } from './lib/api'
import './assets/styles/main.css'

const app = createApp(App)
const pinia = createPinia()

app.use(pinia)
app.use(i18n)
app.use(router)

// Apply the saved/system theme before first paint.
useThemeStore().init()

// On 401 from the API, clear the session and route to login.
setUnauthorizedHandler(() => {
  useAuthStore().clear()
  if (router.currentRoute.value.name !== 'login') {
    void router.push({ name: 'login' })
  }
})

app.mount('#app')
