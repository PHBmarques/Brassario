import { useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { mensagemDeErro } from '../Api/erros'
import { useAuth } from '../Auth/AuthContext.jsx'
import Logo from '../components/Logo.jsx'
import ThemeToggle from '../components/ThemeToggle.jsx'
import './Login.css'

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

const MSG_CREDENCIAIS = 'E-mail ou senha incorretos. Confira e tente de novo.'

function validar({ email, senha }) {
    const erros = {}
    if (!email.trim()) {
        erros.email = 'Informe o e-mail.'
    } else if (!EMAIL_REGEX.test(email.trim())) {
        erros.email = 'Digite um e-mail válido.'
    }
    if (!senha) {
        erros.senha = 'Informe a senha.'
    }
    return erros
}

export default function Login() {
    const { login, isAuthenticated, carregando: restaurando } = useAuth()
    const navigate = useNavigate()
    const location = useLocation()
    const destino = location.state?.from?.pathname ?? '/'

    const [form, setForm] = useState({ email: '', senha: '' })
    const [manterConectado, setManterConectado] = useState(false)
    const [mostrarSenha, setMostrarSenha] = useState(false)
    const [erros, setErros] = useState({})
    const [erroEnvio, setErroEnvio] = useState('')
    const [carregando, setCarregando] = useState(false)

    // Ainda tentando recuperar a sessão: não mostra o formulário para não piscar
    if (restaurando) return null

    if (isAuthenticated) {
        return <Navigate to={destino} replace />
    }

    const aoDigitar = (evento) => {
        const { name, value } = evento.target
        setForm((anterior) => ({ ...anterior, [name]: value }))
        if (erros[name]) setErros((anterior) => ({ ...anterior, [name]: undefined }))
        if (erroEnvio) setErroEnvio('')
    }

    const aoEnviar = async (evento) => {
        evento.preventDefault()
        const encontrados = validar(form)
        setErros(encontrados)
        if (Object.keys(encontrados).length > 0) return

        setCarregando(true)
        setErroEnvio('')
        try {
            await login(form.email.trim(), form.senha, manterConectado)
            navigate(destino, { replace: true })
        } catch (erro) {
            // 401 -> mostra a mensagem ao usuário
            setErroEnvio(erro.response?.status === 401 ? MSG_CREDENCIAIS : mensagemDeErro(erro))
            setCarregando(false)
        }
    }

    return (
        <main className="login">
            <ThemeToggle className="login__toggle-tema" />

            <section className="login__marca">
                <Logo tamanho={120} />
                <h1 className="login__nome">Brassário</h1>
                <p className="login__slogan">Do armário à brassagem</p>
            </section>

            <section className="login__painel">
                <div className="login__caixa">
                    <h2 className="login__titulo">Entrar</h2>
                    <p className="login__subtitulo">
                        Use o e-mail e a senha que o administrador criou para você.
                    </p>

                    <form onSubmit={aoEnviar} noValidate>
                        <div className="campo">
                            <label htmlFor="email">E-mail</label>
                            <input
                                id="email"
                                name="email"
                                type="email"
                                inputMode="email"
                                autoComplete="username"
                                value={form.email}
                                onChange={aoDigitar}
                                aria-invalid={Boolean(erros.email)}
                                aria-describedby={erros.email ? 'email-erro' : undefined}
                                disabled={carregando}
                            />
                            {erros.email && (
                                <p id="email-erro" className="campo__erro">{erros.email}</p>
                            )}
                        </div>

                        <div className="campo">
                            <label htmlFor="senha">Senha</label>
                            <div className="campo__linha">
                                <input
                                    id="senha"
                                    name="senha"
                                    type={mostrarSenha ? 'text' : 'password'}
                                    autoComplete="current-password"
                                    value={form.senha}
                                    onChange={aoDigitar}
                                    aria-invalid={Boolean(erros.senha)}
                                    aria-describedby={erros.senha ? 'senha-erro' : undefined}
                                    disabled={carregando}
                                />
                                <button
                                    type="button"
                                    className="login__mostrar"
                                    onClick={() => setMostrarSenha((v) => !v)}
                                    aria-pressed={mostrarSenha}
                                >
                                    {mostrarSenha ? 'Ocultar' : 'Mostrar'}
                                </button>
                            </div>
                            {erros.senha && (
                                <p id="senha-erro" className="campo__erro">{erros.senha}</p>
                            )}
                        </div>

                        <label className="login__lembrar">
                            <input
                                type="checkbox"
                                checked={manterConectado}
                                onChange={(e) => setManterConectado(e.target.checked)}
                                disabled={carregando}
                            />
                            Manter conectado neste aparelho
                        </label>

                        {erroEnvio && (
                            <p className="login__alerta" role="alert">{erroEnvio}</p>
                        )}

                        <button type="submit" className="login__botao" disabled={carregando}>
                            {carregando ? 'Entrando…' : 'Entrar'}
                        </button>
                    </form>

                    <p className="login__ajuda">Sem acesso? Fale com o administrador.</p>
                </div>
            </section>
        </main>
    )
}
