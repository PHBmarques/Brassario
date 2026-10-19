import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from './AuthContext.jsx';

/**
 * Protege uma rota.
 * - Sem token: redireciona para /login.
 * - Com `roles`: só deixa passar quem tem um dos perfis (ex.: ['ADMIN']).
 *   Um OPERADOR que tentar abrir uma tela de ADMIN volta para o início.
 */
export default function ProtectedRoute({ roles, children }) {
  const { isAuthenticated, usuario } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  if (roles && !roles.includes(usuario?.perfil)) {
    return <Navigate to="/" replace />;
  }

  return children;
}