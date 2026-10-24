// Formatos comuns às telas de receitas, produção e estoque

export const formatarReais = (valor) =>
    Number(valor ?? 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

// "Lúpulo" e "lupulo" contam como iguais (nome repetido, busca)
export const normalizarTexto = (texto) =>
    String(texto ?? '')
        .normalize('NFD')
        .replace(/[̀-ͯ]/g, '')
        .trim()
        .toLowerCase()

// Data e hora da API ("2026-10-20T14:03:00Z") -> "20/10/2026"; curta -> "20/10"
export function formatarDataHora(iso, curta = false) {
    if (!iso) return '—'
    const d = new Date(iso)
    return curta
        ? d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' })
        : d.toLocaleDateString('pt-BR')
}

// "1.234,5" -> "1.234,5 L"
export const formatarLitros = (valor) =>
    `${Number(valor ?? 0).toLocaleString('pt-BR', { maximumFractionDigits: 2 })} L`

// Situação da produção
export const STATUS_BATELADA = {
    EM_ANDAMENTO: { rotulo: 'Em andamento', tipo: 'atencao' },
    FINALIZADA: { rotulo: 'Finalizada', tipo: 'ok' },
    CANCELADA: { rotulo: 'Cancelada', tipo: 'critica' },
}
