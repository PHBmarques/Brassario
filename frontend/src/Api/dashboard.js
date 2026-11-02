import api from './cliente'

// Dashboard da tela Início (GET /api/v1/dashboard/...)

// Totais: insumos ativos, sem estoque, abaixo do mínimo, para compra, consumo dos últimos 30 dias
export const resumoDashboard = () => api.get('/dashboard/resumo').then((r) => r.data)

// Consumo de cada insumo nos últimos 30 dias (já em ordem do que mais saiu)
export const consumoDashboard = () => api.get('/dashboard/consumo').then((r) => r.data)

// Quantos dias o saldo de cada insumo dura, pelo ritmo de consumo (os mais urgentes primeiro)
export const previsaoDashboard = () => api.get('/dashboard/previsao').then((r) => r.data)
