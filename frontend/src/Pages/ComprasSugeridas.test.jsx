import { screen, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { listarComprasSugeridas } from '../Api/alertas.js'
import { erroApi, renderTela, stateDoDestino } from '../test/renderTela.jsx'
import ComprasSugeridas from './ComprasSugeridas.jsx'

vi.mock('../Api/alertas.js')

const item = (dados) => ({ categoria: 'MALTE', unidadeMedida: 'KG', motivo: '', ...dados })

const SUGESTOES = [
    item({ insumoId: 1, insumoNome: 'Malte Munich', saldoUtilizavel: 0, estoqueMinimo: 25, consumoMedioDiario: 4.2, diasRestantes: 0 }),
    item({ insumoId: 7, insumoNome: 'Levedura US-05', saldoUtilizavel: 0.5, estoqueMinimo: 1, consumoMedioDiario: 0.1, diasRestantes: 5 }),
    item({ insumoId: 4, insumoNome: 'Lúpulo Cascade', saldoUtilizavel: 1.2, estoqueMinimo: 1.5, consumoMedioDiario: 0.1, diasRestantes: 12.4 }),
]

const linha = (nome) => screen.getByRole('rowheader', { name: nome }).closest('tr')

describe('Compras sugeridas', () => {
    it('mostra o que repor com a situação de cada insumo', async () => {
        listarComprasSugeridas.mockResolvedValue(SUGESTOES)
        renderTela(<ComprasSugeridas />, { rota: '/compras' })
        await screen.findByText('Malte Munich')

        const munich = linha('Malte Munich')
        expect(within(munich).getByText('0 kg')).toBeInTheDocument()
        expect(within(munich).getByText('25 kg')).toBeInTheDocument()
        expect(within(munich).getByText('4,2 kg')).toBeInTheDocument()
        expect(within(munich).getByText('Sem estoque')).toBeInTheDocument()

        // Até uma semana é urgente (vermelho); até 15 dias, atenção (amarelo)
        expect(within(linha('Levedura US-05')).getByText('5 dias', { selector: '.etiqueta' })).toHaveClass('etiqueta--critica')
        expect(within(linha('Lúpulo Cascade')).getByText('12 dias', { selector: '.etiqueta' })).toHaveClass('etiqueta--atencao')
        expect(screen.getByText(/Como calculamos:/)).toBeInTheDocument()
    })

    it('"Dar entrada" abre a entrada com o insumo escolhido', async () => {
        listarComprasSugeridas.mockResolvedValue(SUGESTOES)
        const { user } = renderTela(<ComprasSugeridas />, { rota: '/compras' })
        await screen.findByText('Levedura US-05')

        await user.click(within(linha('Levedura US-05')).getByRole('button', { name: 'Dar entrada' }))

        const destino = await screen.findByTestId('destino')
        expect(destino).toHaveTextContent('/entrada')
        expect(stateDoDestino(destino)).toEqual({ insumoId: 7, origem: 'pelas compras sugeridas' })
    })

    it('sem nada para repor mostra a mensagem', async () => {
        listarComprasSugeridas.mockResolvedValue([])
        renderTela(<ComprasSugeridas />, { rota: '/compras' })
        expect(await screen.findByText('Nada para repor agora: todos os insumos duram mais de 15 dias.')).toBeInTheDocument()
    })

    it('mostra o erro quando não carrega', async () => {
        listarComprasSugeridas.mockRejectedValue(erroApi(500))
        renderTela(<ComprasSugeridas />, { rota: '/compras' })
        expect(await screen.findByRole('alert')).toHaveTextContent('Ocorreu um erro no servidor')
    })
})
