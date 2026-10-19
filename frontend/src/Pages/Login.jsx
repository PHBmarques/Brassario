import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../Auth/AuthContext.jsx';
import './Login.css';

const MSG_CREDENCIAIS = 'E-mail ou senha incorretos. Confira e tente de novo.';
const MSG_SERVIDOR = 'Não foi possível conectar ao servidor. Tente novamente.';

function mensagemDeErro(err) {
    // Sem resposta da API: servidor desligado, proxy errado ou rede caiu
    if (!err?.response) return MSG_SERVIDOR;

    const { status, data } = err.response;
    if (status === 401) return MSG_CREDENCIAIS;
    if (status === 400) return data?.mensagem || 'Confira os dados informados.';
    if (status >= 500) return 'Erro no servidor. Tente novamente em instantes.';
    return data?.mensagem || MSG_SERVIDOR;
}

export default function Login() {
    const { login, isAuthenticated } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();
    const destino = location.state?.from?.pathname || '/';

    const [email, setEmail] = useState('');
    const [senha, setSenha] = useState('');
    const [mostrarSenha, setMostrarSenha] = useState(false);
    const [manterConectado, setManterConectado] = useState(false);
    const [erro, setErro] = useState('');
    const [enviando, setEnviando] = useState(false);

    // Já logado: não faz sentido ver o login
    if (isAuthenticated) return <Navigate to={destino} replace />;

    async function aoEnviar(e) {
        e.preventDefault();
        if (enviando) return;

        if (!email.trim() || !senha) {
            setErro('Informe o e-mail e a senha.');
            return;
        }

        setErro('');
        setEnviando(true);
        try {
            await login(email.trim(), senha, manterConectado);
            navigate(destino, { replace: true });
        } catch (err) {
            setErro(mensagemDeErro(err));
        } finally {
            setEnviando(false);
        }
    }

    return (
        <div className="login">
            <a className="login__skip" href="#conteudo">
                Pular para o conteúdo
            </a>

            <div className="login__marca">
                <img
                    className="login__logo"
                    src="/logo-brassario.png"
                    alt="Logotipo do Brassário: uma flor de lúpulo"
                />
                <div className="login__nome">Brassário</div>
                <p className="login__slogan">Do armário à brassagem</p>
            </div>

            <main id="conteudo" className="login__lado-form">
                <form className="login__form" onSubmit={aoEnviar} noValidate aria-labelledby="titulo-login">
                    <h1 id="titulo-login" className="login__titulo">Entrar</h1>
                    <p className="login__dica">
                        Use o e-mail e a senha que o administrador criou para você.
                    </p>

                    <div className="login__campo">
                        <label className="login__label" htmlFor="email">E-mail</label>
                        <input
                            id="email"
                            className={`login__input${erro ? ' login__input--erro' : ''}`}
                            type="email"
                            autoComplete="username"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                            aria-invalid={Boolean(erro)}
                        />
                    </div>

                    <div className="login__campo">
                        <label className="login__label" htmlFor="senha">Senha</label>
                        <div className="login__linha-senha">
                            <input
                                id="senha"
                                className={`login__input${erro ? ' login__input--erro' : ''}`}
                                type={mostrarSenha ? 'text' : 'password'}
                                autoComplete="current-password"
                                value={senha}
                                onChange={(e) => setSenha(e.target.value)}
                                aria-invalid={Boolean(erro)}
                            />
                            <button
                                type="button"
                                className="login__mostrar"
                                aria-pressed={mostrarSenha}
                                onClick={() => setMostrarSenha((v) => !v)}
                            >
                                {mostrarSenha ? 'Ocultar' : 'Mostrar'}
                            </button>
                        </div>
                    </div>

                    <label className="login__manter">
                        <input
                            type="checkbox"
                            checked={manterConectado}
                            onChange={(e) => setManterConectado(e.target.checked)}
                        />
                        Manter conectado neste aparelho
                    </label>

                    {erro && (
                        <p className="login__erro" role="alert">
                            {erro}
                        </p>
                    )}

                    <button className="login__entrar" type="submit" disabled={enviando}>
                        {enviando ? 'Entrando...' : 'Entrar'}
                    </button>

                    <p className="login__rodape">Sem acesso? Fale com o administrador.</p>
                </form>
            </main>
        </div>
    );
}