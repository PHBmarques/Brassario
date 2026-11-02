import { screen, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { obterDetalheArmario } from '../Api/armario.js'
import { erroApi, renderTela } from '../test/renderTela.jsx'
import { guardarNotaFiscal } from '../Utils/notasFiscais.js'
import DetalheInsumo from './DetalheInsumo.jsx'

vi.mock('../Api/armario.js')

const DETALHE = {
    resumo: { nome: 'Lúpulo Cascade', categoria: 'LUPULO', unidadeMedida: 'KG', estoqueMinimo: 1, saldo: 5.5, quantidadeLotes: 2, diasParaVencer: 25, cor: 'AMARELO' },
    lotes: [
        { id: 7, ordemFefo: 1, codigoLote: 'NF1234-A', fornecedorNome: 'Hop Brasil', dataEntrada: '2026-01-10', dataValidade: '2026-11-18', quantidadeAtual: 0.5, situacaoValidade: 'AMARELO', diasParaVencer: 25, vencido: false },
        { id: 8, ordemFefo: 2, codigoLote: 'NF1301-B', fornecedorNome: null, dataEntrada: '2026-02-10', dataValidade: '2027-06-15', quantidadeAtual: 5, situacaoValidade: 'VERDE', diasParaVencer: 234, vencido: false },
        { id: 9, ordemFefo: null, codigoLote: 'C-12', fornecedorNome: null, dataEntrada: '2025-01-10', dataValidade: '2026-10-01', quantidadeAtual: 1, situacaoValidade: 'VERMELHO', diasParaVencer: -23, vencido: true },
    ],
}

const abrir = () => renderTela(<DetalheInsumo />, { rota: '/armario/4', caminho: '/armario/:id' })

describe('Detalhe do insumo', () => {
    it('mostra os indicadores e os lotes na ordem de uso', async () => {
        guardarNotaFiscal(7, '004512')
        obterDetalheArmario.mockResolvedValue(DETALHE)
        abrir()

        expect(await screen.findByRole('heading', { name: 'Lúpulo Cascade' })).toBeInTheDocument()
        expect(obterDetalheArmario).toHaveBeenCalledWith('4')
        expect(screen.getByText('5,5 kg')).toBeInTheDocument()

        const primeiro = screen.getByText('NF1234-A').closest('tr')
        expect(within(primeiro).getByText(/Usado primeiro/)).toBeInTheDocument()
        expect(within(primeiro).getByText(/NF 004512/)).toBeInTheDocument()

        const vencido = screen.getByText('C-12').closest('tr')
        expect(within(vencido).getByText('Vencido')).toBeInTheDocument()
        expect(within(vencido).getByText(/Fora da produção/)).toBeInTheDocument()
    })

    it('erro ao carregar permite tentar de novo', async () => {
        obterDetalheArmario.mockRejectedValueOnce(erroApi(404, { mensagem: 'Insumo não encontrado.' })).mockResolvedValue(DETALHE)
        const { user } = abrir()

        expect(await screen.findByRole('alert')).toHaveTextContent('Insumo não encontrado.')
        await user.click(screen.getByRole('button', { name: 'Tentar de novo' }))
        expect(await screen.findByRole('heading', { name: 'Lúpulo Cascade' })).toBeInTheDocument()
    })

    it('tem o link de volta para o Armário', async () => {
        obterDetalheArmario.mockResolvedValue(DETALHE)
        abrir()
        expect(screen.getByRole('link', { name: '‹ Voltar ao Armário' })).toHaveAttribute('href', '/armario')
    })
})
