import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import {
    rotuloCategoria, rotuloUnidade, formatarQuantidade, formatarData, hojeIso, diasAte,
    lerNumero, tipoPorCor, corPorDias, nivelDoSaldo, situacaoDoItem,
} from '../Utils/armario'

describe('rótulos', () => {
    it('traduz categoria e unidade, e devolve o valor quando não conhece', () => {
        expect(rotuloCategoria('LUPULO')).toBe('Lúpulo')
        expect(rotuloUnidade('KG')).toBe('kg')
        expect(rotuloCategoria('XYZ')).toBe('XYZ')
        expect(rotuloUnidade('XYZ')).toBe('XYZ')
    })

    it('formata quantidade com a unidade', () => {
        expect(formatarQuantidade(1.5, 'KG').replace(/ /g, ' ')).toBe('1,5 kg')
    })
})

describe('formatarData', () => {
    it('inverte AAAA-MM-DD sem depender do fuso', () => {
        expect(formatarData('2027-06-30')).toBe('30/06/2027')
        expect(formatarData(null)).toBe('—')
    })
})

describe('datas relativas a hoje', () => {
    beforeEach(() => {
        vi.useFakeTimers()
        vi.setSystemTime(new Date(2026, 9, 24, 12, 0, 0)) // 24/10/2026, hora local
    })
    afterEach(() => vi.useRealTimers())

    it('hojeIso devolve a data local no formato da API', () => {
        expect(hojeIso()).toBe('2026-10-24')
    })

    it('diasAte conta dias a partir de hoje, negativo se já passou', () => {
        expect(diasAte('2026-10-24')).toBe(0)
        expect(diasAte('2026-10-30')).toBe(6)
        expect(diasAte('2026-10-23')).toBe(-1)
        expect(diasAte('2026-11-18')).toBe(25)
    })
})

describe('lerNumero', () => {
    it('aceita vírgula e ponto como decimal', () => {
        expect(lerNumero('1,5')).toBe(1.5)
        expect(lerNumero('1.5')).toBe(1.5)
        expect(lerNumero(' 12 ')).toBe(12)
    })

    it('devolve null para vazio e NaN para texto', () => {
        expect(lerNumero('')).toBeNull()
        expect(lerNumero(null)).toBeNull()
        expect(lerNumero('abc')).toBeNaN()
    })
})

describe('cores da validade', () => {
    it('mapeia cor para tipo de etiqueta', () => {
        expect(tipoPorCor('VERDE')).toBe('ok')
        expect(tipoPorCor('AMARELO')).toBe('atencao')
        expect(tipoPorCor('VERMELHO')).toBe('critica')
        expect(tipoPorCor('OUTRA')).toBe('ok')
    })

    it('crítico tem prioridade sobre atenção (limites 15 e 45 dias)', () => {
        expect(corPorDias(-1, 15, 45)).toBe('VERMELHO')
        expect(corPorDias(15, 15, 45)).toBe('VERMELHO')
        expect(corPorDias(16, 15, 45)).toBe('AMARELO')
        expect(corPorDias(45, 15, 45)).toBe('AMARELO')
        expect(corPorDias(46, 15, 45)).toBe('VERDE')
    })
})

describe('nivelDoSaldo', () => {
    it('é 0 sem saldo e 100 sem estoque mínimo', () => {
        expect(nivelDoSaldo(0, 10)).toBe(0)
        expect(nivelDoSaldo(5, 0)).toBe(100)
    })

    it('metade da barra é o estoque mínimo e a barra trava em 100', () => {
        expect(nivelDoSaldo(10, 10)).toBe(50)
        expect(nivelDoSaldo(100, 10)).toBe(100)
    })

    it('nunca fica abaixo de 4% quando há saldo', () => {
        expect(nivelDoSaldo(0.01, 100)).toBe(4)
    })
})

describe('situacaoDoItem', () => {
    it('mostra "Armário vazio" quando não há saldo', () => {
        expect(situacaoDoItem({ saldo: 0, statusEscassez: 'VAZIO' })).toMatchObject({
            barra: 'vazio', etiqueta: 'critica', texto: 'Armário vazio', nivel: 0,
        })
    })

    it('mostra validade crítica com singular e plural', () => {
        const base = { saldo: 5, cor: 'VERMELHO', estoqueMinimo: 2 }
        expect(situacaoDoItem({ ...base, diasParaVencer: 1 }).texto).toBe('Validade Crítica: 1 dia')
        expect(situacaoDoItem({ ...base, diasParaVencer: 6 }).texto).toBe('Validade Crítica: 6 dias')
    })

    it('mostra atenção e ok', () => {
        expect(situacaoDoItem({ saldo: 5, cor: 'AMARELO', diasParaVencer: 25, estoqueMinimo: 2 }).texto)
            .toBe('Atenção: vence em 25 dias')
        expect(situacaoDoItem({ saldo: 5, cor: 'VERDE', diasParaVencer: 90, estoqueMinimo: 2 }).etiqueta).toBe('ok')
    })
})
