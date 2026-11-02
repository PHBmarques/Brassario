import { render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { ThemeProvider } from '../Theme/ThemeContext.jsx'

// Qualquer navegação para fora da tela cai aqui: o teste confere o caminho e o state
function Destino() {
    const { pathname, state } = useLocation()
    return (
        <p data-testid="destino" data-state={JSON.stringify(state ?? null)}>
            {pathname}
        </p>
    )
}

/**
 * Desenha uma tela dentro do roteador e do tema.
 * rota: endereço inicial (pode ter ?query) · caminho: padrão da rota (ex.: '/receitas/:id') · state: location.state
 */
export function renderTela(ui, { rota = '/', caminho, state } = {}) {
    const [pathname, query] = rota.split('?')
    const entrada = { pathname, search: query ? `?${query}` : '', state }
    const user = userEvent.setup()
    const resultado = render(
        <ThemeProvider>
            <MemoryRouter initialEntries={[entrada]}>
                <Routes>
                    <Route path={caminho ?? pathname} element={ui} />
                    <Route path="*" element={<Destino />} />
                </Routes>
            </MemoryRouter>
        </ThemeProvider>
    )
    return { user, ...resultado }
}

// Erro no formato do Axios: { response: { status, data } }
export function erroApi(status, data = {}) {
    const erro = new Error(`HTTP ${status}`)
    erro.response = { status, data }
    return erro
}

// Lê o state passado na navegação (do elemento de destino)
export const stateDoDestino = (elemento) => JSON.parse(elemento.getAttribute('data-state'))
