import { screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { ADMIN, auth, definirUsuario, OPERADOR } from '../test/authFalso.jsx'
import { renderTela } from '../test/renderTela.jsx'
import AppLayout from './AppLayout.jsx'

vi.mock('../Auth/AuthContext.jsx', () => import('../test/authFalso.jsx'))

const abrir = (rota = '/') => renderTela(<AppLayout />, { rota, caminho: '*' })

describe('Menu lateral', () => {
    it('OPERADOR não vê "Usuários"', () => {
        definirUsuario(OPERADOR)
        abrir()
        expect(screen.getByRole('link', { name: 'Receitas' })).toBeInTheDocument()
        expect(screen.queryByRole('link', { name: 'Usuários' })).not.toBeInTheDocument()
    })

    it('ADMIN vê "Usuários"', () => {
        definirUsuario(ADMIN)
        abrir()
        expect(screen.getByRole('link', { name: 'Usuários' })).toBeInTheDocument()
    })

    it('"Alertas" fica só na barra superior', () => {
        definirUsuario(OPERADOR)
        abrir()
        const alertas = screen.getByRole('link', { name: 'Alertas' })
        expect(alertas).toHaveAttribute('href', '/alertas')
        expect(screen.getByRole('banner')).toContainElement(alertas)
    })

    it('"Alertas" fica ativo também em Compras sugeridas', () => {
        definirUsuario(OPERADOR)
        abrir('/compras')
        expect(screen.getByRole('link', { name: 'Alertas' })).toHaveClass('app__topo-botao--ativo')
    })

    it('o menu do usuário tem Minha conta, Alterar minha senha e Sair', async () => {
        definirUsuario(OPERADOR)
        const { user } = abrir()

        await user.click(screen.getByRole('button', { name: /Carlos Lima/ }))
        const menu = screen.getByRole('menu')
        expect(menu).toHaveTextContent(OPERADOR.email)
        expect(screen.getByRole('menuitem', { name: 'Minha conta' })).toHaveAttribute('href', '/minha-conta')
        expect(screen.getByRole('menuitem', { name: 'Alterar minha senha' })).toHaveAttribute('href', '/alterar-senha')

        await user.click(screen.getByRole('menuitem', { name: 'Sair' }))
        expect(auth.logout).toHaveBeenCalled()
    })
})
