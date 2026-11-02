import { describe, it, expect } from 'vitest'
import { mensagemDeErro, errosPorCampo } from '../Api/erros'

const erro = (status, data) => ({ response: { status, data } })

describe('mensagemDeErro', () => {
    it('avisa quando não há resposta do servidor', () => {
        expect(mensagemDeErro({})).toMatch(/Não foi possível conectar ao servidor/)
    })

    it('usa a mensagem da API quando existe (401, 404, 422)', () => {
        expect(mensagemDeErro(erro(401, { mensagem: 'Credenciais inválidas.' }))).toBe('Credenciais inválidas.')
        expect(mensagemDeErro(erro(404, { mensagem: 'Insumo não encontrado.' }))).toBe('Insumo não encontrado.')
        expect(mensagemDeErro(erro(422, { mensagem: 'Estoque insuficiente.' }))).toBe('Estoque insuficiente.')
    })

    it('tem texto padrão quando a API não manda mensagem', () => {
        expect(mensagemDeErro(erro(401))).toBe('Sessão expirada ou credenciais inválidas.')
        expect(mensagemDeErro(erro(404))).toBe('Item não encontrado.')
        expect(mensagemDeErro(erro(400))).toBe('Não foi possível concluir a operação.')
    })

    it('403 vira falta de permissão', () => {
        expect(mensagemDeErro(erro(403, { mensagem: 'x' }))).toBe('Você não tem permissão para fazer isso.')
    })

    it('500 não vaza detalhes do servidor', () => {
        expect(mensagemDeErro(erro(500, { mensagem: 'NullReference em Foo.cs' }))).toBe(
            'Ocorreu um erro no servidor. Tente novamente em instantes.'
        )
    })
})

describe('errosPorCampo', () => {
    it('transforma os detalhes em um objeto por campo', () => {
        const e = erro(400, { detalhes: [{ campo: 'email', erro: 'E-mail inválido' }, { campo: 'senha', erro: 'Obrigatória' }] })
        expect(errosPorCampo(e)).toEqual({ email: 'E-mail inválido', senha: 'Obrigatória' })
    })

    it('ignora detalhes sem campo e respostas sem detalhes', () => {
        expect(errosPorCampo(erro(400, { detalhes: [{ erro: 'geral' }] }))).toEqual({})
        expect(errosPorCampo(erro(400, {}))).toEqual({})
        expect(errosPorCampo({})).toEqual({})
    })
})
