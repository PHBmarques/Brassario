import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import ProtectedRoute from '../Auth/ProtectedRoute.jsx'

const useAuth = vi.fn()
vi.mock('../Auth/AuthContext.jsx', () => ({ useAuth: () => useAuth() }))

function abrir(elemento, caminho = '/privada') {
    return render(
        <MemoryRouter initialEntries={[caminho]}>
            <Routes>
                <Route path="/login" element={<p>Tela de login</p>} />
                <Route path="/criar-senha" element={<p>Tela criar senha</p>} />
                <Route path="/" element={<p>Início</p>} />
                <Route path="/privada" element={elemento} />
            </Routes>
        </MemoryRouter>
    )
}

const conteudo = <p>Conteúdo protegido</p>

describe('ProtectedRoute', () => {
    beforeEach(() => useAuth.mockReset())

    it('mostra "Carregando…" enquanto recupera a sessão', () => {
        useAuth.mockReturnValue({ carregando: true, isAuthenticated: false })
        abrir(<ProtectedRoute>{conteudo}</ProtectedRoute>)
        expect(screen.getByRole('status')).toHaveTextContent('Carregando')
        expect(screen.queryByText('Tela de login')).not.toBeInTheDocument()
    })

    it('sem sessão manda para o login', () => {
        useAuth.mockReturnValue({ carregando: false, isAuthenticated: false })
        abrir(<ProtectedRoute>{conteudo}</ProtectedRoute>)
        expect(screen.getByText('Tela de login')).toBeInTheDocument()
        expect(screen.queryByText('Conteúdo protegido')).not.toBeInTheDocument()
    })

    it('com sessão mostra o conteúdo', () => {
        useAuth.mockReturnValue({ carregando: false, isAuthenticated: true, usuario: { perfil: 'OPERADOR' } })
        abrir(<ProtectedRoute>{conteudo}</ProtectedRoute>)
        expect(screen.getByText('Conteúdo protegido')).toBeInTheDocument()
    })

    it('com senha temporária só deixa abrir a tela de criar senha', () => {
        useAuth.mockReturnValue({ carregando: false, isAuthenticated: true, usuario: { perfil: 'ADMIN', deveTrocarSenha: true } })
        abrir(<ProtectedRoute>{conteudo}</ProtectedRoute>)
        expect(screen.getByText('Tela criar senha')).toBeInTheDocument()
    })

    it('a tela de criar senha abre mesmo com troca pendente', () => {
        useAuth.mockReturnValue({ carregando: false, isAuthenticated: true, usuario: { perfil: 'ADMIN', deveTrocarSenha: true } })
        abrir(<ProtectedRoute permitirTrocaPendente>{conteudo}</ProtectedRoute>)
        expect(screen.getByText('Conteúdo protegido')).toBeInTheDocument()
    })

    it('OPERADOR em tela de ADMIN volta para o início', () => {
        useAuth.mockReturnValue({ carregando: false, isAuthenticated: true, usuario: { perfil: 'OPERADOR' } })
        abrir(<ProtectedRoute roles={['ADMIN']}>{conteudo}</ProtectedRoute>)
        expect(screen.getByText('Início')).toBeInTheDocument()
    })

    it('ADMIN abre a tela de ADMIN', () => {
        useAuth.mockReturnValue({ carregando: false, isAuthenticated: true, usuario: { perfil: 'ADMIN' } })
        abrir(<ProtectedRoute roles={['ADMIN']}>{conteudo}</ProtectedRoute>)
        expect(screen.getByText('Conteúdo protegido')).toBeInTheDocument()
    })
})
