import { screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { disponibilidadeReceita, obterReceita } from '../Api/receitas.js'
import { ADMIN, definirUsuario, OPERADOR } from '../test/authFalso.jsx'
import { erroApi, renderTela } from '../test/renderTela.jsx'
import FichaReceita from './FichaReceita.jsx'

vi.mock('../Api/receitas.js')
vi.mock('../Auth/AuthContext.jsx', () => import('../test/authFalso.jsx'))

const RECEITA = {
    id: 1,
    nome: 'IPA Cascade',
    estilo: 'India Pale Ale',
    volumeBaseLitros: 100,
    modoPreparo: 'Mosturar a 67 °C por 60 minutos.',
    ativa: true,
    itens: [
        { insumoId: 1, insumoNome: 'Malte Pilsen', unidadeMedida: 'KG', quantidade: 18 },
        { insumoId: 4, insumoNome: 'Lúpulo Cascade', unidadeMedida: 'KG', quantidade: 0.3 },
    ],
}

// Disponibilidade proporcional ao volume: falta Cascade acima de 400 L
function disponibilidadeFalsa(id, volume) {
    const fator = volume / 100
    const itens = [
        { insumoId: 1, insumo: 'Malte Pilsen', unidadeMedida: 'KG', necessario: 18 * fator, disponivel: 500 },
        { insumoId: 4, insumo: 'Lúpulo Cascade', unidadeMedida: 'KG', necessario: 0.3 * fator, disponivel: 1.2 },
    ].map((i) => ({ ...i, suficiente: i.disponivel >= i.necessario, falta: Math.max(0, i.necessario - i.disponivel) }))
    return Promise.resolve({ receitaId: id, volume, podeProduzir: itens.every((i) => i.suficiente), itens })
}

const abrir = () => renderTela(<FichaReceita />, { rota: '/receitas/1', caminho: '/receitas/:id' })

describe('Ficha de receita', () => {
    beforeEach(() => {
        definirUsuario(OPERADOR)
        obterReceita.mockResolvedValue(RECEITA)
        disponibilidadeReceita.mockImplementation(disponibilidadeFalsa)
    })

    it('mostra os ingredientes, o que tem no Armário e o modo de preparo', async () => {
        abrir()
        expect(await screen.findByRole('heading', { name: 'Ficha de Receita: IPA Cascade' })).toBeInTheDocument()
        expect(await screen.findByText('Todos os ingredientes estão disponíveis.')).toBeInTheDocument()

        const malte = screen.getByRole('rowheader', { name: 'Malte Pilsen' }).closest('tr')
        expect(within(malte).getByText('18 kg')).toBeInTheDocument()
        expect(within(malte).getByText('500 kg')).toBeInTheDocument()
        expect(within(malte).getByText('Disponível')).toBeInTheDocument()
        expect(screen.getByText('Mosturar a 67 °C por 60 minutos.')).toBeInTheDocument()
        expect(screen.getByLabelText('Volume a produzir (L)')).toHaveValue('100')
    })

    it('tem o link para voltar à lista de receitas', async () => {
        abrir()
        expect(await screen.findByRole('link', { name: '‹ Voltar para Receitas' })).toHaveAttribute('href', '/receitas')
    })

    it('mudar o volume recalcula e mostra o que falta', async () => {
        const { user } = abrir()
        await screen.findByText('Todos os ingredientes estão disponíveis.')

        await user.clear(screen.getByLabelText('Volume a produzir (L)'))
        await user.type(screen.getByLabelText('Volume a produzir (L)'), '500')

        expect(await screen.findByText(/Faltam 1 ingrediente para 500 L: Lúpulo Cascade/)).toBeInTheDocument()
        expect(screen.getByText('Falta 0,3 kg')).toBeInTheDocument()
        expect(disponibilidadeReceita).toHaveBeenLastCalledWith(1, 500)
    })

    it('volume inválido mostra o erro', async () => {
        const { user } = abrir()
        await screen.findByText('Todos os ingredientes estão disponíveis.')
        await user.clear(screen.getByLabelText('Volume a produzir (L)'))
        expect(screen.getByText('Informe um volume maior que zero.')).toBeInTheDocument()
    })

    it('"Produzir" leva o volume para a tela de produção', async () => {
        abrir()
        await screen.findByText('Todos os ingredientes estão disponíveis.')
        expect(screen.getByRole('link', { name: 'Produzir' })).toHaveAttribute('href', '/produzir?receita=1&volume=100')
    })

    it('"Editar ficha" só para ADMIN', async () => {
        abrir()
        await screen.findByText('Todos os ingredientes estão disponíveis.')
        expect(screen.queryByRole('link', { name: 'Editar ficha' })).not.toBeInTheDocument()
    })

    it('ADMIN vê "Editar ficha"', async () => {
        definirUsuario(ADMIN)
        abrir()
        expect(await screen.findByRole('link', { name: 'Editar ficha' })).toHaveAttribute('href', '/receitas/1/editar')
    })

    it('receita que não existe mostra o erro', async () => {
        obterReceita.mockRejectedValue(erroApi(404, { mensagem: 'Receita não encontrada.' }))
        abrir()
        expect(await screen.findByRole('alert')).toHaveTextContent('Receita não encontrada.')
    })
})
