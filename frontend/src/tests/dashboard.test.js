import { describe, it, expect } from 'vitest'
import {
    inicioDoMes, consumoPorCategoria, statusDaDuracao, duracaoDoEstoque, custoMedioPorLitro, rendimentoMedio, insumosEmAlerta, litrosProduzidos, nomeDoMes,
} from '../Utils/dashboard'

describe('inicioDoMes', () => {
    it('vai para o dia 1 do mesmo mês', () => {
        expect(inicioDoMes('2026-10-24')).toBe('2026-10-01')
        expect(inicioDoMes('2027-01-31')).toBe('2027-01-01')
    })
})

describe('consumoPorCategoria', () => {
    it('soma por categoria e converte g para kg', () => {
        const linhas = consumoPorCategoria([
            { categoria: 'MALTE', unidadeMedida: 'KG', consumoUltimos30Dias: 10 },
            { categoria: 'MALTE', unidadeMedida: 'G', consumoUltimos30Dias: 500 },
            { categoria: 'LUPULO', unidadeMedida: 'G', consumoUltimos30Dias: 250 },
        ])
        expect(linhas.map((l) => [l.rotulo, l.valor, l.unidade])).toEqual([
            ['Malte', 10.5, 'kg'],
            ['Lúpulo', 0.25, 'kg'],
        ])
    })

    it('a maior coluna vale 100 e as outras são proporcionais (mínimo de 4)', () => {
        const linhas = consumoPorCategoria([
            { categoria: 'MALTE', unidadeMedida: 'KG', consumoUltimos30Dias: 100 },
            { categoria: 'LUPULO', unidadeMedida: 'KG', consumoUltimos30Dias: 50 },
            { categoria: 'LEVEDURA', unidadeMedida: 'KG', consumoUltimos30Dias: 0.1 },
        ])
        expect(linhas.map((l) => l.nivel)).toEqual([100, 50, 4])
    })

    it('ignora quem não consumiu e devolve lista vazia sem consumo', () => {
        expect(consumoPorCategoria([{ categoria: 'MALTE', unidadeMedida: 'KG', consumoUltimos30Dias: 0 }])).toEqual([])
        expect(consumoPorCategoria()).toEqual([])
    })

    it('não mistura unidades diferentes na mesma categoria', () => {
        const linhas = consumoPorCategoria([
            { categoria: 'ADJUNTO', unidadeMedida: 'KG', consumoUltimos30Dias: 2 },
            { categoria: 'ADJUNTO', unidadeMedida: 'L', consumoUltimos30Dias: 3 },
        ])
        expect(linhas.map((l) => l.rotulo).sort()).toEqual(['Adjunto', 'Adjunto (L)'])
    })
})

describe('duração do estoque', () => {
    it('classifica em crítico (até 7), atenção (até 15) e ok', () => {
        expect(statusDaDuracao(2)).toBe('critica')
        expect(statusDaDuracao(7)).toBe('critica')
        expect(statusDaDuracao(8)).toBe('atencao')
        expect(statusDaDuracao(15)).toBe('atencao')
        expect(statusDaDuracao(16)).toBe('ok')
    })

    it('mostra só os 3 mais urgentes, ignora os sem consumo e arredonda os dias para baixo', () => {
        const itens = duracaoDoEstoque([
            { insumoId: 1, insumoNome: 'A', diasRestantes: 20.9 },
            { insumoId: 2, insumoNome: 'B', diasRestantes: null },
            { insumoId: 3, insumoNome: 'C', diasRestantes: 2.4 },
            { insumoId: 4, insumoNome: 'D', diasRestantes: 9 },
            { insumoId: 5, insumoNome: 'E', diasRestantes: 40 },
        ])
        expect(itens.map((i) => [i.nome, i.dias, i.status])).toEqual([
            ['C', 2, 'critica'],
            ['D', 9, 'atencao'],
            ['A', 20, 'ok'],
        ])
    })

    it('a barra representa até 30 dias e nunca passa de 100', () => {
        const [a, b] = duracaoDoEstoque([
            { insumoId: 1, insumoNome: 'A', diasRestantes: 15 },
            { insumoId: 2, insumoNome: 'B', diasRestantes: 90 },
        ])
        expect(a.nivel).toBe(50)
        expect(b.nivel).toBe(100)
    })
})

describe('indicadores do mês', () => {
    it('custo médio por litro = custo total / litros das finalizadas', () => {
        expect(custoMedioPorLitro([
            { quantidade: 100, custoTotal: 400 },
            { quantidade: 50, custoTotal: 250 },
        ])).toBeCloseTo(4.3333, 3)
    })

    it('custo médio é null sem litros', () => {
        expect(custoMedioPorLitro([])).toBeNull()
        expect(custoMedioPorLitro([{ quantidade: 0, custoTotal: 10 }])).toBeNull()
    })

    it('rendimento médio ignora produções sem rendimento', () => {
        expect(rendimentoMedio([{ rendimentoPercentual: 90 }, { rendimentoPercentual: 94 }, { rendimentoPercentual: null }, null])).toBe(92)
        expect(rendimentoMedio([{ rendimentoPercentual: null }])).toBeNull()
        expect(rendimentoMedio()).toBeNull()
    })

    it('conta cada insumo uma vez, mesmo com dois alertas', () => {
        expect(insumosEmAlerta([{ insumoId: 1 }, { insumoId: 1 }, { insumoId: 2 }])).toBe(2)
        expect(insumosEmAlerta()).toBe(0)
    })
})

describe('medidas do painel', () => {
    it('a escala de cada unidade é separada (kg não é comparado com L)', () => {
        const linhas = consumoPorCategoria([
            { categoria: 'MALTE', unidadeMedida: 'KG', consumoUltimos30Dias: 100 },
            { categoria: 'ADJUNTO', unidadeMedida: 'L', consumoUltimos30Dias: 2 },
        ])
        expect(linhas.map((l) => [l.rotulo, l.nivel, l.texto])).toEqual([
            ['Malte', 100, '100 kg'],
            ['Adjunto (L)', 100, '2 L'],
        ])
    })

    it('mostra o saldo com a unidade na duração do estoque', () => {
        const [a] = duracaoDoEstoque([{ insumoId: 1, insumoNome: 'A', diasRestantes: 3, saldoUtilizavel: 0.5, unidadeMedida: 'KG' }])
        expect(a.saldo).toBe('0,5 kg')
    })

    it('soma os litros produzidos e dá o nome do mês', () => {
        expect(litrosProduzidos([{ quantidade: 100 }, { quantidade: 50.5 }])).toBe(150.5)
        expect(litrosProduzidos()).toBe(0)
        expect(nomeDoMes('2026-10-24')).toBe('outubro')
    })
})
