import { screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { definirUsuario, OPERADOR } from '../test/authFalso.jsx'
import { renderTela } from '../test/renderTela.jsx'
import Home from './Home.jsx'
import NaoEncontrada from './NaoEncontrada.jsx'

vi.mock('../Auth/AuthContext.jsx', () => import('../test/authFalso.jsx'))

describe('Início', () => {
    it('cumprimenta pelo primeiro nome e mostra as seções', () => {
        definirUsuario(OPERADOR)
        renderTela(<Home />, { rota: '/' })

        expect(screen.getByText(/(Bom dia|Boa tarde|Boa noite), Carlos\./)).toBeInTheDocument()
        expect(screen.getByRole('heading', { name: 'Precisa de atenção' })).toBeInTheDocument()
        expect(screen.getByRole('heading', { name: 'Estoque agora' })).toBeInTheDocument()
        expect(screen.getByRole('link', { name: 'Ver compras sugeridas' })).toHaveAttribute('href', '/compras')
    })
})

describe('Página não encontrada', () => {
    it('oferece voltar ao início', () => {
        renderTela(<NaoEncontrada />, { rota: '/nao-existe' })
        expect(screen.getByRole('heading', { name: 'Página não encontrada' })).toBeInTheDocument()
        expect(screen.getByRole('link', { name: 'Voltar ao início' })).toHaveAttribute('href', '/')
    })
})
