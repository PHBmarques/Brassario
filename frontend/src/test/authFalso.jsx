import { vi } from 'vitest'

// Substitui o Auth/AuthContext.jsx nos testes:
//   vi.mock('../Auth/AuthContext.jsx', () => import('../test/authFalso.jsx'))
// e, antes de cada teste, definirUsuario({...}) para escolher quem está logado.

export const ADMIN = {
    id: 1,
    nome: 'Bernardo Silva',
    email: 'bernardo@cervejaria.com',
    perfil: 'ADMIN',
    ativo: true,
    deveTrocarSenha: false,
    emailRecuperacaoConfirmado: true,
    emailRecuperacao: 'bernardo@gmail.com',
    criadoEm: '2026-10-12T12:00:00Z',
}

export const OPERADOR = {
    id: 2,
    nome: 'Carlos Lima',
    email: 'carlos@cervejaria.local',
    perfil: 'OPERADOR',
    ativo: true,
    deveTrocarSenha: false,
    emailRecuperacaoConfirmado: false,
    emailRecuperacao: null,
    criadoEm: '2026-10-12T12:00:00Z',
}

export const auth = {
    usuario: null,
    token: 'token-de-teste',
    isAuthenticated: true,
    carregando: false,
    login: vi.fn(),
    logout: vi.fn(),
    aplicarSessao: vi.fn(),
    atualizarUsuario: vi.fn(),
    recarregarUsuario: vi.fn(),
}

export function definirUsuario(usuario, extras = {}) {
    Object.assign(auth, {
        usuario,
        isAuthenticated: Boolean(usuario),
        carregando: false,
        ...extras,
    })
    auth.login.mockReset().mockResolvedValue(undefined)
    auth.logout.mockReset().mockResolvedValue(undefined)
    auth.aplicarSessao.mockReset().mockResolvedValue(undefined)
    auth.atualizarUsuario.mockReset()
    auth.recarregarUsuario.mockReset().mockResolvedValue(undefined)
}

export const useAuth = () => auth
export const AuthProvider = ({ children }) => children
