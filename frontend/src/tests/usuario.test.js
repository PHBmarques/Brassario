import { describe, it, expect } from 'vitest'
import { iniciais } from '../Utils/usuario'

describe('iniciais', () => {
    it('usa a primeira letra do primeiro e do último nome', () => {
        expect(iniciais('Carlos Lima')).toBe('CL')
        expect(iniciais('Maria da Silva Souza')).toBe('MS')
    })

    it('usa só uma letra para nome único', () => {
        expect(iniciais('admin')).toBe('A')
    })

    it('devolve ? para nome vazio ou ausente', () => {
        expect(iniciais('')).toBe('?')
        expect(iniciais('   ')).toBe('?')
        expect(iniciais()).toBe('?')
        expect(iniciais(null)).toBe('?')
    })
})
