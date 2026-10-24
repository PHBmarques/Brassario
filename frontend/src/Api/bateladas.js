import api from './cliente'

// Produções (bateladas)

// Histórico paginado: { itens, pagina, tamanhoPagina, total, totalPaginas }
// filtros: { status, receitaId, de, ate, pagina, tamanho }
export const listarBateladas = (filtros = {}) =>
    api
        .get('/bateladas', {
            params: Object.fromEntries(Object.entries(filtros).filter(([, v]) => v !== '' && v != null)),
        })
        .then((r) => r.data)

export const obterBatelada = (id) => api.get(`/bateladas/${id}`).then((r) => r.data)

// Confirma a produção: abate o estoque (FEFO). 422 ESTOQUE_INSUFICIENTE / LOTE_VENCIDO se faltar algo.
export const produzir = (dados) => api.post('/bateladas', dados).then((r) => r.data)

export const finalizarBatelada = (id, volumeFinal) =>
    api.patch(`/bateladas/${id}/finalizar`, { volumeFinal }).then((r) => r.data)

// Devolve os insumos aos lotes. Motivo de 5 a 255 caracteres.
export const cancelarBatelada = (id, motivo) => api.post(`/bateladas/${id}/cancelar`, { motivo }).then((r) => r.data)
