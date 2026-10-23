import { useState } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { mensagemDeErro } from '../Api/erros'
import { useAuth } from '../Auth/AuthContext.jsx'
import CampoSenha from '../components/CampoSenha.jsx'
import TelaAcesso from '../components/TelaAcesso.jsx'

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
        <TelaAcesso>
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
                    <CampoSenha
                        id="senha"
                        name="senha"
                        autoComplete="current-password"
                        value={form.senha}
                        onChange={aoDigitar}
                        aria-invalid={Boolean(erros.senha)}
                        aria-describedby={erros.senha ? 'senha-erro' : undefined}
                        disabled={carregando}
                    />
                    {erros.senha && (
                        <p id="senha-erro" className="campo__erro">{erros.senha}</p>
                    )}
                </div>

                <div className="login__opcoes">
                    <label className="login__lembrar">
                        <input
                            type="checkbox"
                            checked={manterConectado}
                            onChange={(e) => setManterConectado(e.target.checked)}
                            disabled={carregando}
                        />
                        Manter conectado neste aparelho
                    </label>
                    <Link to="/esqueci-senha" className="login__link">
                        Esqueci a senha
                    </Link>
                </div>

                {erroEnvio && (
                    <p className="login__alerta" role="alert">{erroEnvio}</p>
                )}

                <button type="submit" className="login__botao" disabled={carregando}>
                    {carregando ? 'Entrando…' : 'Entrar'}
                </button>
            </form>

            <p className="login__ajuda">Sem acesso? Fale com o administrador.</p>
        </TelaAcesso>
    )
}
