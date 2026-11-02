import { useEffect, useRef, useState } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../Auth/AuthContext.jsx'
import { useTheme } from '../Theme/ThemeContext.jsx'
import { iniciais } from '../Utils/usuario.js'
import Logo from './Logo.jsx'
import {
    IconeAlertas,
    IconeArmario,
    IconeChave,
    IconeConta,
    IconeEntrada,
    IconeInicio,
    IconeMais,
    IconeLua,
    IconeProducoes,
    IconeProduzir,
    IconeReceitas,
    IconeSair,
    IconeSetaCima,
    IconeSol,
    IconeUsuarios,
} from './Icones.jsx'
import './AppLayout.css'

// No celular a barra de baixo mostra só os itens com principal: true; o resto fica em "Mais".
// "curto" é o nome reduzido usado na barra de baixo (sem ele, vale o rótulo inteiro).
const MENU = [
    { rotulo: 'Início', para: '/', fim: true, Icone: IconeInicio, principal: true },
    // O cadastro de insumo também faz parte do Armário
    { rotulo: 'Armário', para: '/armario', Icone: IconeArmario, tambemEm: ['/insumos'], principal: true },
    { rotulo: 'Entrada de estoque', curto: 'Entrada', para: '/entrada', Icone: IconeEntrada, principal: true },
    { rotulo: 'Receitas', para: '/receitas', Icone: IconeReceitas },
    { rotulo: 'Produzir', para: '/produzir', Icone: IconeProduzir, principal: true },
    { rotulo: 'Produções', para: '/bateladas', Icone: IconeProducoes },
    { rotulo: 'Usuários', para: '/usuarios', Icone: IconeUsuarios, somenteAdmin: true },
]

