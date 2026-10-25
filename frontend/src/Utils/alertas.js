import { formatarData, formatarQuantidade } from './armario'

// Nome do alerta na tela (usado em Alertas, Início e no aviso da Entrada de estoque)
export const ROTULO_ALERTA = {
    VENCIDO: 'Lote vencido',
    VALIDADE_CRITICA: 'Validade crítica',
    ATENCAO: 'Vence em breve',
    ESTOQUE_VAZIO: 'Armário vazio',
    ESTOQUE_BAIXO: 'Estoque baixo',
}

// Severidades que contam como "Urgente"; as outras são "Atenção"
export const URGENTES = ['CRITICA', 'ALTA']

export const ehUrgente = (alerta) => URGENTES.includes(alerta.severidade)

export function detalheDoAlerta(a) {
    if (a.loteId) {
        const quando =
            a.diasParaVencer < 0
                ? `venceu em ${formatarData(a.dataValidade)}`
                : a.diasParaVencer === 0
                    ? 'vence hoje'
                    : `vence em ${a.diasParaVencer} ${a.diasParaVencer === 1 ? 'dia' : 'dias'}`
        return `Lote ${a.codigoLote}, ${quando}. Saldo: ${formatarQuantidade(a.saldo, a.unidadeMedida)}.`
    }
    return `Saldo: ${formatarQuantidade(a.saldo, a.unidadeMedida)}. Mínimo: ${formatarQuantidade(a.estoqueMinimo, a.unidadeMedida)}.`
}
