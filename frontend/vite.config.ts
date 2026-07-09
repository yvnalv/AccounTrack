import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// Dev server proxies /api to the .NET host so the SPA and API share an origin in dev.
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  build: {
    // Ship no inline bootstrap <script>, so the SPA runs under a strict `script-src 'self'` CSP
    // (nginx.conf, SECURITY.md §5). Disabling the module-preload polyfill only forgoes a perf hint
    // on older browsers; native `modulepreload` still applies where supported.
    modulePreload: { polyfill: false },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
})
