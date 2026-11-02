import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { listarAlertas } from '../Api/alertas.js'
import { listarBateladas, obterBatelada } from '../Api/bateladas.js'
import { consumoDashboard, previsaoDashboard, resumoDashboard } from '../Api/dashboard.js'
import { mensagemDeErro } from '../Api/erros.js'
import { useAuth } from '../Auth/AuthContext.jsx'
import Barra from '../components/Barra.jsx'
import Etiqueta from '../components/Etiqueta.jsx'
import { formatarNumero, hojeIso } from '../Utils/armario'
import { ROTULO_ALERTA, detalheDoAlerta, ehUrgente } from '../Utils/alertas.js'
import {
    consumoPorCategoria, custoMedioPorLitro, duracaoDoEstoque, inicioDoMes, insumosEmAlerta, litrosProduzidos,
    nomeDoMes, rendimentoMedio,
} from '../Utils/dashboard.js'
import { formatarReais } from '../Utils/formato.js'
import '../components/Formulario.css'
import './Home.css'

const MAX_ALERTAS_NA_TELA = 3
const MAX_DETALHES_PARA_RENDIMENTO = 20

function saudacao() {
    const hora = new Date().getHours()
    if (hora < 12) return 'Bom dia'
    if (hora < 18) return 'Boa tarde'
    return 'Boa noite'
}

// Produções do mês: quantas, litros, custo médio por litro e rendimento médio (das finalizadas)
async function carregarMes() {
    const ate = hojeIso()
    const de = inicioDoMes(ate)
    const [finalizadas, andamento] = await Promise.all([
        listarBateladas({ status: 'FINALIZADA', de, ate, tamanho: 100 }),
        listarBateladas({ status: 'EM_ANDAMENTO', de, ate, tamanho: 1 }),
    ])
    const detalhes = await Promise.all(
        finalizadas.itens.slice(0, MAX_DETALHES_PARA_RENDIMENTO).map((p) => obterBatelada(p.id).catch(() => null))
    )
    return {
        producoes: finalizadas.total + andamento.total,
        litros: litrosProduzidos(finalizadas.itens),
        custoPorLitro: custoMedioPorLitro(finalizadas.itens),
        rendimento: rendimentoMedio(detalhes),
    }
}

// Cada bloco da tela carrega sozinho: se um falhar, os outros continuam aparecendo
function useDados(carregar) {
    const [estado, setEstado] = useState({ dados: null, erro: '' })
    useEffect(() => {
        let ativo = true
        carregar()
            .then((dados) => ativo && setEstado({ dados, erro: '' }))
            .catch((e) => ativo && setEstado({ dados: null, erro: mensagemDeErro(e) }))
        return () => {
            ativo = false
        }
    }, [carregar])
    return estado
}

const Carregando = () => (
    <p className="form-campo__dica" role="status">Carregando…</p>
)
const Erro = ({ texto }) => (
    <p className="form-erro" role="alert">{texto}</p>
)

// Cartão de número. tom: '' | 'atencao' | 'perigo' (cor da faixa lateral e do valor)
function Indicador({ rotulo, valor, dica, tom = '' }) {
    return (
        <div className={`cartao indicador${tom ? ` indicador--${tom}` : ''}`}>
            <span>{rotulo}</span>
            <strong className={tom ? `indicador__valor--${tom}` : ''}>{valor}</strong>
            {dica && <small>{dica}</small>}
        </div>
    )
}

