import { screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { confirmarEmail } from '../Api/auth'
import { auth, definirUsuario, OPERADOR } from '../test/authFalso.jsx'
import { erroApi, renderTela } from '../test/renderTela.jsx'
import ConfirmarEmail from './ConfirmarEmail.jsx'

vi.mock('../Api/auth')
vi.mock('../Auth/AuthContext.jsx', () => import('../test/authFalso.jsx'))

// A tela guarda o resultado por token (para o link não ser gasto duas vezes): cada teste usa um token novo
const abrir = (token) => renderTela(<ConfirmarEmail />, { rota: token ? `/confirmar-email?token=${token}` : '/confirmar-email' })

describe('Confirmar e-mail (link do e-mail)', () => {
    beforeEach(() => definirUsuario(null))

    it('confirma e oferece ir para o login', async () => {
        confirmarEmail.mockResolvedValue({})
        abrir('tok-1')

        expect(await screen.findByText('E-mail confirmado')).toBeInTheDocument()
        expect(confirmarEmail).toHaveBeenCalledWith('tok-1')
        expect(screen.getByRole('link', { name: 'Ir para o login' })).toHaveAttribute('href', '/login')
    })

    it('logado: atualiza o usuário e oferece Minha conta', async () => {
        definirUsuario(OPERADOR)
        confirmarEmail.mockResolvedValue({})
        abrir('tok-2')

        expect(await screen.findByRole('link', { name: 'Ir para Minha conta' })).toHaveAttribute('href', '/minha-conta')
        expect(auth.recarregarUsuario).toHaveBeenCalled()
    })

    it('link inválido ou já usado', async () => {
        confirmarEmail.mockRejectedValue(erroApi(400, { codigo: 'TOKEN_INVALIDO' }))
        abrir('tok-3')
        expect(await screen.findByText('Link inválido ou expirado')).toBeInTheDocument()
    })

    it('sem token nem chama a API', () => {
        abrir()
        expect(screen.getByText('Link inválido ou expirado')).toBeInTheDocument()
        expect(confirmarEmail).not.toHaveBeenCalled()
    })
})
