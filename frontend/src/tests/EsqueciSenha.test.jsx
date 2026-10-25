import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import EsqueciSenha from '../Pages/EsqueciSenha.jsx'

// Função simples (sem vi.fn) para poder simular rejeições sem o executor tratá-las como "não capturadas"
const api = vi.hoisted(() => ({ impl: null, chamadas: [] }))
vi.mock('../Api/auth', () => ({
    esqueciSenha: (...args) => {
        api.chamadas.push(args)
        return api.impl(...args)
    },
}))
vi.mock('../components/TelaAcesso.jsx', () => ({ default: ({ children }) => <div>{children}</div> }))

const erroApi = (status, data) => Object.assign(new Error(`HTTP ${status}`), { response: { status, data } })
const responde = (valor) => { api.impl = () => Promise.resolve(valor) }
const recusa = (erro) => { api.impl = () => new Promise((_, rejeita) => setTimeout(() => rejeita(erro), 0)) }

const abrir = () => render(<MemoryRouter><EsqueciSenha /></MemoryRouter>)

describe('EsqueciSenha', () => {
    beforeEach(() => {
        api.chamadas.length = 0
        responde({})
    })

    it('pede o e-mail quando vazio', async () => {
        abrir()
        await userEvent.click(screen.getByRole('button', { name: 'Enviar link' }))
        expect(screen.getByText('Informe o e-mail.')).toBeInTheDocument()
        expect(api.chamadas).toHaveLength(0)
    })

    it('recusa e-mail inválido', async () => {
        abrir()
        await userEvent.type(screen.getByLabelText('E-mail de login'), 'abc')
        await userEvent.click(screen.getByRole('button', { name: 'Enviar link' }))
        expect(screen.getByText('Digite um e-mail válido.')).toBeInTheDocument()
    })

    it('mostra a confirmação com a mensagem da API', async () => {
        responde({ mensagem: 'Se a conta existir, enviamos o link.' })
        abrir()
        await userEvent.type(screen.getByLabelText('E-mail de login'), 'admin@brassario.com')
        await userEvent.click(screen.getByRole('button', { name: 'Enviar link' }))
        expect(await screen.findByRole('status')).toHaveTextContent('Se a conta existir, enviamos o link.')
        expect(api.chamadas).toEqual([['admin@brassario.com']])
        expect(screen.getByRole('link', { name: 'Voltar para o login' })).toBeInTheDocument()
    })

    it('mostra o erro quando a API recusa (429)', async () => {
        recusa(erroApi(429, { mensagem: 'Muitas tentativas. Aguarde.' }))
        abrir()
        await userEvent.type(screen.getByLabelText('E-mail de login'), 'admin@brassario.com')
        await userEvent.click(screen.getByRole('button', { name: 'Enviar link' }))
        expect(await screen.findByRole('alert')).toHaveTextContent('Muitas tentativas. Aguarde.')
    })
})
