import { describe, it, expect } from 'vitest'
import { formatarReais, normalizarTexto, formatarDataHora, formatarLitros, STATUS_BATELADA } from '../Utils/formato'

const semNbsp = (s) => s.replace(/ /g, ' ')

describe('formato', () => {
    it('formata valores em reais', () => {
        expect(semNbsp(formatarReais(1234.5))).toBe('R$ 1.234,50')
        expect(semNbsp(formatarReais(0))).toBe('R$ 0,00')
    })

    it('trata valor ausente como zero', () => {
        expect(semNbsp(formatarReais(null))).toBe('R$ 0,00')
        expect(semNbsp(formatarReais(undefined))).toBe('R$ 0,00')
    })

    it('normaliza texto: sem acento, sem maiúscula e sem espaço sobrando', () => {
        expect(normalizarTexto('  Lúpulo Cascade ')).toBe('lupulo cascade')
        expect(normalizarTexto('Lúpulo')).toBe(normalizarTexto('lupulo'))
        expect(normalizarTexto(null)).toBe('')
    })

    it('formata data e hora da API', () => {
        expect(formatarDataHora('2026-10-20T14:03:00Z')).toBe('20/10/2026')
        expect(formatarDataHora('2026-10-20T14:03:00Z', true)).toBe('20/10')
        expect(formatarDataHora(null)).toBe('—')
    })

    it('formata litros com até duas casas', () => {
        expect(semNbsp(formatarLitros(1234.5))).toBe('1.234,5 L')
        expect(semNbsp(formatarLitros(100))).toBe('100 L')
        expect(semNbsp(formatarLitros(undefined))).toBe('0 L')
    })

    it('traz os três status da produção com o tipo de etiqueta certo', () => {
        expect(STATUS_BATELADA.EM_ANDAMENTO).toEqual({ rotulo: 'Em andamento', tipo: 'atencao' })
        expect(STATUS_BATELADA.FINALIZADA.tipo).toBe('ok')
        expect(STATUS_BATELADA.CANCELADA.tipo).toBe('critica')
    })
})
