import { formatarQuantidade } from './armario'

// Regras da tela Alertas: filtros, textos de cada cartão e os botões de cada tipo.

export const TIPOS_VALIDADE = ['VENCIDO', 'VALIDADE_CRITICA', 'ATENCAO']
export const TIPOS_ESTOQUE = ['ESTOQUE_VAZIO', 'ESTOQUE_BAIXO']

export const FILTROS = [
    { id: 'TODOS', rotulo: 'Todos', tipos: null },
    { id: 'VALIDADE', rotulo: 'Validade', tipos: TIPOS_VALIDADE },
    { id: 'ESTOQUE', rotulo: 'Estoque', tipos: TIPOS_ESTOQUE },
]

export function filtrarAlertas(alertas = [], filtroId = 'TODOS') {
    const tipos = FILTROS.find((f) => f.id === filtroId)?.tipos
    return tipos ? alertas.filter((a) => tipos.includes(a.tipo)) : alertas
}

export function contarPorFiltro(alertas = []) {
    return Object.fromEntries(FILTROS.map((f) => [f.id, filtrarAlertas(alertas, f.id).length]))
}

const dias = (n) => `${n} ${n === 1 ? 'dia' : 'dias'}`

// Etiqueta do cartão
export function rotuloDoAlerta(a) {
    switch (a.tipo) {
        case 'VENCIDO': return 'Vencido'
        case 'VALIDADE_CRITICA': return 'Validade Crítica'
        case 'ATENCAO': return a.diasParaVencer === 0 ? 'Vence hoje' : `Vence em ${dias(a.diasParaVencer)}`
        case 'ESTOQUE_VAZIO': return 'Armário vazio'
        case 'ESTOQUE_BAIXO': return 'Estoque baixo'
        default: return a.tipo
    }
}

// Frase de apoio do cartão
export function textoDoAlerta(a) {
    const saldo = formatarQuantidade(a.saldo, a.unidadeMedida)
    const quando = a.diasParaVencer === 0 ? 'vence hoje' : `vence em ${dias(a.diasParaVencer)}`
    switch (a.tipo) {
        case 'VENCIDO': return `Lote ${a.codigoLote} venceu há ${dias(Math.abs(a.diasParaVencer))}. Não será usado em produção.`
        case 'VALIDADE_CRITICA': return `Lote ${a.codigoLote} ${quando}. Saldo: ${saldo}.`
        case 'ATENCAO': return `Lote ${a.codigoLote} ${quando}.`
        case 'ESTOQUE_VAZIO': return 'Não há estoque no Armário.'
        case 'ESTOQUE_BAIXO':
            return `Saldo de ${saldo}, abaixo do mínimo de ${formatarQuantidade(a.estoqueMinimo, a.unidadeMedida)}.`
        default: return ''
    }
}

// Os dois botões de cada cartão. ids: LOTES, PERDA, RECEITA, COMPRA, ENTRADA
export function botoesDoAlerta(a) {
    const lotes = { id: 'LOTES', rotulo: 'Ver lotes' }
    switch (a.tipo) {
        case 'VENCIDO':
        case 'VALIDADE_CRITICA':
            return [lotes, { id: 'PERDA', rotulo: 'Registrar perda' }]
        case 'ATENCAO':
            return [lotes, { id: 'RECEITA', rotulo: 'Planejar uso em receita' }]
        case 'ESTOQUE_VAZIO':
            return [{ id: 'COMPRA', rotulo: 'Adicionar à compra' }, { id: 'ENTRADA', rotulo: 'Dar entrada' }]
        case 'ESTOQUE_BAIXO':
            return [{ id: 'COMPRA', rotulo: 'Adicionar à compra' }, lotes]
        default:
            return [lotes]
    }
}
