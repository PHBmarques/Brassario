import { describe, it, expect } from 'vitest'
import { validarNovaSenha, EMAIL_REGEX, MINIMO_SENHA, gerarSenhaTemporaria } from '../Utils/senha'

describe('validarNovaSenha', () => {
    it('aceita senha válida repetida igual', () => {
        expect(validarNovaSenha({ novaSenha: 'Senha@123', confirmarSenha: 'Senha@123' })).toEqual({})
    })

    it('pede a nova senha e a confirmação quando estão vazias', () => {
        const erros = validarNovaSenha({ novaSenha: '', confirmarSenha: '' })
        expect(erros.novaSenha).toBe('Informe a nova senha.')
        expect(erros.confirmarSenha).toBe('Repita a nova senha.')
    })

    it('recusa senha menor que o mínimo', () => {
        const erros = validarNovaSenha({ novaSenha: 'abc', confirmarSenha: 'abc' })
        expect(erros.novaSenha).toContain(String(MINIMO_SENHA))
    })

    it('aceita exatamente o mínimo de caracteres', () => {
        const senha = 'a'.repeat(MINIMO_SENHA)
        expect(validarNovaSenha({ novaSenha: senha, confirmarSenha: senha }).novaSenha).toBeUndefined()
    })

    it('recusa confirmação diferente', () => {
        const erros = validarNovaSenha({ novaSenha: 'Senha@123', confirmarSenha: 'Senha@124' })
        expect(erros.confirmarSenha).toBe('As senhas não são iguais.')
    })
})

describe('EMAIL_REGEX', () => {
    it.each(['admin@brassario.com', 'a.b@c.co'])('aceita %s', (email) => {
        expect(EMAIL_REGEX.test(email)).toBe(true)
    })

    it.each(['', 'semarroba.com', 'a@b', 'a b@c.com', '@c.com'])('recusa "%s"', (email) => {
        expect(EMAIL_REGEX.test(email)).toBe(false)
    })
})

describe('gerarSenhaTemporaria', () => {
    it('gera no formato XXXX-XXXX sem caracteres ambíguos', () => {
        for (let i = 0; i < 50; i++) {
            const senha = gerarSenhaTemporaria()
            expect(senha).toMatch(/^[a-zA-Z2-9]{4}-[a-zA-Z2-9]{4}$/)
            expect(senha).not.toMatch(/[0O1lI]/)
        }
    })

    it('gera senhas diferentes a cada chamada', () => {
        const senhas = new Set(Array.from({ length: 20 }, gerarSenhaTemporaria))
        expect(senhas.size).toBeGreaterThan(1)
    })
})
