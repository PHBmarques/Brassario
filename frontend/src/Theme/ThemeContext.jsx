import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'

const CHAVE = 'brassario-tema'
const ThemeContext = createContext(null)

// Tema inicial: o que a pessoa escolheu da última vez; senão, o do sistema; senão, claro.
function temaInicial() {
    try {
        const salvo = localStorage.getItem(CHAVE)
        if (salvo === 'light' || salvo === 'dark') return salvo
    } catch {
        /* localStorage indisponível: segue para o padrão */
    }
    return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

export function ThemeProvider({ children }) {
    const [tema, setTema] = useState(temaInicial)

    useEffect(() => {
        document.documentElement.dataset.theme = tema
        try {
            localStorage.setItem(CHAVE, tema)
        } catch {
            /* ignora */
        }
    }, [tema])

    const alternarTema = useCallback(() => {
        setTema((atual) => (atual === 'light' ? 'dark' : 'light'))
    }, [])

    const value = useMemo(() => ({ tema, alternarTema }), [tema, alternarTema])

    return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}

export function useTheme() {
    const ctx = useContext(ThemeContext)
    if (!ctx) throw new Error('useTheme deve ser usado dentro de <ThemeProvider>.')
    return ctx
}
