import Logo from './Logo.jsx'
import ThemeToggle from './ThemeToggle.jsx'
import '../Pages/Login.css'

// Moldura das telas fora do menu (login, criar senha, esqueci a senha, links do e-mail):
// marca à esquerda e o conteúdo à direita. "larga" é para formulários maiores.
export default function TelaAcesso({ larga = false, children }) {
    return (
        <main className="login">
            <ThemeToggle className="login__toggle-tema" />

            <section className="login__marca">
                <Logo tamanho={120} />
                <h1 className="login__nome">Brassário</h1>
                <p className="login__slogan">Do armário à brassagem</p>
            </section>

            <section className="login__painel">
                <div className={`login__caixa${larga ? ' login__caixa--larga' : ''}`}>{children}</div>
            </section>
        </main>
    )
}
