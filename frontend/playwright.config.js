import { defineConfig, devices } from '@playwright/test'

// Testes E2E: abrem o sistema de verdade (Docker Compose no ar) e passam por
// Interface (React/Nginx) -> API (.NET) -> Banco (PostgreSQL).
// Rodar: docker compose up -d --build  e depois  npm run e2e
export default defineConfig({
    testDir: './e2e',
    timeout: 30_000,
    expect: { timeout: 7_000 },
    // Os testes cadastram dados no mesmo banco: um de cada vez
    workers: 1,
    fullyParallel: false,
    reporter: [['list'], ['html', { open: 'never' }]],
    use: {
        baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:3000',
        locale: 'pt-BR',
        trace: 'retain-on-failure',
        screenshot: 'only-on-failure',
        // E2E_LENTO=800 espera 0,8 s entre as ações, para dar tempo de ver (ou gravar) o teste
        launchOptions: { slowMo: Number(process.env.E2E_LENTO ?? 0) },
    },
    projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
