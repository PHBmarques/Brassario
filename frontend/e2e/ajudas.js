import { expect } from '@playwright/test'

// Usuário do seed (docker-compose / DbSeeder). Dá para trocar por variável de ambiente.
export const EMAIL = process.env.E2E_EMAIL ?? 'admin@brassario.com'
export const SENHA = process.env.E2E_SENHA ?? 'Admin@2026'

// Nome único por execução: o teste pode rodar várias vezes no mesmo banco
export const nomeUnico = (prefixo) => {
    const d = new Date()
    const p = (n) => String(n).padStart(2, '0')
    return `${prefixo} ${p(d.getHours())}${p(d.getMinutes())}${p(d.getSeconds())}`
}

// Data daqui a N dias no formato do <input type="date"> (AAAA-MM-DD, horário local)
export const dataEmDias = (dias) => {
    const d = new Date()
    d.setDate(d.getDate() + dias)
    const p = (n) => String(n).padStart(2, '0')
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

export async function entrar(page) {
    await page.goto('/login')
    await page.locator('#email').fill(EMAIL)
    await page.locator('#senha').fill(SENHA)
    await page.getByRole('button', { name: 'Entrar' }).click()
    await expect(page.getByRole('navigation', { name: 'Menu principal' })).toBeVisible()
}

// Consulta a API pelo mesmo endereço do navegador (Nginx -> backend), com um login próprio
export async function saldoNaApi(request, nome) {
    const login = await request.post('/api/v1/Auth/login', { data: { email: EMAIL, senha: SENHA } })
    expect(login.ok(), 'login na API').toBeTruthy()
    const { accessToken } = await login.json()

    const resposta = await request.get('/api/v1/armario', {
        headers: { Authorization: `Bearer ${accessToken}` },
    })
    expect(resposta.status()).toBe(200)
    const item = (await resposta.json()).find((i) => i.nome === nome)
    return item ? Number(item.saldo) : null
}
