import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// The API runs on :5080. Proxying keeps the app and the API on one origin,
// so the refresh cookie can be SameSite=Strict.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/auth': 'http://localhost:5080',
      '/api': 'http://localhost:5080',
    },
  },
})
