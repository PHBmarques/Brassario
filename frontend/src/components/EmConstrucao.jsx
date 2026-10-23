import { Link } from 'react-router-dom'
import { rotas } from '../rotas'

// Página provisória: mostra o nome da tela e links para as outras (só para testar a navegação).
// Ela aparece DENTRO do menu lateral (AppLayout), que já tem o <main id="conteudo">.
export default function EmConstrucao({ titulo }) {
    return (
        <section style={{ maxWidth: 720 }}>
            <h1 className="titulo-pagina">{titulo}</h1>
            <p className="subtitulo-pagina">Tela em construção.</p>
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
        </section>
    )
}
