import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'

afterEach(() => {
    cleanup()
    vi.clearAllMocks()
    try {
        localStorage.clear()
    } catch {
        /* ignora */
    }
})

// O jsdom não tem matchMedia (usado pelo tema claro/escuro)
if (!window.matchMedia) {
    window.matchMedia = () => ({
        matches: false,
        addEventListener: () => {},
        removeEventListener: () => {},
        addListener: () => {},
        removeListener: () => {},
    })
}
