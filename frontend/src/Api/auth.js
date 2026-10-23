import api from './cliente'

// manterConectado é opcional: só vai no corpo quando a pessoa marca a caixa.
export const login = (email, senha, manterConectado = false) =>
    api.post('/Auth/login', { email, senha, ...(manterConectado && { manterConectado }) }).then((r) => r.data)

export const me = () => api.get('/Auth/me').then((r) => r.data)

export const logout = () => api.post('/Auth/logout')

// Primeiro acesso: troca a senha temporária. Devolve uma sessão nova (mesmo formato do login).
export const primeiroAcesso = ({ novaSenha, confirmarSenha, emailRecuperacao }) =>
    api.post('/Auth/primeiro-acesso', { novaSenha, confirmarSenha, emailRecuperacao }).then((r) => r.data)

// Troca a senha de quem está logado. Devolve uma sessão nova (os outros aparelhos saem).
export const alterarSenha = ({ senhaAtual, novaSenha, confirmarSenha }) =>
    api.post('/Auth/alterar-senha', { senhaAtual, novaSenha, confirmarSenha }).then((r) => r.data)

// Troca o e-mail de recuperação: ele fica pendente até a pessoa clicar no link. Devolve o usuário.
export const alterarEmailRecuperacao = (email) =>
    api.put('/Auth/email-recuperacao', { email }).then((r) => r.data)

// Rotas públicas (links que chegam por e-mail)
export const confirmarEmail = (token) => api.post('/Auth/confirmar-email', { token })

export const esqueciSenha = (email) => api.post('/Auth/esqueci-senha', { email })

export const redefinirSenha = ({ token, novaSenha, confirmarSenha }) =>
    api.post('/Auth/redefinir-senha', { token, novaSenha, confirmarSenha })