export default function AppLayout() {
    const { usuario, logout } = useAuth()
    const { tema, alternarTema } = useTheme()
    const { pathname } = useLocation()
    const ehAdmin = usuario?.perfil === 'ADMIN'
    const temaClaro = tema === 'light'

    const [menuUsuario, setMenuUsuario] = useState(false)
    const [maisAberto, setMaisAberto] = useState(false)
    const areaUsuario = useRef(null)

    // Fecha o menu do usuário ao clicar fora ou apertar Esc
    useEffect(() => {
        if (!menuUsuario) return undefined
        const fora = (e) => {
            if (!areaUsuario.current?.contains(e.target)) setMenuUsuario(false)
        }
        const esc = (e) => {
            if (e.key === 'Escape') setMenuUsuario(false)
        }
        document.addEventListener('mousedown', fora)
        document.addEventListener('keydown', esc)
        return () => {
            document.removeEventListener('mousedown', fora)
            document.removeEventListener('keydown', esc)
        }
    }, [menuUsuario])

    const fecharMenuUsuario = () => setMenuUsuario(false)

    // "Mais" (celular) fecha ao trocar de tela e com Esc
    useEffect(() => {
        setMaisAberto(false)
    }, [pathname])

    useEffect(() => {
        if (!maisAberto) return undefined
        const esc = (e) => {
            if (e.key === 'Escape') setMaisAberto(false)
        }
        document.addEventListener('keydown', esc)
        return () => document.removeEventListener('keydown', esc)
    }, [maisAberto])

    // Telas de ADMIN ficam ocultas para OPERADOR
    const itens = MENU.filter((item) => !item.somenteAdmin || ehAdmin)
    const temExtras = itens.some((item) => !item.principal)
    const estaNaTela = ({ para, fim, tambemEm = [] }) =>
        (fim ? pathname === para : pathname.startsWith(para)) || tambemEm.some((p) => pathname.startsWith(p))
    const extraAtivo = itens.some((item) => !item.principal && estaNaTela(item))

    const rotuloTema = temaClaro ? 'Tema escuro' : 'Tema claro'
    // Compras sugeridas é aberta a partir dos Alertas
    const emAlertas = pathname.startsWith('/alertas') || pathname.startsWith('/compras')

    return (
        <div className="app">
            <aside className="app__sidebar">
                <div className="app__brand">
                    <Logo tamanho={34} />
                    <span className="app__brand-name">Brassário</span>
                </div>

                <nav className={`app__nav${maisAberto ? ' app__nav--mais' : ''}`} aria-label="Menu principal">
                    {itens.map(({ rotulo, curto, para, fim, tambemEm = [], Icone, principal }) => (
                        <NavLink
                            key={para}
                            to={para}
                            end={fim}
                            className={({ isActive }) =>
                                `app__link${principal ? '' : ' app__link--extra'}${isActive || tambemEm.some((p) => pathname.startsWith(p)) ? ' app__link--ativo' : ''}`
                            }
                        >
                            <Icone />
                            <span className="app__rotulo">
                                {curto ? (
                                    <>
                                        <span className="app__rotulo-longo">{rotulo}</span>
                                        <span className="app__rotulo-curto" aria-hidden="true">{curto}</span>
                                    </>
                                ) : (
                                    rotulo
                                )}
                            </span>
                        </NavLink>
                    ))}
                    {temExtras && (
                        <>
                            <span className="app__quebra" aria-hidden="true" />
                            <button
                                type="button"
                                className={`app__link app__mais${extraAtivo ? ' app__link--ativo' : ''}`}
                                aria-expanded={maisAberto}
                                onClick={() => setMaisAberto((aberto) => !aberto)}
                            >
                                <IconeMais />
                                <span className="app__rotulo">Mais</span>
                            </button>
                        </>
                    )}
                </nav>
            </aside>

            <div className="app__coluna">
                {/* Barra superior: alertas, tema e usuário no canto direito */}
                <header className="app__topo">
                    <Link
                        to="/alertas"
                        className={`app__topo-botao${emAlertas ? ' app__topo-botao--ativo' : ''}`}
                        aria-label="Alertas"
                        title="Alertas"
                    >
                        <IconeAlertas tamanho={20} />
                        {/* Para mostrar a bolinha com a quantidade de alertas, coloque aqui:
                            {total > 0 && <span className="app__topo-selo">{total}</span>} */}
                    </Link>

                    <button
                        type="button"
                        className="app__topo-botao"
                        onClick={alternarTema}
                        aria-label={temaClaro ? 'Mudar para o tema escuro' : 'Mudar para o tema claro'}
                        title={rotuloTema}
                    >
                        {temaClaro ? <IconeLua tamanho={20} /> : <IconeSol tamanho={20} />}
                    </button>

                    <div className="app__usuario" ref={areaUsuario}>
                        <button
                            type="button"
                            className="app__chip"
                            aria-haspopup="menu"
                            aria-expanded={menuUsuario}
                            onClick={() => setMenuUsuario((aberto) => !aberto)}
                            title={usuario?.nome}
                        >
                            <span className="app__avatar" aria-hidden="true">{iniciais(usuario?.nome)}</span>
                            <span className="app__chip-info">
                                <strong>{usuario?.nome ?? 'Usuário'}</strong>
                                <span>{ehAdmin ? 'Administrador' : 'Operador'}</span>
                            </span>
                            <IconeSetaCima
                                tamanho={18}
                                className={`app__chip-seta${menuUsuario ? '' : ' app__chip-seta--fechada'}`}
                            />
                        </button>

                        {menuUsuario && (
                            <div className="app__popover" role="menu" aria-label="Conta">
                                <div className="app__popover-cabecalho">
                                    <strong>{usuario?.nome}</strong>
                                    <span>{usuario?.email}</span>
                                </div>
                                <Link to="/minha-conta" role="menuitem" className="app__popover-item" onClick={fecharMenuUsuario}>
                                    <IconeConta tamanho={18} />
                                    Minha conta
                                </Link>
                                <Link to="/alterar-senha" role="menuitem" className="app__popover-item" onClick={fecharMenuUsuario}>
                                    <IconeChave tamanho={18} />
                                    Alterar minha senha
                                </Link>
                                <button
                                    type="button"
                                    role="menuitem"
                                    className="app__popover-item app__popover-item--sair"
                                    onClick={logout}
                                >
                                    <IconeSair tamanho={18} />
                                    Sair
                                </button>
                            </div>
                        )}
                    </div>
                </header>

                <main className="app__main" id="conteudo">
                    <Outlet />
                </main>
            </div>
        </div>
    )
}
