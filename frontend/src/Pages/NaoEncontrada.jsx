import { Link } from 'react-router-dom'

export default function NaoEncontrada() {
    return (
        <main id="conteudo" style={{ padding: '24px' }}>
            <h1>Página não encontrada</h1>
            <p>O endereço que você abriu não existe.</p>
            <p><Link to="/">Voltar ao início</Link></p>
        </main>
    )
}