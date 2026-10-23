import api from './cliente'

// manterConectado é opcional: só vai no corpo quando a pessoa marca a caixa.
export const login = (email, senha, manterConectado = false) =>
    api.post('/Auth/login', { email, senha, ...(manterConectado && { manterConectado }) }).then((r) => r.data)

export const me = () => api.get('/Auth/me').then((r) => r.data)

export const logout = () => api.post('/Auth/logout')
