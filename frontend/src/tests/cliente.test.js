import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import axios from 'axios'
import api from '../Api/cliente'
import { setToken, clearToken, getToken, setUnauthorizedHandler } from '../Auth/tokenStore'

// Troca a rede por uma função: cada teste decide o que a "API" responde.
function resposta(config, status, data = {}) {
    const res = { data, status, statusText: '', headers: {}, config }
    if (status >= 200 && status < 300) return Promise.resolve(res)
    const erro = new Error(`HTTP ${status}`)
    erro.config = config
    erro.response = res
    erro.isAxiosError = true
    return Promise.reject(erro)
}

describe('cliente da API', () => {
    let chamadas
    beforeEach(() => {
        chamadas = []
        clearToken()
        setUnauthorizedHandler(null)
    })
    afterEach(() => vi.restoreAllMocks())

    it('usa /api/v1 e envia o cookie do refresh token', () => {
        expect(api.defaults.baseURL).toBe('/api/v1')
        expect(api.defaults.withCredentials).toBe(true)
    })

    it('anexa o token Bearer em toda requisição', async () => {
        setToken('token-1')
        api.defaults.adapter = (config) => { chamadas.push(config); return resposta(config, 200) }
        await api.get('/armario')
        expect(chamadas[0].headers.Authorization).toBe('Bearer token-1')
    })

    it('não manda Authorization sem token', async () => {
        api.defaults.adapter = (config) => { chamadas.push(config); return resposta(config, 200) }
        await api.get('/armario')
        expect(chamadas[0].headers.Authorization).toBeUndefined()
    })

    it('em 401 renova o token uma vez e repete a requisição', async () => {
        setToken('velho')
        const post = vi.spyOn(axios, 'post').mockResolvedValue({ data: { accessToken: 'novo' } })
        api.defaults.adapter = (config) => {
            chamadas.push(config)
            return config.headers.Authorization === 'Bearer novo' ? resposta(config, 200, { ok: true }) : resposta(config, 401)
        }
        const res = await api.get('/armario')
        expect(res.data).toEqual({ ok: true })
        expect(post).toHaveBeenCalledTimes(1)
        expect(getToken()).toBe('novo')
        expect(chamadas).toHaveLength(2)
    })

    it('se a renovação falhar, limpa o token e avisa que a sessão expirou', async () => {
        setToken('velho')
        const aoExpirar = vi.fn()
        setUnauthorizedHandler(aoExpirar)
        // Função simples (não vi.fn) para a rejeição não ser tratada como "não capturada" pelo executor
        const postOriginal = axios.post
        axios.post = () => new Promise((_, rejeita) => setTimeout(() => rejeita(new Error('refresh expirado')), 0))
        api.defaults.adapter = (config) => resposta(config, 401)
        await expect(api.get('/armario')).rejects.toBeTruthy()
        expect(getToken()).toBeNull()
        expect(aoExpirar).toHaveBeenCalledTimes(1)
        axios.post = postOriginal
    })

    it('401 no login não tenta renovar o token', async () => {
        const post = vi.spyOn(axios, 'post')
        api.defaults.adapter = (config) => resposta(config, 401, { mensagem: 'Credenciais inválidas.' })
        await expect(api.post('/Auth/login', {})).rejects.toBeTruthy()
        expect(post).not.toHaveBeenCalled()
    })

    it('outros erros (422) passam direto, sem renovar', async () => {
        const post = vi.spyOn(axios, 'post')
        api.defaults.adapter = (config) => resposta(config, 422, { codigo: 'ESTOQUE_INSUFICIENTE' })
        await expect(api.post('/bateladas', {})).rejects.toMatchObject({ response: { status: 422 } })
        expect(post).not.toHaveBeenCalled()
    })
})
