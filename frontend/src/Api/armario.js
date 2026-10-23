import api from './cliente'

// Resumo de todos os insumos ativos (saldo só com lotes não vencidos)
export const listarArmario = () => api.get('/armario').then((r) => r.data)

// Resumo de um insumo + lotes em ordem FEFO
export const obterDetalheArmario = (insumoId) => api.get(`/armario/${insumoId}`).then((r) => r.data)

// Cadastro de insumo (somente ADMIN)
export const criarInsumo = (dados) => api.post('/insumos', dados).then((r) => r.data)

// Entrada de estoque: cria um lote do insumo
export const registrarEntrada = (dados) => api.post('/lotes', dados).then((r) => r.data)
