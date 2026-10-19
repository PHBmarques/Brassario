import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../Auth/AuthContext.jsx';
import './Login.css';

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const MSG_CREDENCIAIS = 'E-mail ou senha incorretos. Confira e tente de novo.';
const MSG_REDE = 'Não foi possível conectar ao servidor. Tente novamente.';

function validate({ email, senha }) {
  const errors = {};
  if (!email.trim()) {
    errors.email = 'Informe o e-mail.';
  } else if (!EMAIL_REGEX.test(email.trim())) {
    errors.email = 'Digite um e-mail válido.';
  }
  if (!senha) {
    errors.senha = 'Informe a senha.';
  }
  return errors;
}

export default function Login() {
  const { login, isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const destino = location.state?.from?.pathname ?? '/';

  const [form, setForm] = useState({ email: '', senha: '' });
  const [manterConectado, setManterConectado] = useState(false);
  const [mostrarSenha, setMostrarSenha] = useState(false);
  const [errors, setErrors] = useState({});
  const [submitError, setSubmitError] = useState('');
  const [loading, setLoading] = useState(false);

  if (isAuthenticated) {
    return <Navigate to={destino} replace />;
  }

  const handleChange = (event) => {
    const { name, value } = event.target;
    setForm((prev) => ({ ...prev, [name]: value }));
    if (errors[name]) setErrors((prev) => ({ ...prev, [name]: undefined }));
    if (submitError) setSubmitError('');
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    const found = validate(form);
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    setLoading(true);
    setSubmitError('');
    try {
      await login(form.email.trim(), form.senha, manterConectado);
      navigate(destino, { replace: true });
    } catch (error) {
      // 401 → mostra a mensagem ao usuário
      setSubmitError(error.response?.status === 401 ? MSG_CREDENCIAIS : MSG_REDE);
      setLoading(false);
    }
  };

  return (
    <main className="login">
      <header className="login__hero">
        <div className="login__inner">
          <h1 className="login__brand">Brassário</h1>
          <p className="login__tagline v-phone-desktop">Do armário à brassagem</p>
          <p className="login__tagline v-tablet">
            Controle de insumos e lotes para cervejarias artesanais.
          </p>
        </div>
      </header>

      <section className="login__panel">
        <div className="login__card">
          <h2 className="login__title v-tablet-desktop">Entrar</h2>
          <p className="login__subtitle v-desktop">
            Use o e-mail e a senha que o administrador criou para você.
          </p>

          <form onSubmit={handleSubmit} noValidate>
            <div className="field">
              <label htmlFor="email">E-mail</label>
              <input
                id="email"
                name="email"
                type="email"
                inputMode="email"
                autoComplete="username"
                value={form.email}
                onChange={handleChange}
                aria-invalid={Boolean(errors.email)}
                aria-describedby={errors.email ? 'email-erro' : undefined}
                disabled={loading}
              />
              {errors.email && (
                <p id="email-erro" className="field__error">
                  {errors.email}
                </p>
              )}
            </div>

            <div className="field">
              <label htmlFor="senha">Senha</label>
              <div className="password-row">
                <input
                  id="senha"
                  name="senha"
                  type={mostrarSenha ? 'text' : 'password'}
                  autoComplete="current-password"
                  value={form.senha}
                  onChange={handleChange}
                  aria-invalid={Boolean(errors.senha)}
                  aria-describedby={errors.senha ? 'senha-erro' : undefined}
                  disabled={loading}
                />
                <button
                  type="button"
                  className="login__toggle v-desktop"
                  onClick={() => setMostrarSenha((v) => !v)}
                  aria-pressed={mostrarSenha}
                >
                  {mostrarSenha ? 'Ocultar' : 'Mostrar'}
                </button>
              </div>
              {errors.senha && (
                <p id="senha-erro" className="field__error">
                  {errors.senha}
                </p>
              )}
            </div>

            <label className="login__remember v-tablet-desktop">
              <input
                type="checkbox"
                checked={manterConectado}
                onChange={(e) => setManterConectado(e.target.checked)}
                disabled={loading}
              />
              <span className="v-tablet">Mantenha-me conectado neste tablet</span>
              <span className="v-desktop">Manter conectado neste aparelho</span>
            </label>

            {submitError && (
              <p className="login__alert" role="alert">
                {submitError}
              </p>
            )}

            <button type="submit" className="login__button" disabled={loading}>
              {loading ? 'Entrando…' : 'Entrar'}
            </button>
          </form>
        </div>

        <p className="login__help v-phone-desktop">Sem acesso? Fale com o administrador.</p>
      </section>
    </main>
  );
}