import { screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { esqueciSenha } from '../Api/auth'
import { erroApi, renderTela } from '../test/renderTela.jsx'
import EsqueciSenha from './EsqueciSenha.jsx'

vi.mock('../Api/auth')

const abrir = () => renderTela(<EsqueciSenha />, { rota: '/esqueci-senha' })

describe('Esqueci a senha', () => {
    it('pede o e-mail antes de enviar', async () => {
        const { user } = abrir()
        await user.click(screen.getByRole('button', { name: 'Enviar link' }))

        expect(screen.getByText('Informe o e-mail.')).toBeInTheDocument()
        expect(esqueciSenha).not.toHaveBeenCalled()
    })

    it('recusa e-mail inválido', async () => {
        const { user } = abrir()
        await user.type(screen.getByLabelText('E-mail de login'), 'carlos@')
        await user.click(screen.getByRole('button', { name: 'Enviar link' }))

        expect(screen.getByText('Digite um e-mail válido.')).toBeInTheDocument()
    })

    it('envia o e-mail de login e mostra a mensagem da API', async () => {
        esqueciSenha.mockResolvedValue({ mensagem: 'Se existir uma conta, enviamos um link.' })
        const { user } = abrir()
        await user.type(screen.getByLabelText('E-mail de login'), ' carlos@cervejaria.local ')
        await user.click(screen.getByRole('button', { name: 'Enviar link' }))

        expect(esqueciSenha).toHaveBeenCalledWith('carlos@cervejaria.local')
        expect(await screen.findByText('Confira seu e-mail')).toBeInTheDocument()
        expect(screen.getByRole('status')).toHaveTextContent('Se existir uma conta, enviamos um link.')
        expect(screen.getByRole('status')).toHaveTextContent('30 minutos')
    })

    it('mostra o aviso quando a API limita os pedidos (429)', async () => {
        esqueciSenha.mockRejectedValue(erroApi(429, { mensagem: 'Muitas tentativas. Aguarde um pouco e tente novamente.' }))
        const { user } = abrir()
        await user.type(screen.getByLabelText('E-mail de login'), 'carlos@cervejaria.local')
        await user.click(screen.getByRole('button', { name: 'Enviar link' }))

        expect(await screen.findByRole('alert')).toHaveTextContent('Muitas tentativas')
    })
})
