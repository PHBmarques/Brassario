import { Route, Routes } from 'react-router-dom'
import ProtectedRoute from './Auth/ProtectedRoute.jsx'
import AppLayout from './components/AppLayout.jsx'
import EmConstrucao from './components/EmConstrucao.jsx'
import Login from './Pages/Login.jsx'
import CriarSenha from './Pages/CriarSenha.jsx'
import EsqueciSenha from './Pages/EsqueciSenha.jsx'
import RedefinirSenha from './Pages/RedefinirSenha.jsx'
import ConfirmarEmail from './Pages/ConfirmarEmail.jsx'
import Home from './Pages/Home.jsx'
import Armario from './Pages/Armario.jsx'
import DetalheInsumo from './Pages/DetalheInsumo.jsx'
import EntradaEstoque from './Pages/EntradaEstoque.jsx'
import NovoInsumo from './Pages/NovoInsumo.jsx'
import Receitas from './Pages/Receitas.jsx'
import FichaReceita from './Pages/FichaReceita.jsx'
import ReceitaForm from './Pages/ReceitaForm.jsx'
import Produzir from './Pages/Produzir.jsx'
import Producoes from './Pages/Producoes.jsx'
import Alertas from './Pages/Alertas.jsx'
import ComprasSugeridas from './Pages/ComprasSugeridas.jsx'
import Usuarios from './Pages/Usuarios.jsx'
import MinhaConta from './Pages/MinhaConta.jsx'
import AlterarSenha from './Pages/AlterarSenha.jsx'
import NaoEncontrada from './Pages/NaoEncontrada.jsx'
import { rotas } from './rotas'

// Telas já prontas. Quando uma nova tela ficar pronta, é só incluir aqui o caminho dela
// (igual ao de rotas.js); as que ainda não estão aqui mostram "Tela em construção".
const prontas = {
    '/': <Home />,
    '/armario': <Armario />,
    '/armario/:id': <DetalheInsumo />,
    '/insumos/novo': <NovoInsumo />,
    '/entrada': <EntradaEstoque />,
    '/receitas': <Receitas />,
    '/receitas/nova': <ReceitaForm />,
    '/receitas/:id': <FichaReceita />,
    '/receitas/:id/editar': <ReceitaForm />,
    '/produzir': <Produzir />,
    '/bateladas': <Producoes />,
    '/bateladas/:id': <Producoes />,
    '/alertas': <Alertas />,
    '/compras': <ComprasSugeridas />,
    '/usuarios': <Usuarios />,
    '/minha-conta': <MinhaConta />,
    '/alterar-senha': <AlterarSenha />,
}

// Telas exclusivas de ADMIN: OPERADOR volta para o início
const somenteAdmin = ['/usuarios', '/insumos/novo', '/receitas/nova', '/receitas/:id/editar']

export default function App() {
    return (
        <Routes>
            <Route path="/login" element={<Login />} />

            {/* Links que chegam por e-mail e "esqueci a senha": abrem sem login */}
            <Route path="/esqueci-senha" element={<EsqueciSenha />} />
            <Route path="/redefinir-senha" element={<RedefinirSenha />} />
            <Route path="/confirmar-email" element={<ConfirmarEmail />} />

            {/* Primeiro acesso: logado, mas ainda com a senha temporária */}
            <Route
                path="/criar-senha"
                element={
                    <ProtectedRoute permitirTrocaPendente>
                        <CriarSenha />
                    </ProtectedRoute>
                }
            />

            {/* Tudo daqui exige login e aparece dentro do menu lateral */}
            <Route
                element={
                    <ProtectedRoute>
                        <AppLayout />
                    </ProtectedRoute>
                }
            >
                {rotas
                    .filter((r) => r.path !== '/login')
                    .map((r) => {
                        const tela = prontas[r.path] ?? <EmConstrucao titulo={r.titulo} />
                        return (
                            <Route
                                key={r.path}
                                path={r.path}
                                element={
                                    somenteAdmin.includes(r.path)
                                        ? <ProtectedRoute roles={['ADMIN']}>{tela}</ProtectedRoute>
                                        : tela
                                }
                            />
                        )
                    })}
            </Route>

            <Route path="*" element={<NaoEncontrada />} />
        </Routes>
    )
}
