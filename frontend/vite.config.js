import { defineConfig } from 'vite'
import react, { reactCompilerPreset } from '@vitejs/plugin-react'
import babel from '@rolldown/plugin-babel'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    babel({ presets: [reactCompilerPreset()] })
  ],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'https://localhost:7240', // Matches your https profile
        changeOrigin: true,
        secure: false, // Bypasses self-signed certificate issues
        fallback: 'http://localhost:5267', // Just a comment for alternative
      }
    }
  }
})
