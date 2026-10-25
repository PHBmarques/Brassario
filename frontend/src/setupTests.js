import '@testing-library/jest-dom/vitest'
import { afterEach } from 'vitest'
import { cleanup } from '@testing-library/react'

// Desmonta o que cada teste renderizou e limpa o localStorage entre os testes
afterEach(() => {
    cleanup()
    localStorage.clear()
})
