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
    IconeExpandir,
    IconeInicio,
    IconeLua,
    IconeProducoes,
    IconeProduzir,
    IconeReceitas,
    IconeRecolher,
    IconeSair,
    IconeSetaCima,
    IconeSol,
    IconeUsuarios,
} from './Icones.jsx'
import './AppLayout.css'

const MENU = [
    { rotulo: 'Início', para: '/', fim: true, Icone: IconeInicio },
    // O cadastro de insumo também faz parte do Armário
    { rotulo: 'Armário', para: '/armario', Icone: IconeArmario, tambemEm: ['/insumos'] },
    { rotulo: 'Entrada de estoque', para: '/entrada', Icone: IconeEntrada },
    { rotulo: 'Receitas', para: '/receitas', Icone: IconeReceitas },
    { rotulo: 'Produzir', para: '/produzir', Icone: IconeProduzir },
    { rotulo: 'Produções', para: '/bateladas', Icone: IconeProducoes },
    { rotulo: 'Alertas', para: '/alertas', Icone: IconeAlertas },
    { rotulo: 'Usuários', para: '/usuarios', Icone: IconeUsuarios, somenteAdmin: true },
]

const CHAVE_MENU = 'brassario-menu-recolhido'

export default function AppLayout() {
    const { usuario, logout } = useAuth()
    const { tema, alternarTema } = useTheme()
    const { pathname } = useLocation()
    const ehAdmin = usuario?.perfil === 'ADMIN'
    const temaClaro = tema === 'light'

    const [recolhido, setRecolhido] = useState(() => {
        try {
            return localStorage.getItem(CHAVE_MENU) === '1'
        } catch {
            return false
        }
    })
    const [menuUsuario, setMenuUsuario] = useState(false)
    const areaUsuario = useRef(null)

    useEffect(() => {
        try {
            localStorage.setItem(CHAVE_MENU, recolhido ? '1' : '0')
        } catch {
            /* ignora */
        }
    }, [recolhido])

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

    // Telas de ADMIN ficam ocultas para OPERADOR
    const itens = MENU.filter((item) => !item.somenteAdmin || ehAdmin)

    const rotuloTema = temaClaro ? 'Tema escuro' : 'Tema claro'
    const rotuloMenu = recolhido ? 'Expandir menu' : 'Recolher menu'

    return (
        <div className={`app${recolhido ? ' app--recolhido' : ''}`}>
            <aside className="app__sidebar">
                <div className="app__brand">
                    <Logo tamanho={34} />
                    <span className="app__brand-name">Brassário</span>
                </div>

                <nav className="app__nav" aria-label="Menu principal">
                    {itens.map(({ rotulo, para, fim, tambemEm = [], Icone }) => (
                        <NavLink
                            key={para}
                            to={para}
                            end={fim}
                            title={recolhido ? rotulo : undefined}
                            className={({ isActive }) =>
                                `app__link${isActive || tambemEm.some((p) => pathname.startsWith(p)) ? ' app__link--ativo' : ''}`
                            }
                        >
                            <Icone />
                            <span className="app__rotulo">{rotulo}</span>
                        </NavLink>
                    ))}
                </nav>

                <div className="app__rodape">
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

                    <button
                        type="button"
                        className="app__acao"
                        onClick={alternarTema}
                        aria-label={temaClaro ? 'Mudar para o tema escuro' : 'Mudar para o tema claro'}
                        title={rotuloTema}
                    >
                        {temaClaro ? <IconeLua /> : <IconeSol />}
                        <span className="app__rotulo">{rotuloTema}</span>
                    </button>

                    <button
                        type="button"
                        className="app__acao app__recolher"
                        onClick={() => setRecolhido((v) => !v)}
                        aria-pressed={recolhido}
                        title={rotuloMenu}
                    >
                        {recolhido ? <IconeExpandir /> : <IconeRecolher />}
                        <span className="app__rotulo">{rotuloMenu}</span>
                    </button>
                </div>
            </aside>

            <main className="app__main" id="conteudo">
                <Outlet />
            </main>
        </div>
    )
}
