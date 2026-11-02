import { screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { cancelarBatelada, finalizarBatelada, listarBateladas, obterBatelada } from '../Api/bateladas.js'
import { listarReceitas } from '../Api/receitas.js'
import { renderTela } from '../test/renderTela.jsx'
import Producoes from './Producoes.jsx'

vi.mock('../Api/bateladas.js')
vi.mock('../Api/receitas.js')

const CONSUMOS = [
    { loteId: 1, codigoLote: 'M-204', validade: '2027-02-12', insumoId: 1, insumo: 'Malte Pilsen', unidadeMedida: 'KG', quantidade: 18 },
    { loteId: 7, codigoLote: 'NF1234-A', validade: '2027-03-31', insumoId: 4, insumo: 'Lúpulo Cascade', unidadeMedida: 'KG', quantidade: 0.12 },
]

const LISTA = {
    itens: [
        { id: 12, codigo: 'B-2026-0012', receita: 'IPA Cascade', quantidade: 100, status: 'EM_ANDAMENTO', custoTotal: 412.5, dataProducao: '2026-10-20T12:00:00Z' },
        { id: 11, codigo: 'B-2026-0011', receita: 'Pilsen Clássica', quantidade: 200, status: 'FINALIZADA', custoTotal: 690, dataProducao: '2026-10-14T12:00:00Z' },
        { id: 9, codigo: 'B-2026-0009', receita: 'Witbier', quantidade: 80, status: 'CANCELADA', custoTotal: 0, dataProducao: '2026-10-02T12:00:00Z' },
    ],
    pagina: 1,
    tamanhoPagina: 50,
    total: 3,
    totalPaginas: 1,
}

const DETALHE = {
    id: 12,
    codigo: 'B-2026-0012',
    receitaId: 1,
    receita: 'IPA Cascade',
    volumePlanejado: 100,
    status: 'EM_ANDAMENTO',
    custoTotal: 412.5,
    dataProducao: '2026-10-20T12:00:00Z',
    consumos: CONSUMOS,
}

const linha = (codigo) => screen.getByRole('button', { name: codigo }).closest('tr')

// A tela já desenhada: escolhe a produção em andamento e espera o detalhe
async function abrirDetalhe(user) {
    await screen.findByText('B-2026-0012')
    await user.click(screen.getByRole('button', { name: 'B-2026-0012' }))
    await screen.findByLabelText('Volume final obtido (L) *')
}

describe('Produções', () => {
    beforeEach(() => {
        listarReceitas.mockResolvedValue([{ id: 1, nome: 'IPA Cascade' }])
        listarBateladas.mockResolvedValue(LISTA)
        obterBatelada.mockResolvedValue(DETALHE)
    })

    it('lista o histórico com a situação de cada produção', async () => {
        renderTela(<Producoes />, { rota: '/bateladas' })
        await screen.findByText('B-2026-0012')

        expect(within(linha('B-2026-0012')).getByText('Em andamento')).toBeInTheDocument()
        expect(within(linha('B-2026-0011')).getByText('Finalizada')).toBeInTheDocument()
        expect(within(linha('B-2026-0009')).getByText('Cancelada')).toBeInTheDocument()
        expect(within(linha('B-2026-0012')).getByText('R$ 412,50')).toBeInTheDocument()
        expect(within(linha('B-2026-0012')).getByText('20/10')).toBeInTheDocument()
        // Período padrão: últimos 30 dias
        expect(listarBateladas).toHaveBeenCalledWith(expect.objectContaining({ de: expect.any(String), ate: expect.any(String) }))
    })

    it('os filtros vão para a API', async () => {
        const { user } = renderTela(<Producoes />, { rota: '/bateladas' })
        await screen.findByText('B-2026-0012')

        await user.selectOptions(screen.getByLabelText('Situação'), 'FINALIZADA')
        await user.selectOptions(screen.getByLabelText('Período'), '')
        await user.selectOptions(screen.getByLabelText('Receita'), '1')

        expect(listarBateladas).toHaveBeenLastCalledWith(
            expect.objectContaining({ status: 'FINALIZADA', receitaId: '1', de: '', ate: '' })
        )
    })

    it('escolher uma linha abre o detalhe com os lotes consumidos', async () => {
        const { user } = renderTela(<Producoes />, { rota: '/bateladas' })
        await abrirDetalhe(user)

        expect(obterBatelada).toHaveBeenCalledWith(12)
        expect(screen.getByText('IPA Cascade, 100 L, em andamento')).toBeInTheDocument()
        expect(screen.getByText('M-204')).toBeInTheDocument()
        expect(screen.getByText(/Da produção B-2026-0012. Custo total: R\$ 412,50/)).toBeInTheDocument()
    })

    it('calcula o rendimento e avisa quando fica abaixo de 70%', async () => {
        const { user } = renderTela(<Producoes />, { rota: '/bateladas' })
        await abrirDetalhe(user)
        const volume = screen.getByLabelText('Volume final obtido (L) *')

        await user.clear(volume)
        await user.type(volume, '92,5')
        expect(screen.getByText('92,5%')).toBeInTheDocument()
        expect(screen.getByText(/Custo por litro: R\$ 4,46/)).toBeInTheDocument()
        expect(screen.queryByText(/rendimento abaixo de 70%/)).not.toBeInTheDocument()

        await user.clear(volume)
        await user.type(volume, '65')
        expect(screen.getByText(/Atenção: rendimento abaixo de 70%/)).toBeInTheDocument()
    })

    it('finaliza a produção', async () => {
        finalizarBatelada.mockResolvedValue({ ...DETALHE, status: 'FINALIZADA', volumeFinal: 92.5, rendimentoPercentual: 92.5, custoPorLitro: 4.46 })
        const { user } = renderTela(<Producoes />, { rota: '/bateladas' })
        await abrirDetalhe(user)

        await user.clear(screen.getByLabelText('Volume final obtido (L) *'))
        await user.type(screen.getByLabelText('Volume final obtido (L) *'), '92,5')
        await user.click(screen.getByRole('button', { name: 'Finalizar produção' }))

        expect(finalizarBatelada).toHaveBeenCalledWith(12, 92.5)
        // O detalhe mostra o resultado e a linha da tabela passa a ter o volume final
        expect((await screen.findByText('Volume final')).nextElementSibling).toHaveTextContent('92,5 L')
        expect(within(linha('B-2026-0012')).getByText('Finalizada')).toBeInTheDocument()
        expect(within(linha('B-2026-0012')).getByText('92,5 L')).toBeInTheDocument()
    })

    it('cancelar pede o motivo e devolve os insumos', async () => {
        cancelarBatelada.mockResolvedValue({ ...DETALHE, status: 'CANCELADA', custoTotal: 0, motivoCancelamento: 'Mosto contaminado', canceladoEm: '2026-10-21T12:00:00Z' })
        const { user } = renderTela(<Producoes />, { rota: '/bateladas' })
        await abrirDetalhe(user)

        await user.click(screen.getByRole('button', { name: 'Cancelar produção' }))
        await user.type(screen.getByLabelText('Motivo do cancelamento *'), 'ruim')
        await user.click(screen.getByRole('button', { name: 'Confirmar cancelamento' }))
        expect(screen.getByText('Explique o motivo (mínimo de 5 caracteres).')).toBeInTheDocument()
        expect(cancelarBatelada).not.toHaveBeenCalled()

        await user.clear(screen.getByLabelText('Motivo do cancelamento *'))
        await user.type(screen.getByLabelText('Motivo do cancelamento *'), 'Mosto contaminado')
        await user.click(screen.getByRole('button', { name: 'Confirmar cancelamento' }))

        expect(cancelarBatelada).toHaveBeenCalledWith(12, 'Mosto contaminado')
        expect(await screen.findByText('Mosto contaminado')).toBeInTheDocument()
        expect(within(linha('B-2026-0012')).getByText('Cancelada')).toBeInTheDocument()
    })

    it('logo depois de confirmar, mostra o estoque antes e depois', async () => {
        const producao = {
            ...DETALHE,
            insumos: [{ insumoId: 1, insumo: 'Malte Pilsen', unidadeMedida: 'KG', quantidade: 18, saldoAntes: 50, saldoDepois: 32 }],
        }
        const { user } = renderTela(<Producoes />, { rota: '/bateladas', state: { producao } })

        expect(screen.getByRole('status')).toHaveTextContent(
            'Produção B-2026-0012 iniciada: IPA Cascade, 100 L. O estoque já foi abatido.'
        )
        const malte = screen.getAllByRole('rowheader', { name: 'Malte Pilsen' })[0].closest('tr')
        expect(within(malte).getByText('50 kg')).toBeInTheDocument()
        expect(within(malte).getByText('- 18 kg')).toBeInTheDocument()
        expect(within(malte).getByText('32 kg')).toBeInTheDocument()
        expect(screen.getByRole('link', { name: 'Nova produção' })).toHaveAttribute('href', '/produzir')

        await user.click(screen.getByRole('button', { name: 'Ver todas as produções' }))
        expect(await screen.findByText('B-2026-0011')).toBeInTheDocument()
    })

    it('sem produções no período mostra a mensagem', async () => {
        listarBateladas.mockResolvedValue({ ...LISTA, itens: [], total: 0 })
        renderTela(<Producoes />, { rota: '/bateladas' })
        expect(await screen.findByText('Nenhuma produção encontrada com esses filtros.')).toBeInTheDocument()
    })
})
