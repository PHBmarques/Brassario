import { useState } from 'react'
import { alterarEmailRecuperacao } from '../Api/auth'
import { errosPorCampo, mensagemDeErro } from '../Api/erros'
import { useAuth } from '../Auth/AuthContext.jsx'
import { EMAIL_REGEX } from '../Utils/senha.js'
import Etiqueta from './Etiqueta.jsx'
import './Formulario.css'

// E-mail de recuperação da pessoa logada: mostra a situação e permite trocar (ou reenviar o link).
// Usado em Minha conta e em Alterar minha senha.
export default function EmailRecuperacao({ compacto = false }) {
    const { usuario, recarregarUsuario } = useAuth()
    const atual = usuario?.emailRecuperacao ?? ''
    const confirmado = Boolean(usuario?.emailRecuperacaoConfirmado)

    const [editando, setEditando] = useState(false)
    const [email, setEmail] = useState('')
    const [erro, setErro] = useState('')
    const [salvando, setSalvando] = useState(false)
    const [aviso, setAviso] = useState('')

    const enviar = async (valor) => {
        setSalvando(true)
        setErro('')
        try {
            const resposta = await alterarEmailRecuperacao(valor)
            setEditando(false)
            setAviso(resposta?.mensagem ?? `Enviamos um link de confirmação para ${valor}.`)
            // Atualiza o selo para "Aguardando confirmação"
            await recarregarUsuario().catch(() => {})
        } catch (err) {
            setErro(errosPorCampo(err).emailRecuperacao ?? mensagemDeErro(err))
        } finally {
            setSalvando(false)
        }
    }

    const salvar = (e) => {
        e.preventDefault()
        const valor = email.trim()
        if (!valor) return setErro('Informe o e-mail.')
        if (!EMAIL_REGEX.test(valor)) return setErro('Digite um e-mail válido.')
        if (valor.toLowerCase() === usuario?.email?.toLowerCase()) return setErro('Use um e-mail diferente do e-mail de login.')
        enviar(valor)
    }

    const abrir = () => {
        setEmail(atual)
        setErro('')
        setAviso('')
        setEditando(true)
    }

    const situacao = atual && (
        <Etiqueta tipo={confirmado ? 'ok' : 'atencao'}>{confirmado ? 'Confirmado' : 'Aguardando confirmação'}</Etiqueta>
    )

    if (editando) {
        return (
            <form className="email-recuperacao__form" onSubmit={salvar} noValidate>
                <div className="form-campo">
                    <label htmlFor="emailRecuperacao">E-mail de recuperação</label>
                    <input
                        id="emailRecuperacao"
                        type="email"
                        inputMode="email"
                        autoComplete="email"
                        value={email}
                        onChange={(e) => {
                            setEmail(e.target.value)
                            setErro('')
                        }}
                        aria-invalid={Boolean(erro)}
                        aria-describedby={erro ? 'emailRecuperacao-erro' : 'emailRecuperacao-dica'}
                        disabled={salvando}
                        autoFocus
                    />
                    {erro ? (
                        <span id="emailRecuperacao-erro" className="form-campo__erro">
                            {erro}
                        </span>
                    ) : (
                        <span id="emailRecuperacao-dica" className="form-campo__dica">
                            Pode ser o seu e-mail pessoal. Enviamos um link para confirmar.
                        </span>
                    )}
                </div>
                <div className="form-acoes">
                    <button type="button" className="botao-secundario" onClick={() => setEditando(false)} disabled={salvando}>
                        Cancelar
                    </button>
                    <button type="submit" className="botao-primario" disabled={salvando}>
                        {salvando ? 'Enviando…' : 'Salvar e enviar link'}
                    </button>
                </div>
            </form>
        )
    }

    return (
        <div className={`email-recuperacao${compacto ? ' email-recuperacao--compacto' : ''}`}>
            {compacto ? (
                <div className="email-recuperacao__valor">
                    <span>{atual || 'Nenhum e-mail cadastrado.'}</span>
                    {situacao}
                </div>
            ) : (
                <dl className="conta__dados">
                    <div>
                        <dt>E-mail de recuperação</dt>
                        <dd>
                            <span className="email-recuperacao__valor">
                                <strong>{atual || 'Nenhum e-mail cadastrado'}</strong>
                                {situacao}
                            </span>
                        </dd>
                    </div>
                </dl>
            )}

            <p className="form-campo__dica">
                {atual
                    ? 'É para este endereço que enviamos o link se você esquecer a senha.'
                    : 'Cadastre um e-mail para poder recuperar a senha sozinho, sem pedir ao administrador.'}
            </p>

            {aviso && (
                <p className="conta__aviso" role="status">
                    {aviso}
                </p>
            )}
            {erro && (
                <p className="form-erro" role="alert">
                    {erro}
                </p>
            )}

            <div className="email-recuperacao__acoes">
                <button type="button" className="botao-secundario" onClick={abrir}>
                    {atual ? 'Alterar e-mail' : 'Cadastrar e-mail'}
                </button>
                {atual && !confirmado && (
                    <button type="button" className="form-campo__acao" onClick={() => enviar(atual)} disabled={salvando}>
                        {salvando ? 'Enviando…' : 'Reenviar link de confirmação'}
                    </button>
                )}
            </div>
        </div>
    )
}
