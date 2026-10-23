import axios from 'axios'
import { getToken, setToken, clearToken, notifyUnauthorized } from '../Auth/tokenStore'

// baseURL relativa: no Docker o Nginx repassa /api para a API, e em desenvolvimento o Vite faz o proxy.
const api = axios.create({
    baseURL: '/api/v1',
    timeout: 15000,
    withCredentials: true, // envia o cookie do refresh token
})

// 1) Anexa o token em toda requisição
api.interceptors.request.use((config) => {
    const token = getToken()
    if (token) config.headers.Authorization = `Bearer ${token}`
    return config
})

// 2) Se a API responder 401, tenta renovar o token UMA vez e repete a requisição
let renovando = null
function renovarToken() {
    if (!renovando) {
        renovando = axios
            .post('/api/v1/Auth/refresh', null, { withCredentials: true })
            .then((res) => {
                setToken(res.data.accessToken)
                return res.data.accessToken
            })
            .finally(() => { renovando = null })
    }
    return renovando
}

api.interceptors.response.use(
    (res) => res,
    async (error) => {
        const original = error.config
        const status = error.response?.status
        const url = original?.url ?? ''
        const ehRotaDeAuth = url.includes('/Auth/login') || url.includes('/Auth/refresh')

        if (status === 401 && original && !original._retry && !ehRotaDeAuth) {
            original._retry = true
            try {
                const novoToken = await renovarToken()
                original.headers.Authorization = `Bearer ${novoToken}`
                return api(original)
            } catch {
                clearToken()
                notifyUnauthorized() // leva o usuário ao login
            }
        }
        return Promise.reject(error)
    }
)

export default api