import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
    plugins: [react()],
    test: {
        environment: 'jsdom',
        globals: true,
        setupFiles: './src/setupTests.js',
        css: false,
        include: ['src/**/*.test.{js,jsx}'],
    },
    server: {
        proxy: {
            // O cliente.js chama /api/v1/... (minúsculo); o Vite repassa para a API local
            '/api': {
                target: 'http://localhost:5000',
                changeOrigin: true,
            },
        },
    },
})
