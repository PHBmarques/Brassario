import { describe, it, expect } from 'vitest'
import { FILTROS, botoesDoAlerta, contarPorFiltro, filtrarAlertas, rotuloDoAlerta, textoDoAlerta } from '../Utils/alertasPainel'
import { validarPerda } from '../Utils/perda'

const base = { insumoId: 1, insumoNome: 'Malte', unidadeMedida: 'KG' }
const alertas = [
    { ...base, tipo: 'VENCIDO', codigoLote: 'C-12', diasParaVencer: -2 },
    { ...base, tipo: 'VALIDADE_CRITICA', codigoLote: 'L-88', diasParaVencer: 6, saldo: 0.5 },
    { ...base, tipo: 'ATENCAO', codigoLote: 'L-90', diasParaVencer: 20 },
    { ...base, tipo: 'ESTOQUE_VAZIO', saldo: 0 },
    { ...base, tipo: 'ESTOQUE_BAIXO', saldo: 1.2, estoqueMinimo: 1.5 },
]

describe('filtros da tela de alertas', () => {
    it('conta Todos, Validade e Estoque', () => {
        expect(contarPorFiltro(alertas)).toEqual({ TODOS: 5, VALIDADE: 3, ESTOQUE: 2 })
    })
    it('filtra por grupo', () => {
        expect(filtrarAlertas(alertas, 'ESTOQUE').map((a) => a.tipo)).toEqual(['ESTOQUE_VAZIO', 'ESTOQUE_BAIXO'])
        expect(filtrarAlertas(alertas, 'TODOS')).toHaveLength(5)
        expect(FILTROS.map((f) => f.id)).toEqual(['TODOS', 'VALIDADE', 'ESTOQUE'])
    })
})

describe('textos dos cartões', () => {
    it('lote vencido', () => {
        expect(textoDoAlerta(alertas[0])).toBe('Lote C-12 venceu há 2 dias. Não será usado em produção.')
        expect(rotuloDoAlerta(alertas[0])).toBe('Vencido')
    })
    it('validade crítica e atenção', () => {
        expect(textoDoAlerta(alertas[1])).toContain('Lote L-88 vence em 6 dias. Saldo: ')
        expect(rotuloDoAlerta(alertas[1])).toBe('Validade Crítica')
        expect(rotuloDoAlerta(alertas[2])).toBe('Vence em 20 dias')
    })
    it('estoque', () => {
        expect(textoDoAlerta(alertas[3])).toBe('Não há estoque no Armário.')
        expect(textoDoAlerta(alertas[4])).toMatch(/^Saldo de 1,2 .*, abaixo do mínimo de 1,5 /)
    })
})

describe('botões por tipo', () => {
    const ids = (a) => botoesDoAlerta(a).map((b) => b.id)
    it('cada tipo tem dois botões', () => {
        expect(ids(alertas[0])).toEqual(['LOTES', 'PERDA'])
        expect(ids(alertas[1])).toEqual(['LOTES', 'PERDA'])
        expect(ids(alertas[2])).toEqual(['LOTES', 'RECEITA'])
        expect(ids(alertas[3])).toEqual(['COMPRA', 'ENTRADA'])
        expect(ids(alertas[4])).toEqual(['COMPRA', 'LOTES'])
    })
})

describe('validarPerda', () => {
    it('aceita quantidade dentro do saldo e motivo válido', () => {
        const r = validarPerda({ quantidade: '0,5', motivo: 'lote vencido' }, 0.5)
        expect(r.erros).toEqual({})
        expect(r.quantidade).toBe(0.5)
    })
    it('recusa quantidade maior que o saldo, zero e vazia', () => {
        expect(validarPerda({ quantidade: '2', motivo: 'lote vencido' }, 1).erros.quantidade).toMatch(/maior que o saldo/)
        expect(validarPerda({ quantidade: '0', motivo: 'lote vencido' }, 1).erros.quantidade).toMatch(/maior que zero/)
        expect(validarPerda({ quantidade: '', motivo: 'lote vencido' }, 1).erros.quantidade).toMatch(/Informe/)
    })
    it('recusa motivo curto', () => {
        expect(validarPerda({ quantidade: '1', motivo: 'ab' }, 1).erros.motivo).toMatch(/mínimo de 5/)
    })
})
