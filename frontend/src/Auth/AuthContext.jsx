import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import api from '../Api/cliente'
import { setToken, clearToken, setUnauthorizedHandler } from './tokenStore'

const AuthContext = createContext(null)


export function AuthProvider({ children }) {
    // Token só em memória (tokenStore). Ao recarregar a página, volta para /login.
    const [session, setSession] = useState(null)

    const logout = useCallback(() => {
        clearToken()
        setSession(null)
    }, [])

    const login = useCallback(async (email, senha, manterConectado = false) => {
        const { data } = await api.post('/Auth/login', { email, senha, manterConectado })

        const token = data.accessToken ?? data.token
        if (!token) throw new Error('Resposta de login inválida.')

        const usuario = data.usuario ?? data.user ?? null
        setToken(token)
        setSession({
            token,
            usuario: usuario
                ? { ...usuario, perfil: String(usuario.perfil ?? '').toUpperCase() }
                : null,
        })
    }, [])

    // Quando o cliente.js desiste de renovar o token, leva o usuário ao login
    useEffect(() => {
        setUnauthorizedHandler(logout)
        return () => setUnauthorizedHandler(null)
    }, [logout])

    const value = useMemo(
        () => ({
            token: session?.token ?? null,
            usuario: session?.usuario ?? null,
            isAuthenticated: Boolean(session?.token),
            login,
            logout,
        }),
        [session, login, logout]
    )

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
    const ctx = useContext(AuthContext)
    if (!ctx) throw new Error('useAuth deve ser usado dentro de <AuthProvider>.')
    return ctx
}