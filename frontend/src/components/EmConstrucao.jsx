import { Link } from 'react-router-dom'
import { rotas } from '../rotas'

// Página provisória: mostra o nome da tela e links para as outras (só para testar a navegação).
export default function EmConstrucao({ titulo }) {
    return (
        <main id="conteudo" style={{ padding: '24px', maxWidth: 720 }}>
            <h1>{titulo}</h1>
            <p>Tela em construção.</p>
            <nav aria-label="Atalhos de desenvolvimento" style={{ marginTop: 24 }}>
                <h2 style={{ fontSize: 16 }}>Telas</h2>
                <ul style={{ lineHeight: 2 }}>
                    {rotas
                        .filter((r) => !r.path.includes(':'))
                        .map((r) => (
                            <li key={r.path}>
                                <Link to={r.path}>{r.titulo}</Link>
                            </li>
                        ))}
                </ul>
            </nav>
        </main>
    )
}