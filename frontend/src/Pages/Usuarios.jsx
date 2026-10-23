import { useCallback, useEffect, useState } from 'react'
import { errosPorCampo, mensagemDeErro } from '../Api/erros'
import { alterarStatusUsuario, criarUsuario, listarUsuarios, redefinirSenhaUsuario } from '../Api/usuarios'
import { useAuth } from '../Auth/AuthContext.jsx'
import Etiqueta from '../components/Etiqueta.jsx'
import { EMAIL_REGEX, MINIMO_SENHA, gerarSenhaTemporaria } from '../Utils/senha.js'
import '../components/Formulario.css'
import './Usuarios.css'

const PERFIS = [
    { valor: 'OPERADOR', dica: 'O operador faz produções e dá entrada no estoque, mas não edita receitas.' },
    { valor: 'ADMIN', dica: 'O administrador pode tudo: cadastros, receitas, ajustes de estoque e usuários.' },
]

const novoFormulario = () => ({ nome: '', email: '', perfil: 'OPERADOR', senha: gerarSenhaTemporaria() })

function validar(f) {
    const erros = {}
    if (!f.nome.trim()) erros.nome = 'Informe o nome.'
    if (!f.email.trim()) erros.email = 'Informe o e-mail.'
    else if (!EMAIL_REGEX.test(f.email.trim())) erros.email = 'Digite um e-mail válido.'
    if (!f.senha) erros.senha = 'Informe a senha temporária.'
    else if (f.senha.length < MINIMO_SENHA) erros.senha = `Mínimo de ${MINIMO_SENHA} caracteres.`
    return erros
}

// Somente ADMIN (protegida em App.jsx)
export default function Usuarios() {
    const { usuario: logado } = useAuth()
    const [usuarios, setUsuarios] = useState([])
    const [carregando, setCarregando] = useState(true)
    const [erroLista, setErroLista] = useState('')
    const [ocupado, setOcupado] = useState(null) // id da linha com ação em andamento
    const [senhaGerada, setSenhaGerada] = useState(null) // { nome, senha }
    const [copiado, setCopiado] = useState(false)

    const [form, setForm] = useState(novoFormulario)
    const [erros, setErros] = useState({})
    const [erroForm, setErroForm] = useState('')
    const [salvando, setSalvando] = useState(false)

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
            const r = await redefinirSenhaUsuario(u.id)
            trocarNaLista(r.usuario)
            mostrarSenha(r.usuario.nome, r.senhaTemporaria)
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

    // ---------- Novo usuário ----------
    const alterar = (campo) => (e) => {
        setForm((f) => ({ ...f, [campo]: e.target.value }))
        setErros((atual) => (atual[campo] ? { ...atual, [campo]: undefined } : atual))
    }

    const limpar = () => {
        setForm(novoFormulario())
        setErros({})
        setErroForm('')
    }

    const cadastrar = async (e) => {
        e.preventDefault()
        const encontrados = validar(form)
        setErros(encontrados)
        setErroForm('')
        if (Object.keys(encontrados).length > 0) return

        setSalvando(true)
        try {
            const criado = await criarUsuario({
                nome: form.nome.trim(),
                email: form.email.trim(),
                perfil: form.perfil,
                senha: form.senha,
            })
            setUsuarios((lista) => [...lista, criado].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR')))
            mostrarSenha(criado.nome, form.senha)
            limpar()
        } catch (err) {
            setErros(errosPorCampo(err))
            setErroForm(mensagemDeErro(err))
        } finally {
            setSalvando(false)
        }
    }

    const erroDe = (campo) =>
        erros[campo] && (
            <span className="form-campo__erro" id={`${campo}-erro`}>
                {erros[campo]}
            </span>
        )

    const propsCampo = (campo) => ({
        id: campo,
        value: form[campo],
        onChange: alterar(campo),
        disabled: salvando,
        'aria-invalid': Boolean(erros[campo]),
        'aria-describedby': erros[campo] ? `${campo}-erro` : `${campo}-dica`,
    })

    const dicaPerfil = PERFIS.find((p) => p.valor === form.perfil)?.dica

    return (
        <section className="usuarios">
            <h1 className="titulo-pagina">Usuários</h1>
            <p className="subtitulo-pagina">Somente administradores veem esta tela.</p>

            <div className="form-cartao">
                <h2 className="usuarios__secao">Usuários cadastrados</h2>

                {senhaGerada && (
                    <div className="usuarios__senha" role="status">
                        <div>
                            <p className="usuarios__senha-titulo">
                                Senha temporária de {senhaGerada.nome}: <code>{senhaGerada.senha}</code>
                            </p>
                            <p className="usuarios__senha-texto">
                                Anote agora: ela não será mostrada de novo. No primeiro acesso, será pedida uma senha
                                nova.
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

            <form className="form-cartao usuarios__form" onSubmit={cadastrar} noValidate>
                <div>
                    <h2 className="usuarios__secao">Novo usuário</h2>
                    <p className="form-campo__dica">Campos com * são obrigatórios</p>
                </div>

                {erroForm && (
                    <p className="form-erro" role="alert">
                        {erroForm}
                    </p>
                )}

                <div className="form-campo">
                    <label htmlFor="nome">Nome *</label>
                    <input type="text" maxLength={120} autoComplete="off" placeholder="Ex.: Carlos Lima" {...propsCampo('nome')} />
                    {erroDe('nome')}
                </div>

                <div className="form-campo">
                    <label htmlFor="email">E-mail *</label>
                    <input
                        type="email"
                        maxLength={160}
                        autoComplete="off"
                        placeholder="Ex.: carlos@cervejaria.local"
                        {...propsCampo('email')}
                    />
                    {erroDe('email') || (
                        <span className="form-campo__dica" id="email-dica">
                            Este é o e-mail de login. Pode ser um endereço interno.
                        </span>
                    )}
                </div>

                <div className="form-campo">
                    <label htmlFor="perfil">Perfil *</label>
                    <select {...propsCampo('perfil')}>
                        {PERFIS.map((p) => (
                            <option key={p.valor} value={p.valor}>
                                {p.valor}
                            </option>
                        ))}
                    </select>
                    {erroDe('perfil') || (
                        <span className="form-campo__dica" id="perfil-dica">
                            {dicaPerfil}
                        </span>
                    )}
                </div>

                <div className="form-campo">
                    <div className="form-campo__rotulo">
                        <label htmlFor="senha">Senha temporária *</label>
                        <button
                            type="button"
                            className="form-campo__acao"
                            onClick={() => {
                                setForm((f) => ({ ...f, senha: gerarSenhaTemporaria() }))
                                setErros((atual) => ({ ...atual, senha: undefined }))
                            }}
                            disabled={salvando}
                        >
                            Gerar outra
                        </button>
                    </div>
                    <input type="text" autoComplete="off" spellCheck={false} {...propsCampo('senha')} />
                    {erroDe('senha') || (
                        <span className="form-campo__dica" id="senha-dica">
                            {form.perfil === 'ADMIN' ? 'O administrador' : 'O operador'} vai trocar esta senha no
                            primeiro acesso.
                        </span>
                    )}
                </div>

                <div className="form-acoes">
                    <button type="button" className="botao-secundario" onClick={limpar} disabled={salvando}>
                        Cancelar
                    </button>
                    <button type="submit" className="botao-primario" disabled={salvando}>
                        {salvando ? 'Cadastrando…' : 'Cadastrar usuário'}
                    </button>
                </div>
            </form>
        </section>
    )
}
