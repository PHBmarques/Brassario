import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
    plugins: [react()],
    server: {
        port: 5173,
        proxy: {
            // Em desenvolvimento, encaminha /api para a API.
            // Troque a porta pela da sua API (veja Properties/launchSettings.json)
            // ou use http://localhost:5000 se ela estiver no Docker.
            '/api': { target: 'http://localhost:5000', changeOrigin: true },
        },
    },
})