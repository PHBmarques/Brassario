import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { errosPorCampo, mensagemDeErro } from '../Api/erros'
import { criarUsuario } from '../Api/usuarios'
import { PERFIS, emailSugerido, novoFormulario, validarCadastro } from '../Utils/cadastroUsuario.js'
import { gerarSenhaTemporaria } from '../Utils/senha.js'
import '../components/Formulario.css'
import './Usuarios.css'

// Somente ADMIN (protegida em App.jsx). Ao cadastrar, volta para a lista mostrando a senha temporária.
export default function NovoUsuario() {
    const navigate = useNavigate()
    const [form, setForm] = useState(novoFormulario)
    const [erros, setErros] = useState({})
    const [erroForm, setErroForm] = useState('')
    const [salvando, setSalvando] = useState(false)

    // Enquanto o administrador não mexer no e-mail, ele acompanha o nome (nome.sobrenome@brassario.com)
    const [emailManual, setEmailManual] = useState(false)

    const alterar = (campo) => (e) => {
        const valor = e.target.value
        setForm((f) => {
            const novo = { ...f, [campo]: valor }
            if (campo === 'nome' && !emailManual) novo.email = emailSugerido(valor)
            return novo
        })
        if (campo === 'email') setEmailManual(valor.trim() !== '')
        setErros((atual) => {
            if (!atual[campo] && !(campo === 'nome' && atual.email)) return atual
            return { ...atual, [campo]: undefined, ...(campo === 'nome' && !emailManual ? { email: undefined } : {}) }
        })
    }

    const cadastrar = async (e) => {
        e.preventDefault()
        const encontrados = validarCadastro(form)
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
            navigate('/usuarios', { state: { senhaGerada: { nome: criado.nome, senha: form.senha } } })
        } catch (err) {
            setErros(errosPorCampo(err))
            setErroForm(mensagemDeErro(err))
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
        <section className="usuarios usuarios--novo">
            <Link to="/usuarios" className="usuarios__voltar">
                ‹ Voltar aos usuários
            </Link>
            <div>
                <h1 className="titulo-pagina">Novo usuário</h1>
                <p className="subtitulo-pagina">Cadastre quem vai usar o Brassário. Campos com * são obrigatórios.</p>
            </div>

            <div className="usuarios__grade">
            <form className="form-cartao usuarios__form" onSubmit={cadastrar} noValidate>
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
                        placeholder="Ex.: carlos.lima@brassario.com"
                        {...propsCampo('email')}
                    />
                    {erroDe('email') || (
                        <span className="form-campo__dica" id="email-dica">
                            Sugerido a partir do nome; pode ser editado. É só o e-mail de login, não precisa existir de verdade.
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
                    <Link to="/usuarios" className="botao-cancelar" aria-disabled={salvando}>
                        Cancelar
                    </Link>
                    <button type="submit" className="botao-primario" disabled={salvando}>
                        {salvando ? 'Cadastrando…' : 'Cadastrar usuário'}
                    </button>
                </div>
            </form>

            <aside className="form-cartao usuarios__perfis" aria-label="O que cada perfil pode fazer">
                <h2 className="usuarios__perfis-titulo">O que cada perfil pode fazer</h2>
                <div>
                    <h3 className="usuarios__perfis-nome">ADMIN</h3>
                    <ul className="usuarios__perfis-lista">
                        <li>Acessa todas as telas do sistema</li>
                        <li>Cadastra insumos e receitas</li>
                        <li>Faz ajustes de estoque</li>
                        <li>Cadastra e gerencia usuários</li>
                    </ul>
                </div>
                <div>
                    <h3 className="usuarios__perfis-nome">OPERADOR</h3>
                    <ul className="usuarios__perfis-lista">
                        <li>Registra entradas no estoque</li>
                        <li>Produz bateladas</li>
                        <li>Consulta o armário e os alertas</li>
                        <li>Não edita receitas nem gerencia usuários</li>
                    </ul>
                </div>
                <p className="form-campo__dica">
                    Depois de cadastrar, a senha temporária aparece na lista de usuários, com o botão de copiar.
                </p>
            </aside>
            </div>
        </section>
    )
}
