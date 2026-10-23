import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { alterarSenha } from '../Api/auth'
import { errosPorCampo, mensagemDeErro } from '../Api/erros'
import { useAuth } from '../Auth/AuthContext.jsx'
import CampoSenha from '../components/CampoSenha.jsx'
import EmailRecuperacao from '../components/EmailRecuperacao.jsx'
import { MINIMO_SENHA, validarNovaSenha } from '../Utils/senha.js'
import '../components/Formulario.css'
import './Conta.css'

const INICIAL = { senhaAtual: '', novaSenha: '', confirmarSenha: '' }

export default function AlterarSenha() {
    const { usuario } = useAuth()
    const navigate = useNavigate()
    const [form, setForm] = useState(INICIAL)
    const [erros, setErros] = useState({})
    const [erroGeral, setErroGeral] = useState('')
    const [salvando, setSalvando] = useState(false)

    const aoDigitar = (e) => {
        const { name, value } = e.target
        setForm((f) => ({ ...f, [name]: value }))
        if (erros[name]) setErros((atual) => ({ ...atual, [name]: undefined }))
    }

    const salvar = async (e) => {
        e.preventDefault()
        const encontrados = validarNovaSenha(form)
        if (!form.senhaAtual) encontrados.senhaAtual = 'Informe a senha atual.'
        else if (form.novaSenha && form.novaSenha === form.senhaAtual)
            encontrados.novaSenha = 'A nova senha deve ser diferente da atual.'
        setErros(encontrados)
        setErroGeral('')
        if (Object.keys(encontrados).length > 0) return

        setSalvando(true)
        try {
            await alterarSenha(form)
            navigate('/minha-conta', {
                state: { aviso: 'Senha alterada. Os outros aparelhos conectados foram desconectados.' },
            })
        } catch (err) {
            if (err.response?.data?.codigo === 'SENHA_ATUAL_INCORRETA') {
                setErros({ senhaAtual: 'A senha atual está incorreta.' })
            } else {
                setErros(errosPorCampo(err))
                setErroGeral(mensagemDeErro(err))
            }
            setSalvando(false)
        }
    }

    const campo = (nome, rotulo, autoComplete, dica) => (
        <div className="form-campo">
            <label htmlFor={nome}>{rotulo}</label>
            <CampoSenha
                id={nome}
                name={nome}
                autoComplete={autoComplete}
                value={form[nome]}
                onChange={aoDigitar}
                disabled={salvando}
                aria-invalid={Boolean(erros[nome])}
                aria-describedby={erros[nome] ? `${nome}-erro` : dica ? `${nome}-dica` : undefined}
            />
            {erros[nome] ? (
                <span id={`${nome}-erro`} className="form-campo__erro">
                    {erros[nome]}
                </span>
            ) : (
                dica && (
                    <span id={`${nome}-dica`} className="form-campo__dica">
                        {dica}
                    </span>
                )
            )}
        </div>
    )

    return (
        <section className="conta conta--larga">
            <h1 className="titulo-pagina">Alterar minha senha</h1>
            <p className="subtitulo-pagina">Conta: {usuario?.email}</p>

            <div className="conta__grade">
                <form className="form-cartao" onSubmit={salvar} noValidate>
                    {erroGeral && (
                        <p className="form-erro" role="alert">
                            {erroGeral}
                        </p>
                    )}
                    {/* Ajuda o gerenciador de senhas do navegador a saber de qual conta é a senha */}
                    <input type="text" name="username" autoComplete="username" value={usuario?.email ?? ''} readOnly hidden />
                    {campo('senhaAtual', 'Senha atual *', 'current-password')}
                    {campo('novaSenha', 'Nova senha *', 'new-password', `Mínimo de ${MINIMO_SENHA} caracteres.`)}
                    {campo('confirmarSenha', 'Confirmar nova senha *', 'new-password')}

                    <div className="form-acoes">
                        <Link to="/minha-conta" className="botao-secundario">
                            Cancelar
                        </Link>
                        <button type="submit" className="botao-primario" disabled={salvando}>
                            {salvando ? 'Salvando…' : 'Salvar senha'}
                        </button>
                    </div>
                </form>

                <aside className="form-cartao">
                    <h2 className="conta__secao">E-mail de recuperação</h2>
                    <EmailRecuperacao compacto />
                </aside>
            </div>
        </section>
    )
}
