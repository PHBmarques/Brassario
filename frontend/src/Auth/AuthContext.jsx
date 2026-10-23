import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import api from '../Api/cliente'
import { login as apiLogin, logout as apiLogout, me } from '../Api/auth'
import { setToken, clearToken, setUnauthorizedHandler } from './tokenStore'

const AuthContext = createContext(null)

const normalizar = (usuario) =>
    usuario ? { ...usuario, perfil: String(usuario.perfil ?? '').toUpperCase() } : null

// Ao abrir ou recarregar a página, tenta recuperar a sessão pelo refresh token (cookie HttpOnly).
// A promessa fica fora do componente para o StrictMode (que roda o efeito 2x em desenvolvimento)
// não gastar o refresh duas vezes.
let restauracao = null
function restaurarSessao() {
    if (!restauracao) {
        restauracao = api
            .post('/auth/refresh')
            .then(async (res) => {
                const token = res.data?.accessToken ?? res.data?.token
                if (!token) return null
                setToken(token)
                const usuario = await me()
                return { token, usuario: normalizar(usuario) }
            })
            .catch(() => {
                clearToken()
                return null // sem cookie ou expirado: segue para o login
            })
            .finally(() => {
                setTimeout(() => { restauracao = null }, 0)
            })
    }
    return restauracao
}

export function AuthProvider({ children }) {
    // Token só em memória (tokenStore). O estado guarda o usuário para a interface.
    const [session, setSession] = useState(null)
    const [carregando, setCarregando] = useState(true)

    // Encerra só no navegador (usado também quando a API diz que a sessão expirou)
    const encerrar = useCallback(() => {
        clearToken()
        setSession(null)
    }, [])

    // Botão "Sair": avisa a API para invalidar o refresh token e depois limpa a sessão
    const logout = useCallback(async () => {
        try {
            await apiLogout()
        } catch {
            /* mesmo sem resposta da API, a pessoa sai */
        }
        encerrar()
    }, [encerrar])

    const login = useCallback(async (email, senha, manterConectado = false) => {
        const data = await apiLogin(email, senha, manterConectado)

        const token = data?.accessToken ?? data?.token
        if (!token) throw new Error('Resposta de login inválida.')

        setToken(token)
        try {
            // Se a resposta do login já traz o usuário, usa; senão busca em /Auth/me
            const usuario = data.usuario ?? data.user ?? (await me())
            setSession({ token, usuario: normalizar(usuario) })
        } catch (erro) {
            clearToken()
            throw erro
        }
    }, [])

    useEffect(() => {
        let ativo = true
        restaurarSessao().then((restaurada) => {
            if (!ativo) return
            setSession(restaurada)
            setCarregando(false)
        })
        return () => { ativo = false }
    }, [])

    // Quando o cliente.js desiste de renovar o token, leva o usuário ao login
    useEffect(() => {
        setUnauthorizedHandler(encerrar)
        return () => setUnauthorizedHandler(null)
    }, [encerrar])

    const value = useMemo(
        () => ({
            token: session?.token ?? null,
            usuario: session?.usuario ?? null,
            isAuthenticated: Boolean(session?.token),
            carregando,
            login,
            logout,
        }),
        [session, carregando, login, logout]
    )

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
    const ctx = useContext(AuthContext)
    if (!ctx) throw new Error('useAuth deve ser usado dentro de <AuthProvider>.')
    return ctx
}