export default function Home() {
    const { usuario } = useAuth()
    const navigate = useNavigate()
    const primeiroNome = usuario?.nome?.split(' ')[0] ?? ''

    const alertas = useDados(listarAlertas)
    const resumo = useDados(resumoDashboard)
    const consumo = useDados(consumoDashboard)
    const previsao = useDados(previsaoDashboard)
    const mes = useDados(carregarMes)

    const urgentesPrimeiro = (alertas.dados ?? []).slice(0, MAX_ALERTAS_NA_TELA)
    const colunas = consumoPorCategoria(consumo.dados ?? [])
    const duracao = duracaoDoEstoque(previsao.dados ?? [])
    const emAlerta = alertas.dados ? insumosEmAlerta(alertas.dados) : null
    const r = resumo.dados
    const m = mes.dados

    const ou = (valor, pronto) => (pronto ? valor : '…')
    const tom = (n, nivel) => (n > 0 ? nivel : '')

    return (
        <section className="inicio">
            <div>
                <h1 className="titulo-pagina">Início</h1>
                <p className="subtitulo-pagina">
                    {saudacao()}{primeiroNome && `, ${primeiroNome}`}. Veja o que precisa de atenção hoje.
                </p>
            </div>

            {/* ---------- Estoque agora ---------- */}
            <div className="inicio__bloco">
                <h2 className="inicio__secao">Estoque agora</h2>
                {resumo.erro && <Erro texto={resumo.erro} />}
                <div className="indicadores indicadores--quatro">
                    <Indicador rotulo="Insumos ativos" valor={ou(r?.insumosAtivos, r)} dica="cadastrados no Armário" />
                    <Indicador
                        rotulo="Insumos em alerta"
                        valor={ou(emAlerta, emAlerta !== null)}
                        dica="validade ou estoque"
                        tom={tom(emAlerta, 'perigo')}
                    />
                    <Indicador
                        rotulo="Sem estoque"
                        valor={ou(r?.insumosSemEstoque, r)}
                        dica="saldo utilizável zerado"
                        tom={tom(r?.insumosSemEstoque, 'perigo')}
                    />
                    <Indicador
                        rotulo="Para comprar"
                        valor={ou(r?.insumosParaCompra, r)}
                        dica="abaixo do mínimo ou acabam em 15 dias"
                        tom={tom(r?.insumosParaCompra, 'atencao')}
                    />
                </div>
            </div>

            {/* ---------- Produção do mês ---------- */}
            <div className="inicio__bloco">
                <h2 className="inicio__secao">Produção de {nomeDoMes(hojeIso())}</h2>
                {mes.erro && <Erro texto={mes.erro} />}
                <div className="indicadores indicadores--quatro">
                    <Indicador rotulo="Produções no mês" valor={ou(m?.producoes, m)} dica="finalizadas e em andamento" />
                    <Indicador
                        rotulo="Litros produzidos"
                        valor={ou(m ? `${formatarNumero(m.litros)} L` : '', m)}
                        dica="das produções finalizadas"
                    />
                    <Indicador
                        rotulo="Custo médio por litro"
                        valor={ou(m?.custoPorLitro != null ? formatarReais(m.custoPorLitro) : '—', m)}
                        dica="custo total ÷ litros"
                    />
                    <Indicador
                        rotulo="Rendimento médio"
                        valor={ou(m?.rendimento != null ? `${Math.round(m.rendimento)}%` : '—', m)}
                        dica="volume final ÷ volume planejado"
                    />
                </div>
            </div>

            <div className="inicio__grade">
                {/* ---------- Coluna esquerda ---------- */}
                <div className="inicio__coluna">
                    <div className="inicio__bloco-topo">
                        <h2 className="inicio__secao">Precisa de atenção</h2>
                        {(alertas.dados?.length ?? 0) > 0 && <Link to="/alertas">Abrir alertas</Link>}
                    </div>

                    {alertas.erro && <Erro texto={alertas.erro} />}
                    {!alertas.dados && !alertas.erro && <Carregando />}
                    {alertas.dados?.length === 0 && (
                        <div className="cartao">
                            <p className="form-campo__dica">Nenhum alerta agora. Tudo certo no Armário.</p>
                        </div>
                    )}
                    {urgentesPrimeiro.length > 0 && (
                        <ul className="inicio__lista">
                            {urgentesPrimeiro.map((a) => (
                                <li key={`${a.tipo}-${a.insumoId}-${a.loteId ?? ''}`} className="cartao atencao">
                                    <div>
                                        <strong>{a.insumoNome}</strong>
                                        <span>{detalheDoAlerta(a)}</span>
                                    </div>
                                    <Etiqueta tipo={ehUrgente(a) ? 'critica' : 'atencao'}>
                                        {ROTULO_ALERTA[a.tipo] ?? a.tipo}
                                    </Etiqueta>
                                </li>
                            ))}
                        </ul>
                    )}
                    {(alertas.dados?.length ?? 0) > MAX_ALERTAS_NA_TELA && (
                        <button type="button" className="botao-secundario" onClick={() => navigate('/alertas')}>
                            Ver todos os {alertas.dados.length} alertas
                        </button>
                    )}

                    <div className="cartao cartao--cresce">
                        <h2 className="cartao__titulo">Consumo nos últimos 30 dias</h2>
                        {consumo.erro && <Erro texto={consumo.erro} />}
                        {!consumo.dados && !consumo.erro && <Carregando />}
                        {consumo.dados && colunas.length === 0 && (
                            <p className="form-campo__dica">Nenhuma produção nos últimos 30 dias.</p>
                        )}
                        {colunas.length > 0 && (
                            <div className="barras" role="img" aria-label="Consumo por categoria nos últimos 30 dias">
                                {colunas.map((c) => (
                                    <div key={c.rotulo} className="barras__linha">
                                        <span className="barras__rotulo">{c.rotulo}</span>
                                        <div className="barras__trilho">
                                            <div className="barras__preenchimento" style={{ width: `${c.nivel}%` }} />
                                        </div>
                                        <span className="barras__valor">{c.texto}</span>
                                    </div>
                                ))}
                            </div>
                        )}
                    </div>
                </div>

                {/* ---------- Coluna direita ---------- */}
                <div className="inicio__coluna">
                    <h2 className="inicio__secao">Previsão</h2>
                    <div className="cartao cartao--cresce">
                        <h2 className="cartao__titulo">Quanto tempo o estoque dura</h2>
                        {previsao.erro && <Erro texto={previsao.erro} />}
                        {!previsao.dados && !previsao.erro && <Carregando />}
                        {previsao.dados && duracao.length === 0 && (
                            <p className="form-campo__dica">Sem consumo nos últimos 30 dias para estimar a duração.</p>
                        )}
                        {duracao.length > 0 && (
                            <ul className="duracao">
                                {duracao.map((item) => (
                                    <li key={item.id}>
                                        <div className="duracao__topo">
                                            <strong>
                                                {item.nome} <small>{item.saldo} em estoque</small>
                                            </strong>
                                            <span>{item.dias} {item.dias === 1 ? 'dia' : 'dias'}</span>
                                        </div>
                                        <Barra nivel={item.nivel} status={item.status} rotulo={`Duração de ${item.nome}`} />
                                    </li>
                                ))}
                            </ul>
                        )}
                        <Link to="/compras" className="botao-principal">Ver compras sugeridas</Link>
                    </div>
                </div>
            </div>
        </section>
    )
}
