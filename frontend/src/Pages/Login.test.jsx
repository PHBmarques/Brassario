import { screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { auth, definirUsuario } from '../test/authFalso.jsx'
import { erroApi, renderTela } from '../test/renderTela.jsx'
import Login from './Login.jsx'

vi.mock('../Auth/AuthContext.jsx', () => import('../test/authFalso.jsx'))

const abrir = () => renderTela(<Login />, { rota: '/login' })

describe('Login', () => {
    beforeEach(() => definirUsuario(null))

    it('mostra os erros quando os campos estão vazios e não chama a API', async () => {
        const { user } = abrir()
        await user.click(screen.getByRole('button', { name: 'Entrar' }))

        expect(screen.getByText('Informe o e-mail.')).toBeInTheDocument()
        expect(screen.getByText('Informe a senha.')).toBeInTheDocument()
        expect(auth.login).not.toHaveBeenCalled()
    })

    it('recusa e-mail em formato inválido', async () => {
        const { user } = abrir()
        await user.type(screen.getByLabelText('E-mail'), 'carlos')
        await user.type(screen.getByLabelText('Senha'), 'qualquer')
        await user.click(screen.getByRole('button', { name: 'Entrar' }))

        expect(screen.getByText('Digite um e-mail válido.')).toBeInTheDocument()
    })

    it('o olhinho mostra e esconde a senha', async () => {
        const { user } = abrir()
        const senha = screen.getByLabelText('Senha')
        expect(senha).toHaveAttribute('type', 'password')

        await user.click(screen.getByRole('button', { name: 'Mostrar senha' }))
        expect(senha).toHaveAttribute('type', 'text')

        await user.click(screen.getByRole('button', { name: 'Ocultar senha' }))
        expect(senha).toHaveAttribute('type', 'password')
    })

    it('entra com e-mail e senha e vai para o início', async () => {
        const { user } = abrir()
        await user.type(screen.getByLabelText('E-mail'), '  carlos@cervejaria.local ')
        await user.type(screen.getByLabelText('Senha'), 'Senha@123')
        await user.click(screen.getByLabelText('Manter conectado neste aparelho'))
        await user.click(screen.getByRole('button', { name: 'Entrar' }))

        expect(auth.login).toHaveBeenCalledWith('carlos@cervejaria.local', 'Senha@123', true)
        expect(await screen.findByTestId('destino')).toHaveTextContent('/')
    })

    it('com credenciais erradas mostra a mensagem amigável', async () => {
        auth.login.mockRejectedValue(erroApi(401, { mensagem: 'E-mail ou senha inválidos.' }))
        const { user } = abrir()
        await user.type(screen.getByLabelText('E-mail'), 'carlos@cervejaria.local')
        await user.type(screen.getByLabelText('Senha'), 'errada')
        await user.click(screen.getByRole('button', { name: 'Entrar' }))

        expect(await screen.findByRole('alert')).toHaveTextContent('E-mail ou senha incorretos. Confira e tente de novo.')
    })

    it('o link "Esqueci a senha" leva para a recuperação', async () => {
        const { user } = abrir()
        await user.click(screen.getByRole('link', { name: 'Esqueci a senha' }))
        expect(await screen.findByTestId('destino')).toHaveTextContent('/esqueci-senha')
    })

    it('quem já está logado vai direto para o início', async () => {
        definirUsuario({ id: 1, nome: 'Carlos' })
        abrir()
        expect(await screen.findByTestId('destino')).toHaveTextContent('/')
    })
})
