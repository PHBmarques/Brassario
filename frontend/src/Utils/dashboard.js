import { formatarNumero, formatarQuantidade, rotuloCategoria } from './armario'

// Cálculos da tela Início. A API entrega os dados por insumo; aqui eles viram o que a tela mostra.

const FATOR_BASE = {
    KG: { base: 'kg', fator: 1 },
    G: { base: 'kg', fator: 0.001 },
    L: { base: 'L', fator: 1 },
    ML: { base: 'L', fator: 0.001 },
    UN: { base: 'un', fator: 1 },
}

// "2026-10-24" -> "2026-10-01"
export const inicioDoMes = (iso) => `${iso.slice(0, 7)}-01`

// Soma o consumo por categoria, convertendo g para kg e mL para L. Só entram categorias com consumo.
// nivel: 0 a 100, em relação à maior coluna.
export function consumoPorCategoria(consumo = []) {
    const grupos = new Map()
    for (const c of consumo) {
        const { base, fator } = FATOR_BASE[c.unidadeMedida] ?? { base: c.unidadeMedida, fator: 1 }
        const chave = `${c.categoria}|${base}`
        const atual = grupos.get(chave) ?? { categoria: c.categoria, unidade: base, valor: 0 }
        atual.valor += Number(c.consumoUltimos30Dias ?? 0) * fator
        grupos.set(chave, atual)
    }
    const linhas = [...grupos.values()].filter((g) => g.valor > 0)
    // A escala é separada por unidade: kg, L e un nunca são comparados entre si
    const maiorPorUnidade = new Map()
    for (const g of linhas) maiorPorUnidade.set(g.unidade, Math.max(maiorPorUnidade.get(g.unidade) ?? 0, g.valor))
    return linhas
        .map((g) => {
            const valor = Math.round(g.valor * 100) / 100
            return {
                rotulo: g.unidade === 'kg' ? rotuloCategoria(g.categoria) : `${rotuloCategoria(g.categoria)} (${g.unidade})`,
                valor,
                unidade: g.unidade,
                texto: `${formatarNumero(valor)} ${g.unidade}`,
                nivel: Math.max(4, Math.round((g.valor / maiorPorUnidade.get(g.unidade)) * 100)),
            }
        })
        .sort((a, b) => b.valor - a.valor)
}

// Status da barra de duração: até 7 dias é crítico, até 15 é atenção (15 é o limite das compras sugeridas)
export function statusDaDuracao(dias) {
    if (dias <= 7) return 'critica'
    if (dias <= 15) return 'atencao'
    return 'ok'
}

// Os insumos que mais urgem, só os que têm consumo para estimar (dias != null)
export function duracaoDoEstoque(previsao = [], limite = 3) {
    return previsao
        .filter((p) => p.diasRestantes != null)
        .slice()
        .sort((a, b) => a.diasRestantes - b.diasRestantes)
        .slice(0, limite)
        .map((p) => ({
            id: p.insumoId,
            nome: p.insumoNome,
            saldo: formatarQuantidade(p.saldoUtilizavel, p.unidadeMedida),
            dias: Math.floor(p.diasRestantes),
            nivel: Math.max(4, Math.min(100, Math.round((p.diasRestantes / 30) * 100))),
            status: statusDaDuracao(p.diasRestantes),
        }))
}

// Custo total dividido pelos litros das produções finalizadas. null se não houver litros.
export function custoMedioPorLitro(finalizadas = []) {
    const litros = finalizadas.reduce((soma, p) => soma + Number(p.quantidade ?? 0), 0)
    if (litros <= 0) return null
    const custo = finalizadas.reduce((soma, p) => soma + Number(p.custoTotal ?? 0), 0)
    return custo / litros
}

// Média do rendimento (%) das produções que já têm volume final. null se nenhuma tiver.
export function rendimentoMedio(detalhes = []) {
    const valores = detalhes.map((d) => d?.rendimentoPercentual).filter((v) => v != null)
    if (valores.length === 0) return null
    return valores.reduce((soma, v) => soma + Number(v), 0) / valores.length
}

// Quantos insumos diferentes têm alerta (um insumo com dois alertas conta uma vez)
export const insumosEmAlerta = (alertas = []) => new Set(alertas.map((a) => a.insumoId)).size

// Litros das produções finalizadas (a quantidade da produção é em litros)
export const litrosProduzidos = (finalizadas = []) =>
    finalizadas.reduce((soma, p) => soma + Number(p.quantidade ?? 0), 0)

// "2026-10-24" -> "outubro"
export const nomeDoMes = (iso) =>
    new Date(`${iso.slice(0, 7)}-15T12:00:00`).toLocaleDateString('pt-BR', { month: 'long' })
