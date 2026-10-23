import { useState } from 'react'
import { Link } from 'react-router-dom'
import { esqueciSenha } from '../Api/auth'
import { mensagemDeErro } from '../Api/erros'
import TelaAcesso from '../components/TelaAcesso.jsx'
import { EMAIL_REGEX } from '../Utils/senha.js'

// "Esqueci a senha": pede o link de redefinição. A API responde igual exista ou não a conta.
export default function EsqueciSenha() {
    const [email, setEmail] = useState('')
    const [erro, setErro] = useState('')
    const [erroEnvio, setErroEnvio] = useState('')
    const [enviando, setEnviando] = useState(false)
    const [enviado, setEnviado] = useState('')

    const enviar = async (e) => {
        e.preventDefault()
        const valor = email.trim()
        if (!valor) return setErro('Informe o e-mail.')
        if (!EMAIL_REGEX.test(valor)) return setErro('Digite um e-mail válido.')

        setEnviando(true)
        setErroEnvio('')
        try {
            const resposta = await esqueciSenha(valor)
            setEnviado(
                resposta?.mensagem ??
                    'Se existir uma conta com esse e-mail e com e-mail de recuperação confirmado, enviamos um link para redefinir a senha.'
            )
        } catch (err) {
            setErroEnvio(mensagemDeErro(err))
        } finally {
            setEnviando(false)
        }
    }

    if (enviado) {
        return (
            <TelaAcesso>
                <h2 className="login__titulo">Confira seu e-mail</h2>
                <div className="login__sucesso" role="status">
                    <strong>Pedido recebido.</strong>
                    {enviado} O link vale por 30 minutos.
                </div>
                <p className="login__subtitulo">
                    Não chegou? Veja a caixa de spam. Se você não cadastrou um e-mail de recuperação, peça ao
                    administrador uma senha temporária.
                </p>
                <Link to="/login" className="login__botao">
                    Voltar para o login
                </Link>
            </TelaAcesso>
        )
    }

    return (
        <TelaAcesso>
            <h2 className="login__titulo">Esqueci a senha</h2>
            <p className="login__subtitulo">
                Informe o e-mail que você usa para entrar. Enviamos um link para o seu e-mail de recuperação.
            </p>

            <form onSubmit={enviar} noValidate>
                <div className="campo">
                    <label htmlFor="email">E-mail de login</label>
                    <input
                        id="email"
                        type="email"
                        inputMode="email"
                        autoComplete="username"
                        value={email}
                        onChange={(e) => {
                            setEmail(e.target.value)
                            setErro('')
                        }}
                        aria-invalid={Boolean(erro)}
                        aria-describedby={erro ? 'email-erro' : undefined}
                        disabled={enviando}
                    />
                    {erro && (
                        <p id="email-erro" className="campo__erro">
                            {erro}
                        </p>
                    )}
                </div>

                {erroEnvio && (
                    <p className="login__alerta" role="alert">
                        {erroEnvio}
                    </p>
                )}

                <div className="login__acoes">
                    <Link to="/login" className="login__botao login__botao--secundario">
                        Voltar
                    </Link>
                    <button type="submit" className="login__botao" disabled={enviando}>
                        {enviando ? 'Enviando…' : 'Enviar link'}
                    </button>
                </div>
            </form>
        </TelaAcesso>
    )
}
