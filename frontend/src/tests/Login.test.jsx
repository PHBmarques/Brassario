import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import Login from '../Pages/Login.jsx'

// Erro de verdade (com .response), como o Axios devolve
const erroApi = (status, data) => Object.assign(new Error(`HTTP ${status}`), { response: { status, data } })
const responde = () => { api.impl = () => Promise.resolve() }
const recusa = (erro) => { api.impl = () => new Promise((_, rejeita) => setTimeout(() => rejeita(erro), 0)) }


const api = vi.hoisted(() => ({ impl: null, chamadas: [] }))
const login = (...args) => {
    api.chamadas.push(args)
    return api.impl(...args)
}
const estado = { isAuthenticated: false, carregando: false }
vi.mock('../Auth/AuthContext.jsx', () => ({ useAuth: () => ({ ...estado, login }) }))
vi.mock('../components/TelaAcesso.jsx', () => ({ default: ({ children }) => <div>{children}</div> }))

function abrir() {
    return render(
        <MemoryRouter initialEntries={['/login']}>
            <Routes>
                <Route path="/login" element={<Login />} />
                <Route path="/" element={<p>Início</p>} />
                <Route path="/esqueci-senha" element={<p>Esqueci</p>} />
            </Routes>
        </MemoryRouter>
    )
}

describe('Login', () => {
    beforeEach(() => {
        api.chamadas.length = 0
        responde()
        estado.isAuthenticated = false
        estado.carregando = false
    })

    it('mostra os erros quando envia vazio e não chama a API', async () => {
        abrir()
        await userEvent.click(screen.getByRole('button', { name: 'Entrar' }))
        expect(screen.getByText('Informe o e-mail.')).toBeInTheDocument()
        expect(screen.getByText('Informe a senha.')).toBeInTheDocument()
        expect(api.chamadas).toHaveLength(0)
    })

    it('recusa e-mail em formato inválido', async () => {
        abrir()
        await userEvent.type(screen.getByLabelText('E-mail'), 'semarroba')
        await userEvent.type(screen.getByLabelText('Senha'), 'qualquer')
        await userEvent.click(screen.getByRole('button', { name: 'Entrar' }))
        expect(screen.getByText('Digite um e-mail válido.')).toBeInTheDocument()
        expect(api.chamadas).toHaveLength(0)
    })

    it('entra com credenciais válidas e vai para o início', async () => {
        responde()
        abrir()
        await userEvent.type(screen.getByLabelText('E-mail'), ' admin@brassario.com ')
        await userEvent.type(screen.getByLabelText('Senha'), 'Admin@2026')
        await userEvent.click(screen.getByRole('button', { name: 'Entrar' }))
        await waitFor(() => expect(screen.getByText('Início')).toBeInTheDocument())
        expect(api.chamadas).toEqual([['admin@brassario.com', 'Admin@2026', false]])
    })

    it('envia manterConectado quando a caixa está marcada', async () => {
        responde()
        abrir()
        await userEvent.type(screen.getByLabelText('E-mail'), 'a@b.com')
        await userEvent.type(screen.getByLabelText('Senha'), 'x')
        await userEvent.click(screen.getByLabelText(/Manter conectado/))
        await userEvent.click(screen.getByRole('button', { name: 'Entrar' }))
        await waitFor(() => expect(api.chamadas).toEqual([['a@b.com', 'x', true]]))
    })

    it('mostra a mensagem de credenciais incorretas no 401', async () => {
        recusa(erroApi(401, { mensagem: 'Credenciais inválidas.' }))
        abrir()
        await userEvent.type(screen.getByLabelText('E-mail'), 'a@b.com')
        await userEvent.type(screen.getByLabelText('Senha'), 'errada')
        await userEvent.click(screen.getByRole('button', { name: 'Entrar' }))
        expect(await screen.findByRole('alert')).toHaveTextContent('E-mail ou senha incorretos')
        expect(screen.getByRole('button', { name: 'Entrar' })).toBeEnabled()
    })

    it('mostra aviso de conexão quando o servidor não responde', async () => {
        recusa(new Error('Network Error'))
        abrir()
        await userEvent.type(screen.getByLabelText('E-mail'), 'a@b.com')
        await userEvent.type(screen.getByLabelText('Senha'), 'x')
        await userEvent.click(screen.getByRole('button', { name: 'Entrar' }))
        expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível conectar ao servidor')
    })

    it('o olhinho alterna entre mostrar e ocultar a senha', async () => {
        abrir()
        const campo = screen.getByLabelText('Senha')
        expect(campo).toHaveAttribute('type', 'password')
        await userEvent.click(screen.getByRole('button', { name: 'Mostrar senha' }))
        expect(campo).toHaveAttribute('type', 'text')
        await userEvent.click(screen.getByRole('button', { name: 'Ocultar senha' }))
        expect(campo).toHaveAttribute('type', 'password')
    })

    it('quem já está logado é levado ao início', () => {
        estado.isAuthenticated = true
        abrir()
        expect(screen.getByText('Início')).toBeInTheDocument()
    })

    it('tem o link "Esqueci a senha"', () => {
        abrir()
        expect(screen.getByRole('link', { name: 'Esqueci a senha' })).toHaveAttribute('href', '/esqueci-senha')
    })
})
