import { describe, it, expect } from 'vitest'
import { PERFIS, emailSugerido, novoFormulario, validarCadastro } from '../Utils/cadastroUsuario'

const valido = { nome: 'Carlos Lima', email: 'carlos@cervejaria.local', perfil: 'OPERADOR', senha: 'Cerv-4829' }

describe('validarCadastro', () => {
    it('aceita um cadastro completo', () => {
        expect(validarCadastro(valido)).toEqual({})
    })

    it('pede nome, e-mail e senha quando vazios', () => {
        expect(validarCadastro({ ...valido, nome: '  ', email: '', senha: '' })).toEqual({
            nome: 'Informe o nome.',
            email: 'Informe o e-mail.',
            senha: 'Informe a senha temporária.',
        })
    })

    it('recusa e-mail inválido', () => {
        expect(validarCadastro({ ...valido, email: 'carlos@' }).email).toBe('Digite um e-mail válido.')
    })

    it('exige o mínimo de caracteres na senha temporária', () => {
        expect(validarCadastro({ ...valido, senha: 'abc' }).senha).toBe('Mínimo de 8 caracteres.')
        expect(validarCadastro({ ...valido, senha: 'abcdefgh' }).senha).toBeUndefined()
    })
})

describe('novoFormulario e perfis', () => {
    it('começa como OPERADOR, com senha temporária já gerada', () => {
        const f = novoFormulario()
        expect(f.perfil).toBe('OPERADOR')
        expect(f.nome).toBe('')
        expect(f.senha).toMatch(/^[a-zA-Z2-9]{4}-[a-zA-Z2-9]{4}$/)
    })

    it('oferece OPERADOR e ADMIN, cada um com uma dica', () => {
        expect(PERFIS.map((p) => p.valor)).toEqual(['OPERADOR', 'ADMIN'])
        expect(PERFIS.every((p) => p.dica.length > 10)).toBe(true)
    })
})

describe('emailSugerido', () => {
    it('monta nome.sobrenome@brassario.com', () => {
        expect(emailSugerido('Carlos Lima')).toBe('carlos.lima@brassario.com')
    })

    it('tira acentos, espaços sobrando e símbolos', () => {
        expect(emailSugerido('  João  Víctor ')).toBe('joao.victor@brassario.com')
        expect(emailSugerido("O'Neil")).toBe('o.neil@brassario.com')
    })

    it('fica vazio quando não há nome', () => {
        expect(emailSugerido('')).toBe('')
        expect(emailSugerido('   ')).toBe('')
    })

    it('gera um e-mail aceito pela validação do formulário', () => {
        const f = { nome: 'Maria da Silva', email: emailSugerido('Maria da Silva'), perfil: 'OPERADOR', senha: 'Cerv-4829' }
        expect(validarCadastro(f)).toEqual({})
    })
})
