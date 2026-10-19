import { Navigate, Route, Routes } from 'react-router-dom';
import ProtectedRoute from './Auth/ProtectedRoute.jsx';
import Login from './Pages/Login.jsx';
import Home from './Pages/Home.jsx';
import Usuarios from './Pages/Usuarios.jsx';

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />

      <Route
        path="/"
        element={
          <ProtectedRoute>
            <Home />
          </ProtectedRoute>
        }
      />

      {/* Tela exclusiva de ADMIN: fica oculta/bloqueada para OPERADOR */}
      <Route
        path="/usuarios"
        element={
          <ProtectedRoute roles={['ADMIN']}>
            <Usuarios />
          </ProtectedRoute>
        }
      />

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}