import { screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { disponibilidadeReceita, listarReceitas } from '../Api/receitas.js'
import { ADMIN, definirUsuario, OPERADOR } from '../test/authFalso.jsx'
import { erroApi, renderTela } from '../test/renderTela.jsx'
import Receitas from './Receitas.jsx'

vi.mock('../Api/receitas.js')
vi.mock('../Auth/AuthContext.jsx', () => import('../test/authFalso.jsx'))

const RECEITAS = [
    { id: 1, nome: 'IPA Cascade', estilo: 'India Pale Ale', volumeBaseLitros: 100, ativa: true },
    { id: 2, nome: 'Stout de Aveia', estilo: 'Stout', volumeBaseLitros: 60, ativa: true },
    { id: 3, nome: 'Witbier', estilo: null, volumeBaseLitros: 80, ativa: true },
]

const cartao = (nome) => screen.getByRole('heading', { name: nome }).closest('li')

describe('Receitas', () => {
    beforeEach(() => {
        definirUsuario(OPERADOR)
        listarReceitas.mockResolvedValue(RECEITAS)
        disponibilidadeReceita.mockImplementation(async (id) =>
            id === 2
                ? { podeProduzir: false, itens: [{ suficiente: false }, { suficiente: false }, { suficiente: true }] }
                : { podeProduzir: true, itens: [{ suficiente: true }] }
        )
    })

    it('mostra os cartões com o selo do que dá para produzir', async () => {
        renderTela(<Receitas />, { rota: '/receitas' })

        await screen.findByText('IPA Cascade')
        expect(await within(cartao('IPA Cascade')).findByText('Pode produzir')).toBeInTheDocument()
        expect(await within(cartao('Stout de Aveia')).findByText('Falta 2 ingredientes')).toBeInTheDocument()
        expect(within(cartao('IPA Cascade')).getByText('India Pale Ale, base de 100 L')).toBeInTheDocument()
        expect(within(cartao('Witbier')).getByText('Base de 80 L')).toBeInTheDocument()
        expect(disponibilidadeReceita).toHaveBeenCalledWith(2, 60)
    })

    it('"Ver ficha" leva para a ficha da receita', async () => {
        renderTela(<Receitas />, { rota: '/receitas' })
        await screen.findByText('IPA Cascade')
        expect(within(cartao('IPA Cascade')).getByRole('link', { name: 'Ver ficha' })).toHaveAttribute('href', '/receitas/1')
    })

    it('a busca filtra por nome ou estilo, sem acento', async () => {
        const { user } = renderTela(<Receitas />, { rota: '/receitas' })
        await screen.findByText('IPA Cascade')

        await user.type(screen.getByLabelText('Buscar receita'), 'stout')
        expect(screen.queryByText('IPA Cascade')).not.toBeInTheDocument()
        expect(screen.getByText('Stout de Aveia')).toBeInTheDocument()

        await user.clear(screen.getByLabelText('Buscar receita'))
        await user.type(screen.getByLabelText('Buscar receita'), 'xyz')
        expect(screen.getByText('Nenhuma receita encontrada.')).toBeInTheDocument()
    })

    it('"Nova receita" só aparece para o ADMIN', async () => {
        renderTela(<Receitas />, { rota: '/receitas' })
        await screen.findByText('IPA Cascade')
        expect(screen.queryByRole('link', { name: 'Nova receita' })).not.toBeInTheDocument()
    })

    it('ADMIN vê "Nova receita"', async () => {
        definirUsuario(ADMIN)
        renderTela(<Receitas />, { rota: '/receitas' })
        expect(await screen.findByRole('link', { name: 'Nova receita' })).toHaveAttribute('href', '/receitas/nova')
    })

    it('mostra o erro quando a lista não carrega', async () => {
        listarReceitas.mockRejectedValue(erroApi(500))
        renderTela(<Receitas />, { rota: '/receitas' })
        expect(await screen.findByRole('alert')).toHaveTextContent('Ocorreu um erro no servidor')
    })
})
