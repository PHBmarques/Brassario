import { screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { produzir } from '../Api/bateladas.js'
import { listarReceitas, previaProducao } from '../Api/receitas.js'
import { erroApi, renderTela, stateDoDestino } from '../test/renderTela.jsx'
import Produzir from './Produzir.jsx'

vi.mock('../Api/bateladas.js')
vi.mock('../Api/receitas.js')

const RECEITAS = [
    { id: 1, nome: 'IPA Cascade', estilo: 'India Pale Ale', volumeBaseLitros: 100, ativa: true },
    { id: 9, nome: 'Receita antiga', estilo: null, volumeBaseLitros: 50, ativa: false },
]

const linhaPrevia = (insumo, lote, necessario, disponivel, sai, validade = '2027-02-12') => ({
    insumoId: insumo === 'Malte Pilsen' ? 1 : insumo === 'Lúpulo Cascade' ? 4 : 7,
    insumo,
    unidadeMedida: 'KG',
    necessario,
    disponivel,
    situacao: lote ? 'DISPONIVEL' : 'FALTA_ESTOQUE',
    loteId: lote ? lote.length : null,
    lote,
    validade: lote ? validade : null,
    saiDoLote: sai,
})

const PREVIA_OK = {
    receitaId: 1,
    receita: 'IPA Cascade',
    quantidade: 100,
    podeConfirmar: true,
    custoEstimado: 412.5,
    itens: [
        linhaPrevia('Malte Pilsen', 'M-204', 18, 50, 18),
        linhaPrevia('Lúpulo Cascade', 'NF1234-A', 0.3, 5.5, 0.12, '2027-03-31'),
        linhaPrevia('Lúpulo Cascade', 'NF1301-B', 0.3, 5.5, 0.18, '2027-06-15'),
    ],
    problemas: [],
}

const PREVIA_FALTA = {
    ...PREVIA_OK,
    podeConfirmar: false,
    itens: [linhaPrevia('Lúpulo Cascade', 'NF1234-A', 0.3, 0.1, 0.1), linhaPrevia('Levedura US-05', null, 0.2, 0, 0)],
    problemas: [
        { tipo: 'FALTA', insumoId: 4, insumo: 'Lúpulo Cascade', unidadeMedida: 'KG', falta: 0.2 },
        { tipo: 'FALTA', insumoId: 7, insumo: 'Levedura US-05', unidadeMedida: 'KG', falta: 0.2 },
        { tipo: 'VENCIDO', insumoId: 7, insumo: 'Levedura US-05', unidadeMedida: 'KG', lote: 'L-71', validade: '2026-10-10' },
    ],
}

const abrir = (rota = '/produzir?receita=1') => renderTela(<Produzir />, { rota })

describe('Produzir (nova produção)', () => {
    beforeEach(() => {
        listarReceitas.mockResolvedValue(RECEITAS)
        previaProducao.mockResolvedValue(PREVIA_OK)
    })

    it('só oferece receitas ativas e começa pelo volume base', async () => {
        abrir()
        expect(await screen.findByText('India Pale Ale, base de 100 L.')).toBeInTheDocument()
        expect(screen.getByLabelText('Volume a produzir (L) *')).toHaveValue('100')
        expect(screen.queryByRole('option', { name: 'Receita antiga' })).not.toBeInTheDocument()
    })

    it('mostra os lotes que serão usados (FEFO) e o custo', async () => {
        abrir()
        expect(await screen.findByText('Todos os ingredientes estão disponíveis.')).toBeInTheDocument()
        expect(previaProducao).toHaveBeenCalledWith('1', 100)

        expect(screen.getByText('NF1234-A')).toBeInTheDocument()
        expect(screen.getByText('NF1301-B')).toBeInTheDocument()
        // O nome do insumo aparece só na primeira linha dele
        expect(screen.getAllByRole('rowheader', { name: 'Lúpulo Cascade' })).toHaveLength(1)
        expect(screen.getByText('R$ 412,50')).toBeInTheDocument()
        expect(screen.getByText('R$ 4,13 por litro')).toBeInTheDocument()
        expect(screen.getByRole('button', { name: 'Confirmar produção' })).toBeEnabled()
    })

    it('com falta de insumo, explica o que falta e bloqueia a confirmação', async () => {
        previaProducao.mockResolvedValue(PREVIA_FALTA)
        abrir()

        const aviso = await screen.findByRole('alert')
        expect(aviso).toHaveTextContent('Faltam insumos para esta produção:')
        expect(within(aviso).getByText(/Lúpulo Cascade: faltam 0,2 kg \(necessário 0,3 kg, disponível 0,1 kg\)/)).toBeInTheDocument()
        expect(within(aviso).getByText(/o lote L-71 está vencido \(venceu em 10\/10\/2026\)/)).toBeInTheDocument()
        expect(screen.getByText('Sem lote válido')).toBeInTheDocument()
        expect(screen.getByText('2 ingredientes em falta. Confirmar está bloqueado.')).toBeInTheDocument()
        expect(screen.getByRole('button', { name: 'Confirmar produção' })).toBeDisabled()
    })

    it('confirma e vai para Produções com o resultado', async () => {
        const PRODUCAO = { id: 12, codigo: 'B-2026-0012' }
        produzir.mockResolvedValue(PRODUCAO)
        const { user } = abrir()
        await screen.findByText('Todos os ingredientes estão disponíveis.')

        await user.type(screen.getByLabelText('Observações'), 'Produção de terça')
        await user.click(screen.getByRole('button', { name: 'Confirmar produção' }))

        expect(produzir).toHaveBeenCalledWith({ receitaId: 1, volumePlanejado: 100, observacoes: 'Produção de terça' })
        const destino = await screen.findByTestId('destino')
        expect(destino).toHaveTextContent('/bateladas')
        expect(stateDoDestino(destino).producao).toEqual(PRODUCAO)
    })

    it('se o estoque mudou e a API recusar, mostra o erro e refaz a prévia', async () => {
        produzir.mockRejectedValue(erroApi(422, { codigo: 'ESTOQUE_INSUFICIENTE', mensagem: 'Estoque insuficiente para a produção.' }))
        const { user } = abrir()
        await screen.findByText('Todos os ingredientes estão disponíveis.')
        const chamadasAntes = previaProducao.mock.calls.length

        await user.click(screen.getByRole('button', { name: 'Confirmar produção' }))

        expect(await screen.findByText('Estoque insuficiente para a produção.')).toBeInTheDocument()
        await vi.waitFor(() => expect(previaProducao.mock.calls.length).toBeGreaterThan(chamadasAntes))
    })

    it('sem receita escolhida, pede para escolher', async () => {
        abrir('/produzir')
        expect(await screen.findByText('Escolha a receita e o volume para ver os lotes.')).toBeInTheDocument()
        expect(screen.getByRole('button', { name: 'Confirmar produção' })).toBeDisabled()
    })

    it('volume inválido mostra o erro', async () => {
        const { user } = abrir()
        await screen.findByText('Todos os ingredientes estão disponíveis.')
        await user.clear(screen.getByLabelText('Volume a produzir (L) *'))
        await user.type(screen.getByLabelText('Volume a produzir (L) *'), '0')
        expect(screen.getByText('Informe um volume maior que zero.')).toBeInTheDocument()
    })
})
