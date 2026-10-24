import api from './cliente'

// Alertas do Armário, já ordenados (urgentes primeiro).
// Cada um traz a ação da tela: VER_LOTES, REGISTRAR_PERDA, DAR_ENTRADA ou VER_COMPRAS.
export const listarAlertas = () => api.get('/alertas').then((r) => r.data)

// Insumos que acabam em 15 dias ou menos (pelo consumo dos últimos 30 dias), os mais urgentes primeiro
export const listarComprasSugeridas = () => api.get('/compras/sugestao').then((r) => r.data)
