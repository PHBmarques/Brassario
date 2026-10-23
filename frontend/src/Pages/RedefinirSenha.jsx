import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { redefinirSenha } from '../Api/auth'
import { errosPorCampo, mensagemDeErro } from '../Api/erros'
import CampoSenha from '../components/CampoSenha.jsx'
import TelaAcesso from '../components/TelaAcesso.jsx'
import { MINIMO_SENHA, validarNovaSenha } from '../Utils/senha.js'

// Aberta pelo link do e-mail de "esqueci a senha": /redefinir-senha?token=...
export default function RedefinirSenha() {
    const [params] = useSearchParams()
    const token = params.get('token') ?? ''

    const [form, setForm] = useState({ novaSenha: '', confirmarSenha: '' })
    const [erros, setErros] = useState({})
    const [erroEnvio, setErroEnvio] = useState('')
    const [linkInvalido, setLinkInvalido] = useState(!token)
    const [salvando, setSalvando] = useState(false)
    const [pronto, setPronto] = useState(false)

    const aoDigitar = (e) => {
        const { name, value } = e.target
        setForm((f) => ({ ...f, [name]: value }))
        if (erros[name]) setErros((atual) => ({ ...atual, [name]: undefined }))
    }

    const salvar = async (e) => {
        e.preventDefault()
        const encontrados = validarNovaSenha(form)
        setErros(encontrados)
        if (Object.keys(encontrados).length > 0) return

        setSalvando(true)
        setErroEnvio('')
        try {
            await redefinirSenha({ token, ...form })
            setPronto(true)
        } catch (err) {
            if (err.response?.data?.codigo === 'TOKEN_INVALIDO') setLinkInvalido(true)
            setErros(errosPorCampo(err))
            setErroEnvio(mensagemDeErro(err))
        } finally {
            setSalvando(false)
        }
    }

    if (pronto) {
        return (
            <TelaAcesso>
                <h2 className="login__titulo">Senha alterada</h2>
                <div className="login__sucesso" role="status">
                    <strong>Tudo certo!</strong>
                    Entre com a senha nova. Por segurança, os aparelhos que estavam conectados foram desconectados.
                </div>
                <Link to="/login" className="login__botao">
                    Ir para o login
                </Link>
            </TelaAcesso>
        )
    }

    if (linkInvalido) {
        return (
            <TelaAcesso>
                <h2 className="login__titulo">Link inválido ou expirado</h2>
                <p className="login__subtitulo">
                    O link de redefinição vale por 30 minutos e só pode ser usado uma vez. Peça um novo para continuar.
                </p>
                <div className="login__acoes">
                    <Link to="/login" className="login__botao login__botao--secundario">
                        Voltar ao login
                    </Link>
                    <Link to="/esqueci-senha" className="login__botao">
                        Pedir novo link
                    </Link>
                </div>
            </TelaAcesso>
        )
    }

    const erroDe = (campo) =>
        erros[campo] && (
            <p id={`${campo}-erro`} className="campo__erro">
                {erros[campo]}
            </p>
        )

    return (
        <TelaAcesso>
            <h2 className="login__titulo">Crie uma senha nova</h2>
            <p className="login__subtitulo">Escolha uma senha que você não use em outros sites.</p>

            <form onSubmit={salvar} noValidate>
                <div className="campo">
                    <label htmlFor="novaSenha">Nova senha *</label>
                    <CampoSenha
                        id="novaSenha"
                        name="novaSenha"
                        autoComplete="new-password"
                        value={form.novaSenha}
                        onChange={aoDigitar}
                        disabled={salvando}
                        aria-invalid={Boolean(erros.novaSenha)}
                        aria-describedby={erros.novaSenha ? 'novaSenha-erro' : 'novaSenha-dica'}
                    />
                    {erroDe('novaSenha') || (
                        <p id="novaSenha-dica" className="campo__dica">
                            Mínimo de {MINIMO_SENHA} caracteres.
                        </p>
                    )}
                </div>

                <div className="campo">
                    <label htmlFor="confirmarSenha">Confirmar nova senha *</label>
                    <CampoSenha
                        id="confirmarSenha"
                        name="confirmarSenha"
                        autoComplete="new-password"
                        value={form.confirmarSenha}
                        onChange={aoDigitar}
                        disabled={salvando}
                        aria-invalid={Boolean(erros.confirmarSenha)}
                        aria-describedby={erros.confirmarSenha ? 'confirmarSenha-erro' : undefined}
                    />
                    {erroDe('confirmarSenha')}
                </div>

                {erroEnvio && (
                    <p className="login__alerta" role="alert">
                        {erroEnvio}
                    </p>
                )}

                <button type="submit" className="login__botao" disabled={salvando}>
                    {salvando ? 'Salvando…' : 'Salvar nova senha'}
                </button>
            </form>
        </TelaAcesso>
    )
}
