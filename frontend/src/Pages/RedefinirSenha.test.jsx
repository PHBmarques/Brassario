import { screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { redefinirSenha } from '../Api/auth'
import { erroApi, renderTela } from '../test/renderTela.jsx'
import RedefinirSenha from './RedefinirSenha.jsx'

vi.mock('../Api/auth')

const abrir = (rota = '/redefinir-senha?token=abc123') => renderTela(<RedefinirSenha />, { rota })

async function preencher(user, nova, confirmar) {
    await user.type(screen.getByLabelText('Nova senha *'), nova)
    await user.type(screen.getByLabelText('Confirmar nova senha *'), confirmar)
    await user.click(screen.getByRole('button', { name: 'Salvar nova senha' }))
}

describe('Redefinir senha (link do e-mail)', () => {
    it('sem token na URL avisa que o link é inválido', () => {
        abrir('/redefinir-senha')
        expect(screen.getByText('Link inválido ou expirado')).toBeInTheDocument()
        expect(screen.getByRole('link', { name: 'Pedir novo link' })).toHaveAttribute('href', '/esqueci-senha')
    })

    it('valida o tamanho mínimo e a confirmação', async () => {
        const { user } = abrir()
        await preencher(user, 'curta', 'outra')

        expect(screen.getByText('A senha precisa ter no mínimo 8 caracteres.')).toBeInTheDocument()
        expect(screen.getByText('As senhas não são iguais.')).toBeInTheDocument()
        expect(redefinirSenha).not.toHaveBeenCalled()
    })

    it('salva a senha nova com o token do link', async () => {
        redefinirSenha.mockResolvedValue({})
        const { user } = abrir()
        await preencher(user, 'NovaSenha@1', 'NovaSenha@1')

        expect(redefinirSenha).toHaveBeenCalledWith({ token: 'abc123', novaSenha: 'NovaSenha@1', confirmarSenha: 'NovaSenha@1' })
        expect(await screen.findByText('Senha alterada')).toBeInTheDocument()
    })

    it('link vencido (TOKEN_INVALIDO) leva à tela de link inválido', async () => {
        redefinirSenha.mockRejectedValue(erroApi(400, { codigo: 'TOKEN_INVALIDO', mensagem: 'Link inválido ou expirado.' }))
        const { user } = abrir()
        await preencher(user, 'NovaSenha@1', 'NovaSenha@1')

        expect(await screen.findByText('Link inválido ou expirado')).toBeInTheDocument()
    })
})
