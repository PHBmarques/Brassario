import api from './client'

export const login = (email, senha) =>
    api.post('/auth/login', { email, senha }).then((r) => r.data)

export const me = () => api.get('/Auth/me').then((r) => r.data)

export const logout = () => api.post('/Auth/logout')