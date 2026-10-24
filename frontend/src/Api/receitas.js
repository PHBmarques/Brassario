import api from './cliente'

// Lista (sem ingredientes): [{ id, nome, estilo, volumeBaseLitros, ativa, quantidadeItens }]
export const listarReceitas = (busca) =>
    api.get('/receitas', { params: busca ? { busca } : undefined }).then((r) => r.data)

// Ficha completa: { id, nome, estilo, volumeBaseLitros, modoPreparo, ativa, itens: [...] }
export const obterReceita = (id) => api.get(`/receitas/${id}`).then((r) => r.data)

// Somente ADMIN
export const criarReceita = (dados) => api.post('/receitas', dados).then((r) => r.data)
export const atualizarReceita = (id, dados) => api.put(`/receitas/${id}`, dados).then((r) => r.data)

// Para o volume pedido: por ingrediente, o necessário, o disponível e o que falta. Não mexe no estoque.
export const disponibilidadeReceita = (id, volume) =>
    api.get(`/receitas/${id}/disponibilidade`, { params: { volume } }).then((r) => r.data)

// Prévia da produção (mesmo cálculo da confirmação): linhas por lote, problemas, custo estimado
export const previaProducao = (id, quantidade) =>
    api.get(`/receitas/${id}/previa`, { params: { quantidade } }).then((r) => r.data)
