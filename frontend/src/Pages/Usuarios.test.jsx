import { screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { alterarStatusUsuario, listarUsuarios, redefinirSenhaUsuario } from '../Api/usuarios'
import { ADMIN, definirUsuario } from '../test/authFalso.jsx'
import { erroApi, renderTela } from '../test/renderTela.jsx'
import Usuarios from './Usuarios.jsx'

vi.mock('../Api/usuarios')
vi.mock('../Auth/AuthContext.jsx', () => import('../test/authFalso.jsx'))

const LISTA = [
    { id: 1, nome: 'Bernardo Silva', email: 'bernardo@cervejaria.com', perfil: 'ADMIN', ativo: true },
    { id: 3, nome: 'Operador Teste', email: 'operador@brassario.com', perfil: 'OPERADOR', ativo: true },
    { id: 4, nome: 'Ana Souza', email: 'ana@cervejaria.com', perfil: 'OPERADOR', ativo: false },
]

const linha = (nome) => screen.getByText(nome).closest('tr')

async function abrir() {
    const r = renderTela(<Usuarios />, { rota: '/usuarios' })
    await screen.findByText('Operador Teste')
    return r
}

describe('Usuários', () => {
    beforeEach(() => {
        definirUsuario(ADMIN)
        listarUsuarios.mockResolvedValue(LISTA)
        vi.spyOn(window, 'confirm').mockReturnValue(true)
    })

    it('lista os usuários com perfil e situação', async () => {
        await abrir()
        expect(within(linha('Operador Teste')).getByText('Ativo')).toBeInTheDocument()
        expect(within(linha('Ana Souza')).getByText('Inativo')).toBeInTheDocument()
        expect(within(linha('Ana Souza')).getByRole('button', { name: 'Ativar' })).toBeInTheDocument()
        // Inativo não tem "Redefinir senha"
        expect(within(linha('Ana Souza')).queryByRole('button', { name: 'Redefinir senha' })).not.toBeInTheDocument()
    })

    it('a própria linha não tem ações', async () => {
        await abrir()
        const minha = linha('Bernardo Silva')
        expect(within(minha).getByText('(você)')).toBeInTheDocument()
        expect(within(minha).queryByRole('button')).not.toBeInTheDocument()
    })

    it('redefinir senha mostra a senha temporária uma vez', async () => {
        redefinirSenhaUsuario.mockResolvedValue({ senhaTemporaria: 'Kx7p-92Qa' })
        const { user } = await abrir()
        await user.click(within(linha('Operador Teste')).getByRole('button', { name: 'Redefinir senha' }))

        expect(redefinirSenhaUsuario).toHaveBeenCalledWith(3)
        expect(await screen.findByText('Kx7p-92Qa')).toBeInTheDocument()
        expect(screen.getByText(/Senha temporária de Operador Teste/)).toBeInTheDocument()

        await user.click(screen.getByRole('button', { name: 'Copiar' }))
        expect(await screen.findByRole('button', { name: 'Copiada!' })).toBeInTheDocument()

        await user.click(screen.getByRole('button', { name: 'Fechar aviso da senha temporária' }))
        expect(screen.queryByText('Kx7p-92Qa')).not.toBeInTheDocument()
    })

    it('se a pessoa desistir no confirmar, nada acontece', async () => {
        window.confirm.mockReturnValue(false)
        const { user } = await abrir()
        await user.click(within(linha('Operador Teste')).getByRole('button', { name: 'Redefinir senha' }))
        expect(redefinirSenhaUsuario).not.toHaveBeenCalled()
    })

    it('inativar troca a situação na lista', async () => {
        alterarStatusUsuario.mockResolvedValue({ ...LISTA[1], ativo: false })
        const { user } = await abrir()
        await user.click(within(linha('Operador Teste')).getByRole('button', { name: 'Inativar' }))

        expect(alterarStatusUsuario).toHaveBeenCalledWith(3, false)
        expect(await within(linha('Operador Teste')).findByText('Inativo')).toBeInTheDocument()
    })

    it('erro de regra da API aparece no topo', async () => {
        alterarStatusUsuario.mockRejectedValue(erroApi(422, { mensagem: 'Este é o único ADMIN ativo.' }))
        const { user } = await abrir()
        await user.click(within(linha('Ana Souza')).getByRole('button', { name: 'Ativar' }))
        expect(await screen.findByRole('alert')).toHaveTextContent('Este é o único ADMIN ativo.')
    })
})
