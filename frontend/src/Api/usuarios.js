import api from './cliente'

// Tudo aqui é somente ADMIN

export const listarUsuarios = () => api.get('/usuarios').then((r) => r.data)

export const criarUsuario = (dados) => api.post('/usuarios', dados).then((r) => r.data)

export const alterarStatusUsuario = (id, ativo) => api.patch(`/usuarios/${id}/status`, { ativo }).then((r) => r.data)

// Gera uma senha temporária: { usuario, senhaTemporaria }. Ela só aparece nesta resposta.
export const redefinirSenhaUsuario = (id) => api.post(`/usuarios/${id}/redefinir-senha`).then((r) => r.data)
