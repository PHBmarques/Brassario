import { Link } from 'react-router-dom'
import { useAuth } from '../Auth/AuthContext.jsx'
import Barra from '../components/Barra.jsx'
import Etiqueta from '../components/Etiqueta.jsx'
import { CONSUMO_30_DIAS, DURACAO_ESTOQUE, INDICADORES, PRECISA_ATENCAO } from '../Utils/mock.js'
import './Home.css'

function saudacao() {
    const hora = new Date().getHours()
    if (hora < 12) return 'Bom dia'
    if (hora < 18) return 'Boa tarde'
    return 'Boa noite'
}

export default function Home() {
    const { usuario } = useAuth()
    const primeiroNome = usuario?.nome?.split(' ')[0] ?? ''

    return (
        <section className="inicio">
            <h1 className="titulo-pagina">Início</h1>
            <p className="subtitulo-pagina">
                {saudacao()}{primeiroNome && `, ${primeiroNome}`}. Veja o que precisa de atenção hoje.
            </p>

            <div className="inicio__grade">
                {/* ---------- Coluna esquerda ---------- */}
                <div className="inicio__coluna">
                    <h2 className="inicio__secao">Precisa de atenção</h2>

                    <ul className="inicio__lista">
                        {PRECISA_ATENCAO.map((item) => (
                            <li key={item.id} className="cartao atencao">
                                <div>
                                    <strong>{item.nome}</strong>
                                    <span>{item.detalhe}</span>
                                </div>
                                <Etiqueta tipo={item.tipo}>{item.etiqueta}</Etiqueta>
                            </li>
                        ))}
                    </ul>

                    <div className="cartao cartao--cresce">
                        <h2 className="cartao__titulo">Consumo nos últimos 30 dias</h2>
                        <div className="grafico" role="img" aria-label="Consumo por categoria nos últimos 30 dias">
                            {CONSUMO_30_DIAS.map((coluna) => (
                                <div key={coluna.rotulo} className="grafico__item">
                                    <div className="grafico__area">
                                        <div
                                            className="grafico__coluna"
                                            style={{ height: `${coluna.nivel}%` }}
                                            title={`${coluna.valor} kg`}
                                        />
                                    </div>
                                    <span>{coluna.rotulo}</span>
                                </div>
                            ))}
                        </div>
                    </div>
                </div>

                {/* ---------- Coluna direita ---------- */}
                <div className="inicio__coluna">
                    <h2 className="inicio__secao">Resumo do mês</h2>

                    <div className="indicadores">
                        {INDICADORES.map((ind) => (
                            <div key={ind.id} className="cartao indicador">
                                <span>{ind.rotulo}</span>
                                <strong className={ind.destaque === 'perigo' ? 'indicador__valor--perigo' : ''}>
                                    {ind.valor}
                                </strong>
                            </div>
                        ))}
                    </div>

                    <div className="cartao cartao--cresce">
                        <h2 className="cartao__titulo">Quanto tempo o estoque dura</h2>
                        <ul className="duracao">
                            {DURACAO_ESTOQUE.map((item) => (
                                <li key={item.nome}>
                                    <div className="duracao__topo">
                                        <strong>{item.nome}</strong>
                                        <span>{item.dias} dias</span>
                                    </div>
                                    <Barra nivel={item.nivel} status={item.status} rotulo={`Duração de ${item.nome}`} />
                                </li>
                            ))}
                        </ul>
                        <Link to="/compras" className="botao-principal">Ver compras sugeridas</Link>
                    </div>
                </div>
            </div>
        </section>
    )
}
