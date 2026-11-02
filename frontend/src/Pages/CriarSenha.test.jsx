import { screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { primeiroAcesso } from '../Api/auth'
import { auth, definirUsuario, OPERADOR } from '../test/authFalso.jsx'
import { erroApi, renderTela, stateDoDestino } from '../test/renderTela.jsx'
import CriarSenha from './CriarSenha.jsx'

vi.mock('../Api/auth')
vi.mock('../Auth/AuthContext.jsx', () => import('../test/authFalso.jsx'))

const abrir = () => renderTela(<CriarSenha />, { rota: '/criar-senha' })
const SESSAO = { accessToken: 'novo' }

async function digitarSenhas(user, nova = 'MinhaSenha@1', confirmar = nova) {
    await user.type(screen.getByLabelText('Nova senha *'), nova)
    await user.type(screen.getByLabelText('Confirmar nova senha *'), confirmar)
}

describe('Crie sua senha (primeiro acesso)', () => {
    beforeEach(() => {
        definirUsuario({ ...OPERADOR, deveTrocarSenha: true })
        primeiroAcesso.mockResolvedValue(SESSAO)
    })

    it('quem não tem senha temporária volta para o início', async () => {
        definirUsuario(OPERADOR)
        abrir()
        expect(await screen.findByTestId('destino')).toHaveTextContent('/')
    })

    it('valida a senha nova e a confirmação', async () => {
        const { user } = abrir()
        await digitarSenhas(user, 'MinhaSenha@1', 'Diferente@1')
        await user.click(screen.getByRole('button', { name: 'Salvar e entrar' }))

        expect(screen.getByText('As senhas não são iguais.')).toBeInTheDocument()
        expect(primeiroAcesso).not.toHaveBeenCalled()
    })

    it('não aceita o e-mail de login como e-mail de recuperação', async () => {
        const { user } = abrir()
        await digitarSenhas(user)
        await user.type(screen.getByLabelText('E-mail pessoal'), OPERADOR.email)
        await user.click(screen.getByRole('button', { name: 'Salvar e entrar' }))

        expect(screen.getByText('Use um e-mail diferente do e-mail de login.')).toBeInTheDocument()
    })

    it('"Pular por agora" salva só a senha e vai para o início', async () => {
        const { user } = abrir()
        await digitarSenhas(user)
        await user.type(screen.getByLabelText('E-mail pessoal'), 'carlos.lima@gmail.com')
        await user.click(screen.getByRole('button', { name: 'Pular por agora' }))

        expect(primeiroAcesso).toHaveBeenCalledWith({
            novaSenha: 'MinhaSenha@1',
            confirmarSenha: 'MinhaSenha@1',
            emailRecuperacao: undefined,
        })
        expect(auth.aplicarSessao).toHaveBeenCalledWith(SESSAO)
        expect(await screen.findByTestId('destino')).toHaveTextContent('/')
    })

    it('com e-mail, salva e leva para Minha conta com o aviso do link', async () => {
        const { user } = abrir()
        await digitarSenhas(user)
        await user.type(screen.getByLabelText('E-mail pessoal'), 'carlos.lima@gmail.com')
        await user.click(screen.getByRole('button', { name: 'Salvar e entrar' }))

        expect(primeiroAcesso).toHaveBeenCalledWith(expect.objectContaining({ emailRecuperacao: 'carlos.lima@gmail.com' }))
        const destino = await screen.findByTestId('destino')
        expect(destino).toHaveTextContent('/minha-conta')
        expect(stateDoDestino(destino).aviso).toContain('carlos.lima@gmail.com')
    })

    it('mostra o erro de campo devolvido pela API', async () => {
        primeiroAcesso.mockRejectedValue(
            erroApi(400, {
                mensagem: 'Dados inválidos.',
                detalhes: [{ campo: 'novaSenha', erro: 'A nova senha deve ser diferente da senha temporária.' }],
            })
        )
        const { user } = abrir()
        await digitarSenhas(user)
        await user.click(screen.getByRole('button', { name: 'Salvar e entrar' }))

        expect(await screen.findByText('A nova senha deve ser diferente da senha temporária.')).toBeInTheDocument()
    })

    it('"Sair" encerra a sessão', async () => {
        const { user } = abrir()
        await user.click(screen.getByRole('button', { name: 'Sair' }))
        expect(auth.logout).toHaveBeenCalled()
    })
})
