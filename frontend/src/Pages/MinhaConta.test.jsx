import { screen, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { alterarEmailRecuperacao } from '../Api/auth'
import { ADMIN, auth, definirUsuario, OPERADOR } from '../test/authFalso.jsx'
import { erroApi, renderTela } from '../test/renderTela.jsx'
import MinhaConta from './MinhaConta.jsx'

vi.mock('../Api/auth')
vi.mock('../Auth/AuthContext.jsx', () => import('../test/authFalso.jsx'))

const abrir = (state) => renderTela(<MinhaConta />, { rota: '/minha-conta', state })

describe('Minha conta', () => {
    it('mostra os dados de acesso', () => {
        definirUsuario(ADMIN)
        abrir()

        expect(screen.getByRole('heading', { name: 'Bernardo Silva' })).toBeInTheDocument()
        expect(screen.getByText('bernardo@cervejaria.com')).toBeInTheDocument()
        expect(screen.getAllByText('Administrador').length).toBeGreaterThan(0)
        expect(screen.getByText('12/10/2026')).toBeInTheDocument()
        expect(screen.getByText('BS')).toBeInTheDocument()
    })

    it('mostra o e-mail de recuperação com o selo de confirmado', () => {
        definirUsuario(ADMIN)
        abrir()
        expect(screen.getByText('bernardo@gmail.com')).toBeInTheDocument()
        expect(screen.getByText('Confirmado')).toBeInTheDocument()
    })

    it('sem e-mail de recuperação oferece cadastrar', () => {
        definirUsuario(OPERADOR)
        abrir()
        expect(screen.getByText('Nenhum e-mail cadastrado')).toBeInTheDocument()
        expect(screen.getByRole('button', { name: 'Cadastrar e-mail' })).toBeInTheDocument()
    })

    it('mostra o aviso vindo de outra tela', () => {
        definirUsuario(OPERADOR)
        abrir({ aviso: 'Senha alterada.' })
        expect(screen.getByText('Senha alterada.')).toBeInTheDocument()
    })

    it('troca o e-mail de recuperação e atualiza o usuário', async () => {
        definirUsuario(OPERADOR)
        alterarEmailRecuperacao.mockResolvedValue({ mensagem: 'Enviamos um link de confirmação para o novo e-mail.' })
        const { user } = abrir()

        await user.click(screen.getByRole('button', { name: 'Cadastrar e-mail' }))
        await user.type(screen.getByLabelText('E-mail de recuperação'), 'carlos.lima@gmail.com')
        await user.click(screen.getByRole('button', { name: 'Salvar e enviar link' }))

        expect(alterarEmailRecuperacao).toHaveBeenCalledWith('carlos.lima@gmail.com')
        expect(await screen.findByText('Enviamos um link de confirmação para o novo e-mail.')).toBeInTheDocument()
        expect(auth.recarregarUsuario).toHaveBeenCalled()
    })

    it('não deixa usar o e-mail de login como recuperação', async () => {
        definirUsuario(OPERADOR)
        const { user } = abrir()
        await user.click(screen.getByRole('button', { name: 'Cadastrar e-mail' }))
        await user.type(screen.getByLabelText('E-mail de recuperação'), OPERADOR.email)
        await user.click(screen.getByRole('button', { name: 'Salvar e enviar link' }))

        expect(screen.getByText('Use um e-mail diferente do e-mail de login.')).toBeInTheDocument()
        expect(alterarEmailRecuperacao).not.toHaveBeenCalled()
    })

    it('mostra o erro do campo vindo da API', async () => {
        definirUsuario(OPERADOR)
        alterarEmailRecuperacao.mockRejectedValue(
            erroApi(400, { detalhes: [{ campo: 'emailRecuperacao', erro: 'Informe um e-mail válido.' }] })
        )
        const { user } = abrir()
        await user.click(screen.getByRole('button', { name: 'Cadastrar e-mail' }))
        await user.type(screen.getByLabelText('E-mail de recuperação'), 'a@b.co')
        await user.click(screen.getByRole('button', { name: 'Salvar e enviar link' }))

        expect(await screen.findByText('Informe um e-mail válido.')).toBeInTheDocument()
    })

    it('o botão de segurança leva para Alterar minha senha', () => {
        definirUsuario(OPERADOR)
        abrir()
        const seguranca = screen.getByRole('heading', { name: 'Segurança' }).parentElement
        expect(within(seguranca).getByRole('link', { name: 'Alterar minha senha' })).toHaveAttribute('href', '/alterar-senha')
    })
})
