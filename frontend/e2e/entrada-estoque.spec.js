import { expect, test } from '@playwright/test'
import { dataEmDias, entrar, nomeUnico, saldoNaApi } from './ajudas.js'

async function preencherLote(page, { codigo, nota, quantidade, preco }) {
    await page.locator('#codigoLote').fill(codigo)
    await page.locator('#notaFiscal').fill(nota)
    await page.locator('#dataValidade').fill(dataEmDias(90))
    await page.locator('#quantidade').fill(quantidade)
    await page.locator('#precoUnitario').fill(preco)
}

test('registra entrada de estoque (insumo novo e depois insumo existente) e o saldo soma na API', async ({
    page,
    request,
}) => {
    const nome = nomeUnico('Lúpulo E2E')
    await entrar(page)
    await page.goto('/entrada')

    // 1ª entrada: cadastra o insumo novo no próprio formulário e salva tudo junto
    await page.getByRole('button', { name: '+ Novo insumo' }).first().click()
    await page.locator('#novoInsumoNome').fill(nome)
    await page.locator('#novoInsumoMinimo').fill('1')
    await page.getByRole('button', { name: 'Adicionar insumo' }).click()
    await preencherLote(page, { codigo: 'E2E-A', nota: '0001', quantidade: '10', preco: '12,50' })
    await page.getByRole('button', { name: 'Salvar entrada' }).click()

    await expect(page.getByRole('status').filter({ hasText: 'Entrada registrada' })).toContainText(nome)
    expect(await saldoNaApi(request, nome)).toBe(10)

    // 2ª entrada: escolhe o insumo que acabou de existir na lista de busca
    await page.getByRole('button', { name: 'Registrar outra entrada' }).click()
    await page.locator('#insumo').fill(nome)
    await page.getByRole('option', { name: new RegExp(nome) }).click()
    await preencherLote(page, { codigo: 'E2E-B', nota: '0002', quantidade: '5', preco: '13,00' })
    await page.getByRole('button', { name: 'Salvar entrada' }).click()

    await expect(page.getByRole('status').filter({ hasText: 'Entrada registrada' })).toBeVisible()
    // Persistência: os dois lotes somam no banco
    expect(await saldoNaApi(request, nome)).toBe(15)
})
