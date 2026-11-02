import { expect, test } from '@playwright/test'
import { dataEmDias, entrar, nomeUnico, saldoNaApi } from './ajudas.js'

test('cadastra um insumo com a primeira entrada e ele aparece no Armário e na API', async ({ page, request }) => {
    const nome = nomeUnico('Malte E2E')
    await entrar(page)

    // Interface: formulário de novo insumo
    await page.goto('/insumos/novo')
    await page.locator('#nome').fill(nome)
    await page.locator('#estoqueMinimo').fill('2')
    await page.locator('#codigoLote').fill('E2E-001')
    await page.locator('#quantidade').fill('5')
    await page.locator('#dataValidade').fill(dataEmDias(60))
    await page.getByRole('button', { name: 'Salvar insumo' }).click()

    // Interface: volta para o Armário e mostra o cartão do insumo novo
    await expect(page).toHaveURL(/\/armario$/)
    await expect(page.getByRole('link', { name: new RegExp(nome) })).toBeVisible()

    // API + banco: o saldo foi gravado e volta pela API (passando pelo Nginx)
    expect(await saldoNaApi(request, nome)).toBe(5)
})

test('mostra os erros de validação e não cadastra com dados inválidos', async ({ page }) => {
    await entrar(page)
    await page.goto('/insumos/novo')

    await page.getByRole('button', { name: 'Salvar insumo' }).click()

    await expect(page.getByText('Informe o nome do insumo.')).toBeVisible()
    await expect(page.getByText('Informe o estoque mínimo.')).toBeVisible()
    await expect(page).toHaveURL(/\/insumos\/novo$/)
})
