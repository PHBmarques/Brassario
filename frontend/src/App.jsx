import { Routes, Route } from 'react-router-dom'
import { rotas } from './rotas'
import EmConstrucao from './components/EmConstrucao'
import NaoEncontrada from './pages/NaoEncontrada'

// FE-1: rotas de todas as telas, ainda apontando para a página provisória.
// FE-2 vai envolver as rotas protegidas com o ProtectedRoute.
function App() {
    return (
        <Routes>
            {rotas.map((r) => (
                <Route key={r.path} path={r.path} element={<EmConstrucao titulo={r.titulo} />} />
            ))}
            <Route path="*" element={<NaoEncontrada />} />
        </Routes>
    )
}

export default App