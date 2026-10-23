import api from './cliente'

export const listarFornecedores = () => api.get('/fornecedores').then((r) => r.data)

// Cadastro de fornecedor (somente ADMIN)
export const criarFornecedor = (dados) => api.post('/fornecedores', dados).then((r) => r.data)
