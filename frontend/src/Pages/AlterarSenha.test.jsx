import { screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { alterarSenha } from '../Api/auth'
import { definirUsuario, OPERADOR } from '../test/authFalso.jsx'
import { erroApi, renderTela, stateDoDestino } from '../test/renderTela.jsx'
import AlterarSenha from './AlterarSenha.jsx'

vi.mock('../Api/auth')
vi.mock('../Auth/AuthContext.jsx', () => import('../test/authFalso.jsx'))

const abrir = () => renderTela(<AlterarSenha />, { rota: '/alterar-senha' })

async function preencher(user, atual, nova, confirmar = nova) {
    if (atual) await user.type(screen.getByLabelText('Senha atual *'), atual)
    if (nova) await user.type(screen.getByLabelText('Nova senha *'), nova)
    if (confirmar) await user.type(screen.getByLabelText('Confirmar nova senha *'), confirmar)
    await user.click(screen.getByRole('button', { name: 'Salvar senha' }))
}

describe('Alterar minha senha', () => {
    beforeEach(() => definirUsuario(OPERADOR))

    it('mostra a conta e todos os campos com olhinho', () => {
        abrir()
        expect(screen.getByText(`Conta: ${OPERADOR.email}`)).toBeInTheDocument()
        expect(screen.getAllByRole('button', { name: 'Mostrar senha' })).toHaveLength(3)
    })

    it('exige a senha atual', async () => {
        const { user } = abrir()
        await preencher(user, '', 'NovaSenha@1')
        expect(screen.getByText('Informe a senha atual.')).toBeInTheDocument()
        expect(alterarSenha).not.toHaveBeenCalled()
    })

    it('a nova senha precisa ser diferente da atual', async () => {
        const { user } = abrir()
        await preencher(user, 'MesmaSenha@1', 'MesmaSenha@1')
        expect(screen.getByText('A nova senha deve ser diferente da atual.')).toBeInTheDocument()
    })

    it('senha atual errada aparece no campo', async () => {
        alterarSenha.mockRejectedValue(erroApi(422, { codigo: 'SENHA_ATUAL_INCORRETA', mensagem: 'A senha atual está incorreta.' }))
        const { user } = abrir()
        await preencher(user, 'Errada@123', 'NovaSenha@1')

        expect(await screen.findByText('A senha atual está incorreta.')).toBeInTheDocument()
        expect(screen.getByLabelText('Senha atual *')).toHaveAttribute('aria-invalid', 'true')
    })

    it('salva e volta para Minha conta com o aviso', async () => {
        alterarSenha.mockResolvedValue({})
        const { user } = abrir()
        await preencher(user, 'Atual@1234', 'NovaSenha@1')

        expect(alterarSenha).toHaveBeenCalledWith({
            senhaAtual: 'Atual@1234',
            novaSenha: 'NovaSenha@1',
            confirmarSenha: 'NovaSenha@1',
        })
        const destino = await screen.findByTestId('destino')
        expect(destino).toHaveTextContent('/minha-conta')
        expect(stateDoDestino(destino).aviso).toMatch(/Senha alterada/)
    })
})
