import { screen, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { listarAlertas } from '../Api/alertas.js'
import { erroApi, renderTela, stateDoDestino } from '../test/renderTela.jsx'
import Alertas from './Alertas.jsx'

vi.mock('../Api/alertas.js')

const alerta = (dados) => ({ unidadeMedida: 'KG', estoqueMinimo: 1, loteId: null, ...dados })

const ALERTAS = [
    alerta({ tipo: 'VENCIDO', severidade: 'CRITICA', acao: 'REGISTRAR_PERDA', insumoId: 3, insumoNome: 'Malte Caramelo', loteId: 5, codigoLote: 'C-12', dataValidade: '2026-10-13', diasParaVencer: -11, saldo: 4 }),
    alerta({ tipo: 'ESTOQUE_VAZIO', severidade: 'ALTA', acao: 'DAR_ENTRADA', insumoId: 1, insumoNome: 'Malte Munich', saldo: 0, estoqueMinimo: 25 }),
    alerta({ tipo: 'ATENCAO', severidade: 'MEDIA', acao: 'VER_LOTES', insumoId: 4, insumoNome: 'Lúpulo Cascade', loteId: 7, codigoLote: 'NF1234-A', dataValidade: '2026-11-12', diasParaVencer: 19, saldo: 0.15 }),
    alerta({ tipo: 'ESTOQUE_BAIXO', severidade: 'BAIXA', acao: 'VER_COMPRAS', insumoId: 6, insumoNome: 'Lúpulo Saaz', saldo: 0.2 }),
]

const itemDe = (nome) => screen.getByText(nome).closest('li')

describe('Alertas', () => {
    it('separa os alertas em Urgente e Atenção', async () => {
        listarAlertas.mockResolvedValue(ALERTAS)
        renderTela(<Alertas />, { rota: '/alertas' })

        const urgente = (await screen.findByRole('heading', { name: 'Urgente' })).parentElement
        const atencao = screen.getByRole('heading', { name: 'Atenção' }).parentElement
        expect(within(urgente).getByText('Malte Caramelo')).toBeInTheDocument()
        expect(within(atencao).getByText('Lúpulo Saaz')).toBeInTheDocument()

        expect(within(itemDe('Malte Caramelo')).getByText('Vencido')).toBeInTheDocument()
        expect(within(itemDe('Malte Caramelo')).getByText(/Lote C-12 venceu há 11 dias/)).toBeInTheDocument()
        expect(within(itemDe('Lúpulo Cascade')).getByText(/Lote NF1234-A vence em 19 dias/)).toBeInTheDocument()
        expect(within(itemDe('Malte Munich')).getByText('Não há estoque no Armário.')).toBeInTheDocument()
    })

    it('"Dar entrada" abre a entrada com o insumo e a origem do alerta', async () => {
        listarAlertas.mockResolvedValue(ALERTAS)
        const { user } = renderTela(<Alertas />, { rota: '/alertas' })
        await screen.findByText('Malte Munich')

        await user.click(within(itemDe('Malte Munich')).getByRole('button', { name: 'Dar entrada' }))

        const destino = await screen.findByTestId('destino')
        expect(destino).toHaveTextContent('/entrada')
        expect(stateDoDestino(destino)).toEqual({ insumoId: 1, origem: 'pelo alerta Armário vazio' })
    })

    it('"Adicionar à compra" leva para as compras sugeridas', async () => {
        listarAlertas.mockResolvedValue(ALERTAS)
        const { user } = renderTela(<Alertas />, { rota: '/alertas' })
        await screen.findByText('Lúpulo Saaz')

        await user.click(within(itemDe('Lúpulo Saaz')).getByRole('button', { name: 'Adicionar à compra' }))
        expect(await screen.findByTestId('destino')).toHaveTextContent('/compras')
    })

    it('"Ver lotes" abre o detalhe do insumo', async () => {
        listarAlertas.mockResolvedValue(ALERTAS)
        const { user } = renderTela(<Alertas />, { rota: '/alertas' })
        await screen.findByText('Lúpulo Cascade')

        await user.click(within(itemDe('Lúpulo Cascade')).getByRole('button', { name: 'Ver lotes' }))
        expect(await screen.findByTestId('destino')).toHaveTextContent('/armario/4')
    })

    it('sem alertas mostra que está tudo certo', async () => {
        listarAlertas.mockResolvedValue([])
        renderTela(<Alertas />, { rota: '/alertas' })
        expect(await screen.findByText('Nenhum alerta agora. Tudo certo no Armário.')).toBeInTheDocument()
    })

    it('mostra o erro quando não carrega', async () => {
        listarAlertas.mockRejectedValue(erroApi(500))
        renderTela(<Alertas />, { rota: '/alertas' })
        expect(await screen.findByRole('alert')).toHaveTextContent('Ocorreu um erro no servidor')
    })
})
