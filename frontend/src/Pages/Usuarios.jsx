import { useCallback, useEffect, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { mensagemDeErro } from '../Api/erros'
import { alterarStatusUsuario, listarUsuarios, redefinirSenhaUsuario } from '../Api/usuarios'
import { useAuth } from '../Auth/AuthContext.jsx'
import Etiqueta from '../components/Etiqueta.jsx'
import '../components/Formulario.css'
import './Usuarios.css'

// Somente ADMIN (protegida em App.jsx). Só lista; o cadastro fica em /usuarios/novo.
export default function Usuarios() {
    const { usuario: logado } = useAuth()
    const location = useLocation()
    const navigate = useNavigate()
    const [usuarios, setUsuarios] = useState([])
    const [carregando, setCarregando] = useState(true)
    const [erroLista, setErroLista] = useState('')
    const [ocupado, setOcupado] = useState(null) // id da linha com ação em andamento
    // Vem do cadastro: { nome, senha }. Também é preenchida ao redefinir uma senha.
    const [senhaGerada, setSenhaGerada] = useState(location.state?.senhaGerada ?? null)
    const [copiado, setCopiado] = useState(false)

    // Tira a senha do histórico do navegador: atualizar a página não mostra a senha de novo
    useEffect(() => {
        if (location.state?.senhaGerada) navigate(location.pathname, { replace: true, state: null })
    }, [location.state, location.pathname, navigate])

    const carregar = useCallback(async () => {
        try {
            setUsuarios(await listarUsuarios())
            setErroLista('')
        } catch (err) {
            setErroLista(mensagemDeErro(err))
        } finally {
            setCarregando(false)
        }
    }, [])

    useEffect(() => {
        carregar()
    }, [carregar])

    const mostrarSenha = (nome, senha) => {
        setSenhaGerada({ nome, senha })
        setCopiado(false)
    }

    const trocarNaLista = (atualizado) => setUsuarios((lista) => lista.map((u) => (u.id === atualizado.id ? atualizado : u)))

    const redefinir = async (u) => {
        const ok = window.confirm(
            `Gerar uma senha temporária para ${u.nome}?\n\nA senha atual deixa de valer e ${u.nome} vai criar uma senha nova no próximo acesso.`
        )
        if (!ok) return
        setOcupado(u.id)
        setErroLista('')
        try {
            const { senhaTemporaria } = await redefinirSenhaUsuario(u.id)
            mostrarSenha(u.nome, senhaTemporaria)
        } catch (err) {
            setErroLista(mensagemDeErro(err))
        } finally {
            setOcupado(null)
        }
    }

    const alternarStatus = async (u) => {
        if (u.ativo && !window.confirm(`Inativar ${u.nome}? A pessoa não vai mais conseguir entrar.`)) return
        setOcupado(u.id)
        setErroLista('')
        try {
            trocarNaLista(await alterarStatusUsuario(u.id, !u.ativo))
        } catch (err) {
            setErroLista(mensagemDeErro(err))
        } finally {
            setOcupado(null)
        }
    }

    const copiar = async () => {
        try {
            await navigator.clipboard.writeText(senhaGerada.senha)
            setCopiado(true)
        } catch {
            /* sem permissão para a área de transferência: a senha continua visível para anotar */
        }
    }

    return (
        <section className="usuarios">
            <div className="usuarios__topo">
                <div>
                    <h1 className="titulo-pagina">Usuários</h1>
                    <p className="subtitulo-pagina">Somente administradores veem esta tela.</p>
                </div>
                <Link to="/usuarios/novo" className="botao-primario">
                    Novo usuário
                </Link>
            </div>

            {senhaGerada && (
                <div className="usuarios__senha" role="status">
                    <div>
                        <p className="usuarios__senha-titulo">
                            Senha temporária de {senhaGerada.nome}: <code>{senhaGerada.senha}</code>
                        </p>
                        <p className="usuarios__senha-texto">
                            Anote agora: ela não será mostrada de novo. No primeiro acesso, será pedida uma senha nova.
                        </p>
                    </div>
                    <div className="usuarios__senha-acoes">
                        <button type="button" className="botao-secundario usuarios__botao" onClick={copiar}>
                            {copiado ? 'Copiada!' : 'Copiar'}
                        </button>
                        <button
                            type="button"
                            className="usuarios__fechar"
                            onClick={() => setSenhaGerada(null)}
                            aria-label="Fechar aviso da senha temporária"
                            title="Fechar"
                        >
                            ×
                        </button>
                    </div>
                </div>
            )}

            <div className="form-cartao">
                <h2 className="usuarios__secao">Usuários cadastrados</h2>

                {erroLista && (
                    <p className="form-erro" role="alert">
                        {erroLista}
                    </p>
                )}

                {carregando ? (
                    <p className="form-campo__dica" role="status">
                        Carregando usuários…
                    </p>
                ) : (
                    <div className="usuarios__rolagem">
                        <table className="usuarios__tabela">
                            <thead>
                                <tr>
                                    <th scope="col">Nome</th>
                                    <th scope="col">E-mail</th>
                                    <th scope="col">Perfil</th>
                                    <th scope="col">Situação</th>
                                    <th scope="col">Ação</th>
                                </tr>
                            </thead>
                            <tbody>
                                {usuarios.map((u) => {
                                    const souEu = u.id === logado?.id
                                    return (
                                        <tr key={u.id}>
                                            <td>
                                                {u.nome}
                                                {souEu && <span className="usuarios__voce"> (você)</span>}
                                            </td>
                                            <td>{u.email}</td>
                                            <td>{u.perfil}</td>
                                            <td>
                                                <Etiqueta tipo={u.ativo ? 'ok' : 'atencao'}>{u.ativo ? 'Ativo' : 'Inativo'}</Etiqueta>
                                            </td>
                                            <td>
                                                {souEu ? (
                                                    <span className="form-campo__dica">—</span>
                                                ) : (
                                                    <div className="usuarios__acoes">
                                                        {u.ativo && (
                                                            <button
                                                                type="button"
                                                                className="botao-secundario usuarios__botao"
                                                                onClick={() => redefinir(u)}
                                                                disabled={ocupado === u.id}
                                                            >
                                                                Redefinir senha
                                                            </button>
                                                        )}
                                                        <button
                                                            type="button"
                                                            className="botao-secundario usuarios__botao"
                                                            onClick={() => alternarStatus(u)}
                                                            disabled={ocupado === u.id}
                                                        >
                                                            {u.ativo ? 'Inativar' : 'Ativar'}
                                                        </button>
                                                    </div>
                                                )}
                                            </td>
                                        </tr>
                                    )
                                })}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>
        </section>
    )
}
