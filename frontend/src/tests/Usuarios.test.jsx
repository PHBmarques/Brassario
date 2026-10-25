import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import Usuarios from '../Pages/Usuarios.jsx'

const api = vi.hoisted(() => ({ listar: null }))
vi.mock('../Api/usuarios', () => ({
    listarUsuarios: () => api.listar(),
    alterarStatusUsuario: () => Promise.resolve({}),
    redefinirSenhaUsuario: () => Promise.resolve({ senhaTemporaria: 'xxxx-yyyy' }),
}))
vi.mock('../Auth/AuthContext.jsx', () => ({ useAuth: () => ({ usuario: { id: 1, nome: 'Admin' } }) }))

const USUARIOS = [
    { id: 1, nome: 'Administrador', email: 'admin@brassario.com', perfil: 'ADMIN', ativo: true },
    { id: 2, nome: 'Carlos Lima', email: 'carlos@cervejaria.local', perfil: 'OPERADOR', ativo: true },
    { id: 3, nome: 'Ana Souza', email: 'ana@cervejaria.local', perfil: 'OPERADOR', ativo: false },
]

const abrir = (entrada = '/usuarios') =>
    render(
        <MemoryRouter initialEntries={[entrada]}>
            <Usuarios />
        </MemoryRouter>
    )

describe('Usuarios (lista)', () => {
    beforeEach(() => {
        api.listar = () => Promise.resolve(USUARIOS)
    })

    it('tem o botão "Novo usuário" no topo e não mostra o formulário de cadastro', async () => {
        abrir()
        expect(await screen.findByText('Carlos Lima')).toBeInTheDocument()
        expect(screen.getByRole('link', { name: 'Novo usuário' })).toHaveAttribute('href', '/usuarios/novo')
        expect(screen.queryByRole('button', { name: 'Cadastrar usuário' })).not.toBeInTheDocument()
        expect(screen.queryByLabelText('Senha temporária *')).not.toBeInTheDocument()
    })

    it('lista os usuários com perfil e situação', async () => {
        abrir()
        const linha = (await screen.findByText('Ana Souza')).closest('tr')
        expect(within(linha).getByText('OPERADOR')).toBeInTheDocument()
        expect(within(linha).getByText('Inativo')).toBeInTheDocument()
        expect(within(linha).getByRole('button', { name: 'Ativar' })).toBeInTheDocument()
    })

    it('não oferece ações sobre o próprio usuário', async () => {
        abrir()
        const linha = (await screen.findByText(/Administrador/)).closest('tr')
        expect(within(linha).getByText('(você)')).toBeInTheDocument()
        expect(within(linha).queryByRole('button')).not.toBeInTheDocument()
    })

    it('mostra o aviso da senha temporária quando vem do cadastro e permite fechar', async () => {
        abrir({ pathname: '/usuarios', state: { senhaGerada: { nome: 'Carlos Lima', senha: 'Cerv-4829' } } })
        expect(screen.getByText(/Senha temporária de Carlos Lima/)).toHaveTextContent('Cerv-4829')
        await userEvent.click(screen.getByRole('button', { name: 'Fechar aviso da senha temporária' }))
        expect(screen.queryByText(/Cerv-4829/)).not.toBeInTheDocument()
    })

    it('sem vir do cadastro não mostra aviso de senha', async () => {
        abrir()
        await screen.findByText('Carlos Lima')
        expect(screen.queryByText(/Senha temporária de/)).not.toBeInTheDocument()
    })

    it('mostra o erro quando a lista não carrega', async () => {
        api.listar = () => new Promise((_, rejeita) => setTimeout(() => rejeita(Object.assign(new Error('x'), { response: { status: 403, data: {} } })), 0))
        abrir()
        expect(await screen.findByRole('alert')).toHaveTextContent('Você não tem permissão para fazer isso.')
    })
})
