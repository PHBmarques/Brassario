import api from './cliente'

// Registra a perda de um lote (vencimento, quebra, contaminação...). 422 PERDA_MAIOR_QUE_SALDO se passar do saldo.
// dados: { quantidade, motivo } (motivo de 5 a 255 caracteres)
export const registrarPerda = (loteId, dados) => api.post(`/lotes/${loteId}/perdas`, dados).then((r) => r.data)
