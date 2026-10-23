import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { confirmarEmail } from '../Api/auth'
import { useAuth } from '../Auth/AuthContext.jsx'
import TelaAcesso from '../components/TelaAcesso.jsx'

// O token só pode ser usado uma vez. Guardar a promessa por token evita que o StrictMode
// (que roda o efeito 2x em desenvolvimento) gaste o link na primeira chamada e mostre erro na segunda.
const pedidos = new Map()
function confirmarUmaVez(token) {
    if (!pedidos.has(token)) {
        pedidos.set(
            token,
            confirmarEmail(token).then(
                () => true,
                () => false
            )
        )
    }
    return pedidos.get(token)
}

// Aberta pelo link do e-mail de confirmação: /confirmar-email?token=...
export default function ConfirmarEmail() {
    const [params] = useSearchParams()
    const token = params.get('token') ?? ''
    const { isAuthenticated, carregando, recarregarUsuario } = useAuth()
    const [situacao, setSituacao] = useState(token ? 'confirmando' : 'invalido')

    useEffect(() => {
        if (!token) return undefined
        let ativo = true
        confirmarUmaVez(token).then((ok) => {
            if (ativo) setSituacao(ok ? 'confirmado' : 'invalido')
        })
        return () => {
            ativo = false
        }
    }, [token])

    // Quem já está logado vê o selo "Confirmado" em Minha conta sem recarregar a página
    useEffect(() => {
        if (situacao === 'confirmado' && isAuthenticated) recarregarUsuario().catch(() => {})
    }, [situacao, isAuthenticated, recarregarUsuario])

    const destino = isAuthenticated ? '/minha-conta' : '/login'
    const rotuloDestino = isAuthenticated ? 'Ir para Minha conta' : 'Ir para o login'

    return (
        <TelaAcesso>
            {situacao === 'confirmando' && (
                <>
                    <h2 className="login__titulo">Confirmando seu e-mail…</h2>
                    <p className="login__subtitulo" role="status">
                        Só um instante.
                    </p>
                </>
            )}

            {situacao === 'confirmado' && (
                <>
                    <h2 className="login__titulo">E-mail confirmado</h2>
                    <div className="login__sucesso" role="status">
                        <strong>Recuperação ativada.</strong>
                        Se você esquecer a senha, é só clicar em “Esqueci a senha” na tela de login.
                    </div>
                    {!carregando && (
                        <Link to={destino} className="login__botao">
                            {rotuloDestino}
                        </Link>
                    )}
                </>
            )}

            {situacao === 'invalido' && (
                <>
                    <h2 className="login__titulo">Link inválido ou expirado</h2>
                    <p className="login__subtitulo">
                        O link de confirmação vale por 24 horas e só pode ser usado uma vez. Entre no Brassário e, em
                        Minha conta, peça um novo link.
                    </p>
                    {!carregando && (
                        <Link to={destino} className="login__botao">
                            {rotuloDestino}
                        </Link>
                    )}
                </>
            )}
        </TelaAcesso>
    )
}
