import { Link } from 'react-router-dom';
import { useAuth } from '../Auth/AuthContext.jsx';

// Placeholder: o Início real será a próxima tela do esboço.
export default function Home() {
  const { usuario, logout } = useAuth();

  return (
    <main style={{ padding: 24 }}>
      <h1>Início</h1>
      <p>Bom dia, {usuario?.nome ?? 'usuário'}.</p>
      {usuario?.perfil === 'ADMIN' && (
        <p>
          <Link to="/usuarios">Usuários</Link>
        </p>
      )}
      <button type="button" onClick={logout}>
        Sair
      </button>
    </main>
  );
}