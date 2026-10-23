import { Link, useLocation } from 'react-router-dom'
import { useAuth } from '../Auth/AuthContext.jsx'
import EmailRecuperacao from '../components/EmailRecuperacao.jsx'
import { iniciais } from '../Utils/usuario.js'
import './Conta.css'

const rotuloPerfil = (perfil) => (perfil === 'ADMIN' ? 'Administrador' : 'Operador')

function formatarData(iso) {
    if (!iso) return '—'
    return new Date(iso).toLocaleDateString('pt-BR')
}

export default function MinhaConta() {
    const { usuario } = useAuth()
    const { state } = useLocation()

    return (
        <section className="conta">
            <h1 className="titulo-pagina">Minha conta</h1>
            <p className="subtitulo-pagina">Seus dados de acesso ao Brassário.</p>

            {state?.aviso && (
                <p className="conta__aviso" role="status">
                    {state.aviso}
                </p>
            )}

            <div className="conta__cartao">
                <div className="conta__identidade">
                    <span className="conta__avatar" aria-hidden="true">
                        {iniciais(usuario?.nome)}
                    </span>
                    <div>
                        <h2 className="conta__nome">{usuario?.nome}</h2>
                        <p className="conta__perfil">{rotuloPerfil(usuario?.perfil)}</p>
                    </div>
                </div>

                <dl className="conta__dados">
                    <div>
                        <dt>E-mail de login</dt>
                        <dd>{usuario?.email}</dd>
                    </div>
                    <div>
                        <dt>Perfil</dt>
                        <dd>{rotuloPerfil(usuario?.perfil)}</dd>
                    </div>
                    <div>
                        <dt>Conta criada em</dt>
                        <dd>{formatarData(usuario?.criadoEm)}</dd>
                    </div>
                </dl>
                <p className="form-campo__dica">Para mudar o nome ou o e-mail de login, peça ao administrador.</p>
            </div>

            <div className="conta__cartao">
                <h2 className="conta__secao">Recuperação de senha</h2>
                <EmailRecuperacao />
            </div>

            <div className="conta__cartao">
                <h2 className="conta__secao">Segurança</h2>
                <p className="form-campo__dica">Troque a senha sempre que achar necessário.</p>
                <div>
                    <Link to="/alterar-senha" className="botao-primario">
                        Alterar minha senha
                    </Link>
                </div>
            </div>
        </section>
    )
}
