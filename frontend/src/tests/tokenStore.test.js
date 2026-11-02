import { describe, it, expect, vi, beforeEach } from 'vitest'
import { getToken, setToken, clearToken, setUnauthorizedHandler, notifyUnauthorized } from '../Auth/tokenStore'

describe('tokenStore', () => {
    beforeEach(() => {
        clearToken()
        setUnauthorizedHandler(null)
    })

    it('guarda o token só em memória, não no localStorage', () => {
        setToken('abc')
        expect(getToken()).toBe('abc')
        expect(localStorage.length).toBe(0)
    })

    it('limpa o token', () => {
        setToken('abc')
        clearToken()
        expect(getToken()).toBeNull()
    })

    it('chama o tratador quando a sessão expira', () => {
        const fn = vi.fn()
        setUnauthorizedHandler(fn)
        notifyUnauthorized()
        expect(fn).toHaveBeenCalledTimes(1)
    })

    it('não quebra sem tratador registrado', () => {
        expect(() => notifyUnauthorized()).not.toThrow()
    })
})
