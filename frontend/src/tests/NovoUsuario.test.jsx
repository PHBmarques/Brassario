import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom'
import NovoUsuario from '../Pages/NovoUsuario.jsx'

// Função simples (sem vi.fn) para poder simular falhas sem o executor tratá-las como "não capturadas"
const api = vi.hoisted(() => ({ impl: null, chamadas: [] }))
vi.mock('../Api/usuarios', () => ({
    criarUsuario: (dados) => {
        api.chamadas.push(dados)
        return api.impl(dados)
    },
}))

const erroApi = (status, data) => Object.assign(new Error(`HTTP ${status}`), { response: { status, data } })
const responde = (valor) => { api.impl = () => Promise.resolve(valor) }
const recusa = (erro) => { api.impl = () => new Promise((_, rejeita) => setTimeout(() => rejeita(erro), 0)) }

// Mostra o que a lista recebeu do cadastro
function ListaFalsa() {
    const { state } = useLocation()
    return <p>Lista de usuários. Senha: {state?.senhaGerada?.senha} de {state?.senhaGerada?.nome}</p>
}

const abrir = () =>
    render(
        <MemoryRouter initialEntries={['/usuarios/novo']}>
            <Routes>
                <Route path="/usuarios" element={<ListaFalsa />} />
                <Route path="/usuarios/novo" element={<NovoUsuario />} />
            </Routes>
        </MemoryRouter>
    )

describe('NovoUsuario', () => {
    beforeEach(() => {
        api.chamadas.length = 0
        responde({ id: 9, nome: 'Carlos Lima' })
    })

    it('mostra o formulário com perfil OPERADOR e senha temporária já gerada', () => {
        abrir()
        expect(screen.getByRole('heading', { name: 'Novo usuário' })).toBeInTheDocument()
        expect(screen.getByRole('link', { name: '‹ Voltar aos usuários' })).toHaveAttribute('href', '/usuarios')
        expect(screen.getByLabelText('Perfil *')).toHaveValue('OPERADOR')
        expect(screen.getByLabelText('Senha temporária *').value).toMatch(/^[a-zA-Z2-9]{4}-[a-zA-Z2-9]{4}$/)
        expect(screen.getByText(/O operador faz produções/)).toBeInTheDocument()
    })

    it('a dica do perfil muda ao escolher ADMIN', async () => {
        abrir()
        await userEvent.selectOptions(screen.getByLabelText('Perfil *'), 'ADMIN')
        expect(screen.getByText(/O administrador pode tudo/)).toBeInTheDocument()
    })

    it('"Gerar outra" troca a senha temporária', async () => {
        abrir()
        const campo = screen.getByLabelText('Senha temporária *')
        const antes = campo.value
        await userEvent.click(screen.getByRole('button', { name: 'Gerar outra' }))
        expect(campo.value).not.toBe(antes)
    })

    it('mostra os erros e não chama a API com campos vazios', async () => {
        abrir()
        await userEvent.click(screen.getByRole('button', { name: 'Cadastrar usuário' }))
        expect(screen.getByText('Informe o nome.')).toBeInTheDocument()
        expect(screen.getByText('Informe o e-mail.')).toBeInTheDocument()
        expect(api.chamadas).toHaveLength(0)
    })

    it('cadastra, volta para a lista e entrega a senha para o aviso', async () => {
        abrir()
        await userEvent.type(screen.getByLabelText('Nome *'), '  Carlos Lima ')
        await userEvent.type(screen.getByLabelText('E-mail *'), 'carlos@cervejaria.local')
        const senha = screen.getByLabelText('Senha temporária *').value
        await userEvent.click(screen.getByRole('button', { name: 'Cadastrar usuário' }))

        expect(await screen.findByText(`Lista de usuários. Senha: ${senha} de Carlos Lima`)).toBeInTheDocument()
        expect(api.chamadas).toEqual([
            { nome: 'Carlos Lima', email: 'carlos@cervejaria.local', perfil: 'OPERADOR', senha },
        ])
    })

    it('se a API recusar (e-mail repetido), mostra o motivo e continua na tela', async () => {
        recusa(erroApi(409, { mensagem: 'Já existe um usuário com este e-mail.' }))
        abrir()
        await userEvent.type(screen.getByLabelText('Nome *'), 'Carlos Lima')
        await userEvent.type(screen.getByLabelText('E-mail *'), 'carlos@cervejaria.local')
        await userEvent.click(screen.getByRole('button', { name: 'Cadastrar usuário' }))
        expect(await screen.findByRole('alert')).toHaveTextContent('Já existe um usuário com este e-mail.')
        expect(screen.getByRole('heading', { name: 'Novo usuário' })).toBeInTheDocument()
        expect(screen.getByRole('button', { name: 'Cadastrar usuário' })).toBeEnabled()
    })

    it('Cancelar volta para a lista sem cadastrar', async () => {
        abrir()
        await userEvent.click(screen.getByRole('link', { name: 'Cancelar' }))
        expect(await screen.findByText(/Lista de usuários/)).toBeInTheDocument()
        expect(api.chamadas).toHaveLength(0)
    })
})
