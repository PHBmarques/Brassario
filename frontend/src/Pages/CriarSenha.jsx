import { useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { primeiroAcesso } from '../Api/auth'
import { errosPorCampo, mensagemDeErro } from '../Api/erros'
import { useAuth } from '../Auth/AuthContext.jsx'
import CampoSenha from '../components/CampoSenha.jsx'
import TelaAcesso from '../components/TelaAcesso.jsx'
import { EMAIL_REGEX, MINIMO_SENHA, validarNovaSenha } from '../Utils/senha.js'

// Primeiro acesso: a pessoa entrou com a senha temporária e cria a própria senha.
// O e-mail de recuperação é opcional ("Pular por agora" salva só a senha).
export default function CriarSenha() {
    const { usuario, aplicarSessao, logout } = useAuth()
    const navigate = useNavigate()

    const [form, setForm] = useState({ novaSenha: '', confirmarSenha: '', emailRecuperacao: '' })
    const [erros, setErros] = useState({})
    const [erroEnvio, setErroEnvio] = useState('')
    const [salvando, setSalvando] = useState(false)

    if (!usuario?.deveTrocarSenha) return <Navigate to="/" replace />

    const aoDigitar = (e) => {
        const { name, value } = e.target
        setForm((f) => ({ ...f, [name]: value }))
        if (erros[name]) setErros((atual) => ({ ...atual, [name]: undefined }))
        if (erroEnvio) setErroEnvio('')
    }

    const salvar = async (comEmail) => {
        const email = comEmail ? form.emailRecuperacao.trim() : ''
        const encontrados = validarNovaSenha(form)
        if (email && !EMAIL_REGEX.test(email)) encontrados.emailRecuperacao = 'Digite um e-mail válido.'
        else if (email && email.toLowerCase() === usuario.email?.toLowerCase())
            encontrados.emailRecuperacao = 'Use um e-mail diferente do e-mail de login.'
        setErros(encontrados)
        if (Object.keys(encontrados).length > 0) return

        setSalvando(true)
        setErroEnvio('')
        try {
            const sessao = await primeiroAcesso({
                novaSenha: form.novaSenha,
                confirmarSenha: form.confirmarSenha,
                emailRecuperacao: email || undefined,
            })
            await aplicarSessao(sessao)
            // Com e-mail, Minha conta mostra o aviso e o selo "Aguardando confirmação"
            if (email) {
                navigate('/minha-conta', {
                    replace: true,
                    state: { aviso: `Senha criada! Enviamos um link de confirmação para ${email}. Ele vale por 24 horas.` },
                })
            } else {
                navigate('/', { replace: true })
            }
        } catch (erro) {
            setErros(errosPorCampo(erro))
            setErroEnvio(mensagemDeErro(erro))
            setSalvando(false)
        }
    }

    const propsCampo = (campo) => ({
        id: campo,
        name: campo,
        value: form[campo],
        onChange: aoDigitar,
        disabled: salvando,
        'aria-invalid': Boolean(erros[campo]),
        'aria-describedby': erros[campo] ? `${campo}-erro` : `${campo}-dica`,
    })

    const erroDe = (campo) =>
        erros[campo] && (
            <p id={`${campo}-erro`} className="campo__erro">
                {erros[campo]}
            </p>
        )

    return (
        <TelaAcesso larga>
            <h2 className="login__titulo">Crie sua senha</h2>
            <p className="login__subtitulo">Sua senha atual é temporária. Escolha uma senha só sua para continuar.</p>

            <form
                noValidate
                onSubmit={(e) => {
                    e.preventDefault()
                    salvar(true)
                }}
            >
                <div className="campo">
                    <label htmlFor="novaSenha">Nova senha *</label>
                    <CampoSenha autoComplete="new-password" {...propsCampo('novaSenha')} />
                    {erroDe('novaSenha') || (
                        <p id="novaSenha-dica" className="campo__dica">
                            Mínimo de {MINIMO_SENHA} caracteres.
                        </p>
                    )}
                </div>

                <div className="campo">
                    <label htmlFor="confirmarSenha">Confirmar nova senha *</label>
                    <CampoSenha autoComplete="new-password" {...propsCampo('confirmarSenha')} />
                    {erroDe('confirmarSenha')}
                </div>

                <hr className="login__divisor" />

                <h3 className="login__secao">E-mail para recuperar a senha</h3>
                <p className="login__subtitulo">
                    Opcional, mas recomendado. Se você esquecer a senha, enviamos o link para este e-mail. Pode ser o
                    seu e-mail pessoal.
                </p>

                <div className="campo">
                    <label htmlFor="emailRecuperacao">E-mail pessoal</label>
                    <input type="email" inputMode="email" autoComplete="email" {...propsCampo('emailRecuperacao')} />
                    {erroDe('emailRecuperacao')}
                </div>

                {erroEnvio && (
                    <p className="login__alerta" role="alert">
                        {erroEnvio}
                    </p>
                )}

                <div className="login__acoes">
                    <button
                        type="button"
                        className="login__botao login__botao--secundario"
                        onClick={() => salvar(false)}
                        disabled={salvando}
                        title="Salva só a senha. Você pode cadastrar o e-mail depois em Minha conta."
                    >
                        Pular por agora
                    </button>
                    <button type="submit" className="login__botao" disabled={salvando}>
                        {salvando ? 'Salvando…' : 'Salvar e entrar'}
                    </button>
                </div>
            </form>

            <p className="login__ajuda">
                Não é {usuario.nome}?{' '}
                <button type="button" className="login__link login__link--botao" onClick={logout}>
                    Sair
                </button>
            </p>
        </TelaAcesso>
    )
}
