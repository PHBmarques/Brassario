import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
    plugins: [react()],
    server: {
        proxy: {
            '/Api': {
                target: 'http://localhost:5268',
                changeOrigin: true,
            },
        },
    },
})