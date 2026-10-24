import { useEffect, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { cancelarBatelada, finalizarBatelada, listarBateladas, obterBatelada } from '../Api/bateladas.js'
import { errosPorCampo, mensagemDeErro } from '../Api/erros.js'
import { listarReceitas } from '../Api/receitas.js'
import Etiqueta from '../components/Etiqueta.jsx'
import { formatarData, formatarQuantidade, hojeIso, lerNumero } from '../Utils/armario.js'
import { STATUS_BATELADA, formatarDataHora, formatarLitros, formatarReais } from '../Utils/formato.js'
import '../components/Formulario.css'
import './Producao.css'

const PERIODOS = [
    { valor: '7', rotulo: 'Últimos 7 dias' },
    { valor: '30', rotulo: 'Últimos 30 dias' },
    { valor: '90', rotulo: 'Últimos 90 dias' },
    { valor: '', rotulo: 'Todo o período' },
]

// Rendimento fora desta faixa merece uma conferida antes de finalizar
const RENDIMENTO_MINIMO = 70
const RENDIMENTO_MAXIMO = 130

function diasAtras(dias) {
    const d = new Date()
    d.setDate(d.getDate() - Number(dias))
    const mes = String(d.getMonth() + 1).padStart(2, '0')
    const dia = String(d.getDate()).padStart(2, '0')
    return `${d.getFullYear()}-${mes}-${dia}`
}

const formatarPercentual = (valor) => `${Number(valor).toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`

function SeloStatus({ status }) {
    const s = STATUS_BATELADA[status] ?? { rotulo: status, tipo: 'atencao' }
    return <Etiqueta tipo={s.tipo}>{s.rotulo}</Etiqueta>
}

function TabelaLotes({ consumos }) {
    if (!consumos?.length) return <p className="form-campo__dica">Nenhum lote consumido.</p>
    return (
        <div className="producao__rolagem">
            <table className="tabela-dados">
                <thead>
                    <tr>
                        <th scope="col">Insumo</th>
                        <th scope="col">Lote</th>
                        <th scope="col">Quantidade</th>
                        <th scope="col">Validade</th>
                    </tr>
                </thead>
                <tbody>
                    {consumos.map((c) => (
                        <tr key={`${c.loteId}-${c.insumoId}`}>
                            <th scope="row">{c.insumo}</th>
                            <td>{c.codigoLote}</td>
                            <td>{formatarQuantidade(c.quantidade, c.unidadeMedida)}</td>
                            <td>{formatarData(c.validade)}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    )
}

// ---------- Logo depois de confirmar a produção ----------
function ProducaoRegistrada({ producao }) {
    const navigate = useNavigate()
    return (
        <section className="producao">
            <h1 className="titulo-pagina">Produções</h1>
            <p className="producao__sucesso" role="status">
                Produção {producao.codigo} iniciada: {producao.receita}, {formatarLitros(producao.volumePlanejado)}. O
                estoque já foi abatido.
            </p>

            <div className="producao__duas">
                <div className="form-cartao">
                    <h2 className="producao__secao">Estoque depois da produção</h2>
                    <div className="producao__rolagem">
                        <table className="tabela-dados">
                            <thead>
                                <tr>
                                    <th scope="col">Insumo</th>
                                    <th scope="col">Antes</th>
                                    <th scope="col">Saiu</th>
                                    <th scope="col">Depois</th>
                                </tr>
                            </thead>
                            <tbody>
                                {(producao.insumos ?? []).map((i) => (
                                    <tr key={i.insumoId}>
                                        <th scope="row">{i.insumo}</th>
                                        <td>{i.saldoAntes != null ? formatarQuantidade(i.saldoAntes, i.unidadeMedida) : '—'}</td>
                                        <td>- {formatarQuantidade(i.quantidade, i.unidadeMedida)}</td>
                                        <td className="producao__forte">
                                            {i.saldoDepois != null ? formatarQuantidade(i.saldoDepois, i.unidadeMedida) : '—'}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                    <p className="form-campo__dica">Tudo foi abatido de uma vez. Se algo faltasse, nada seria abatido.</p>
                </div>

                <div className="form-cartao">
                    <div>
                        <h2 className="producao__secao">Lotes consumidos</h2>
                        <p className="form-campo__dica">
                            Da produção {producao.codigo}. Custo total: {formatarReais(producao.custoTotal)}.
                        </p>
                    </div>
                    <TabelaLotes consumos={producao.consumos} />
                </div>
            </div>

            <div className="producao__rodape">
                <button
                    type="button"
                    className="botao-secundario"
                    onClick={() => navigate('/bateladas', { replace: true, state: { selecionada: producao.id } })}
                >
                    Ver todas as produções
                </button>
                <Link to="/produzir" className="botao-primario">
                    Nova produção
                </Link>
            </div>
        </section>
    )
}

// ---------- Detalhe de uma produção do histórico ----------
function DetalheProducao({ detalhe, aoAtualizar }) {
    const [volumeFinal, setVolumeFinal] = useState(String(detalhe.volumePlanejado).replace('.', ','))
    const [cancelando, setCancelando] = useState(false)
    const [motivo, setMotivo] = useState('')
    const [erros, setErros] = useState({})
    const [erroGeral, setErroGeral] = useState('')
    const [enviando, setEnviando] = useState(false)

    const emAndamento = detalhe.status === 'EM_ANDAMENTO'
    const vf = lerNumero(volumeFinal)
    const vfValido = Number.isFinite(vf) && vf > 0
    const rendimento = vfValido ? (vf / detalhe.volumePlanejado) * 100 : null
    const custoLitro = vfValido ? detalhe.custoTotal / vf : null
    const foraDaFaixa = rendimento != null && (rendimento < RENDIMENTO_MINIMO || rendimento > RENDIMENTO_MAXIMO)

    async function finalizar() {
        if (!vfValido) return setErros({ volumeFinal: 'Informe um volume maior que zero.' })
        setEnviando(true)
        setErroGeral('')
        try {
            aoAtualizar(await finalizarBatelada(detalhe.id, vf))
        } catch (err) {
            setErros(errosPorCampo(err))
            setErroGeral(mensagemDeErro(err))
        } finally {
            setEnviando(false)
        }
    }

    async function cancelar() {
        const texto = motivo.trim()
        if (texto.length < 5) return setErros({ motivo: 'Explique o motivo (mínimo de 5 caracteres).' })
        setEnviando(true)
        setErroGeral('')
        try {
            aoAtualizar(await cancelarBatelada(detalhe.id, texto))
        } catch (err) {
            setErros(errosPorCampo(err))
            setErroGeral(mensagemDeErro(err))
        } finally {
            setEnviando(false)
        }
    }

    const descricao = `${detalhe.receita}, ${formatarLitros(detalhe.volumePlanejado)}, ${STATUS_BATELADA[detalhe.status]?.rotulo.toLowerCase() ?? detalhe.status}`

    return (
        <div className="producao__duas">
            <div className="form-cartao">
                <div>
                    <h2 className="producao__secao">{detalhe.codigo}</h2>
                    <p className="form-campo__dica">{descricao}</p>
                    {detalhe.observacoes && <p className="form-campo__dica">Observações: {detalhe.observacoes}</p>}
                </div>

                {erroGeral && (
                    <p className="form-erro" role="alert">
                        {erroGeral}
                    </p>
                )}

                {emAndamento && !cancelando && (
                    <>
                        <div className="form-campo">
                            <label htmlFor="volumeFinal">Volume final obtido (L) *</label>
                            <input
                                id="volumeFinal"
                                type="text"
                                inputMode="decimal"
                                value={volumeFinal}
                                onChange={(e) => {
                                    setVolumeFinal(e.target.value)
                                    setErros({})
                                }}
                                disabled={enviando}
                                aria-invalid={Boolean(erros.volumeFinal)}
                                aria-describedby="volumeFinal-dica"
                            />
                            {erros.volumeFinal ? (
                                <span className="form-campo__erro" id="volumeFinal-dica">
                                    {erros.volumeFinal}
                                </span>
                            ) : (
                                rendimento != null && (
                                    <span className="form-campo__dica" id="volumeFinal-dica">
                                        Rendimento de{' '}
                                        <strong className={foraDaFaixa ? 'producao__rendimento--alerta' : 'producao__rendimento'}>
                                            {formatarPercentual(rendimento)}
                                        </strong>{' '}
                                        sobre o planejado. Custo por litro: {formatarReais(custoLitro)}.
                                    </span>
                                )
                            )}
                        </div>

                        {foraDaFaixa && (
                            <p className="producao__alerta" role="status">
                                Atenção: rendimento {rendimento < RENDIMENTO_MINIMO ? `abaixo de ${RENDIMENTO_MINIMO}%` : `acima de ${RENDIMENTO_MAXIMO}%`}.
                                Confira o volume antes de finalizar.
                            </p>
                        )}

                        <div className="producao__botoes">
                            <button
                                type="button"
                                className="botao-perigo"
                                onClick={() => {
                                    setCancelando(true)
                                    setErros({})
                                }}
                                disabled={enviando}
                            >
                                Cancelar produção
                            </button>
                            <button type="button" className="botao-primario" onClick={finalizar} disabled={enviando}>
                                {enviando ? 'Finalizando…' : 'Finalizar produção'}
                            </button>
                        </div>
                        <p className="form-campo__dica">Cancelar devolve os insumos ao estoque.</p>
                    </>
                )}

                {emAndamento && cancelando && (
                    <>
                        <div className="form-campo">
                            <label htmlFor="motivo">Motivo do cancelamento *</label>
                            <input
                                id="motivo"
                                type="text"
                                maxLength={255}
                                placeholder="Ex.: Mosto contaminado"
                                value={motivo}
                                onChange={(e) => {
                                    setMotivo(e.target.value)
                                    setErros({})
                                }}
                                disabled={enviando}
                                aria-invalid={Boolean(erros.motivo)}
                                aria-describedby="motivo-dica"
                                autoFocus
                            />
                            <span className={erros.motivo ? 'form-campo__erro' : 'form-campo__dica'} id="motivo-dica">
                                {erros.motivo ?? 'Os insumos voltam para os lotes de origem.'}
                            </span>
                        </div>
                        <div className="producao__botoes">
                            <button type="button" className="botao-secundario" onClick={() => setCancelando(false)} disabled={enviando}>
                                Voltar
                            </button>
                            <button type="button" className="botao-perigo botao-perigo--cheio" onClick={cancelar} disabled={enviando}>
                                {enviando ? 'Cancelando…' : 'Confirmar cancelamento'}
                            </button>
                        </div>
                    </>
                )}

                {detalhe.status === 'FINALIZADA' && (
                    <dl className="producao__dados">
                        <div>
                            <dt>Volume final</dt>
                            <dd>{formatarLitros(detalhe.volumeFinal)}</dd>
                        </div>
                        <div>
                            <dt>Rendimento</dt>
                            <dd>{detalhe.rendimentoPercentual != null ? formatarPercentual(detalhe.rendimentoPercentual) : '—'}</dd>
                        </div>
                        <div>
                            <dt>Custo por litro</dt>
                            <dd>{detalhe.custoPorLitro != null ? formatarReais(detalhe.custoPorLitro) : '—'}</dd>
                        </div>
                    </dl>
                )}

                {detalhe.status === 'CANCELADA' && (
                    <dl className="producao__dados">
                        <div>
                            <dt>Cancelada em</dt>
                            <dd>{formatarDataHora(detalhe.canceladoEm)}</dd>
                        </div>
                        <div>
                            <dt>Motivo</dt>
                            <dd>{detalhe.motivoCancelamento ?? '—'}</dd>
                        </div>
                        <div>
                            <dt>Insumos</dt>
                            <dd>Devolvidos aos lotes de origem.</dd>
                        </div>
                    </dl>
                )}
            </div>

            <div className="form-cartao">
                <div>
                    <h2 className="producao__secao">Lotes consumidos</h2>
                    <p className="form-campo__dica">
                        Da produção {detalhe.codigo}. Custo total: {formatarReais(detalhe.custoTotal)}.
                    </p>
                </div>
                <TabelaLotes consumos={detalhe.consumos} />
            </div>
        </div>
    )
}

// ---------- Histórico ----------
function Historico({ selecionadaInicial }) {
    const [filtros, setFiltros] = useState({ status: '', periodo: '30', receitaId: '' })
    const [receitas, setReceitas] = useState([])
    const [pagina, setPagina] = useState(null)
    const [erro, setErro] = useState('')
    const [selecionada, setSelecionada] = useState(selecionadaInicial ?? null)
    const [detalhe, setDetalhe] = useState(null)
    const [erroDetalhe, setErroDetalhe] = useState('')

    useEffect(() => {
        listarReceitas()
            .then(setReceitas)
            .catch(() => {})
    }, [])

    useEffect(() => {
        let ativo = true
        listarBateladas({
            status: filtros.status,
            receitaId: filtros.receitaId,
            de: filtros.periodo ? diasAtras(filtros.periodo) : '',
            ate: filtros.periodo ? hojeIso() : '',
            tamanho: 50,
        })
            .then((p) => {
                if (!ativo) return
                setPagina(p)
                setErro('')
            })
            .catch((e) => ativo && setErro(mensagemDeErro(e)))
        return () => {
            ativo = false
        }
    }, [filtros])

    useEffect(() => {
        if (selecionada == null) return undefined
        let ativo = true
        obterBatelada(selecionada)
            .then((d) => {
                if (!ativo) return
                setDetalhe(d)
                setErroDetalhe('')
            })
            .catch((e) => ativo && setErroDetalhe(mensagemDeErro(e)))
        return () => {
            ativo = false
        }
    }, [selecionada])

    const filtro = (campo) => (e) => setFiltros((f) => ({ ...f, [campo]: e.target.value }))

    // Finalizar/cancelar devolve a produção atualizada: troca no detalhe e na linha da tabela
    function atualizar(nova) {
        setDetalhe(nova)
        setPagina((p) =>
            p && {
                ...p,
                itens: p.itens.map((i) =>
                    i.id === nova.id
                        ? { ...i, status: nova.status, custoTotal: nova.custoTotal, quantidade: nova.volumeFinal ?? i.quantidade }
                        : i
                ),
            }
        )
    }

    const abrir = (id) => setSelecionada(id)

    return (
        <section className="producao">
            <div className="producao__topo">
                <div>
                    <h1 className="titulo-pagina">Produções</h1>
                    <p className="subtitulo-pagina">
                        Histórico de produção. Escolha uma linha para ver os detalhes e os lotes usados.
                    </p>
                </div>
                <Link to="/produzir" className="botao-primario">
                    Nova produção
                </Link>
            </div>

            <div className="producao__filtros">
                <div className="form-campo">
                    <label htmlFor="filtroStatus">Situação</label>
                    <select id="filtroStatus" value={filtros.status} onChange={filtro('status')}>
                        <option value="">Todas</option>
                        {Object.entries(STATUS_BATELADA).map(([valor, s]) => (
                            <option key={valor} value={valor}>
                                {s.rotulo}
                            </option>
                        ))}
                    </select>
                </div>
                <div className="form-campo">
                    <label htmlFor="filtroPeriodo">Período</label>
                    <select id="filtroPeriodo" value={filtros.periodo} onChange={filtro('periodo')}>
                        {PERIODOS.map((p) => (
                            <option key={p.valor} value={p.valor}>
                                {p.rotulo}
                            </option>
                        ))}
                    </select>
                </div>
                <div className="form-campo">
                    <label htmlFor="filtroReceita">Receita</label>
                    <select id="filtroReceita" value={filtros.receitaId} onChange={filtro('receitaId')}>
                        <option value="">Todas</option>
                        {receitas.map((r) => (
                            <option key={r.id} value={String(r.id)}>
                                {r.nome}
                            </option>
                        ))}
                    </select>
                </div>
            </div>

            <div className="form-cartao">
                <h2 className="producao__secao">Histórico</h2>
                {erro && (
                    <p className="form-erro" role="alert">
                        {erro}
                    </p>
                )}
                {!pagina && !erro && (
                    <p className="form-campo__dica" role="status">
                        Carregando produções…
                    </p>
                )}
                {pagina && pagina.itens.length === 0 && (
                    <p className="form-campo__dica">Nenhuma produção encontrada com esses filtros.</p>
                )}
                {pagina && pagina.itens.length > 0 && (
                    <div className="producao__rolagem">
                        <table className="tabela-dados">
                            <thead>
                                <tr>
                                    <th scope="col">Código</th>
                                    <th scope="col">Receita</th>
                                    <th scope="col">Volume</th>
                                    <th scope="col">Situação</th>
                                    <th scope="col">Custo</th>
                                    <th scope="col">Data</th>
                                </tr>
                            </thead>
                            <tbody>
                                {pagina.itens.map((b) => (
                                    <tr
                                        key={b.id}
                                        className="tabela-dados__clicavel"
                                        aria-selected={b.id === selecionada}
                                        onClick={() => abrir(b.id)}
                                    >
                                        <th scope="row">
                                            {/* Botão para abrir pelo teclado */}
                                            <button type="button" className="producao__codigo" onClick={() => abrir(b.id)}>
                                                {b.codigo}
                                            </button>
                                        </th>
                                        <td>{b.receita}</td>
                                        <td>{formatarLitros(b.quantidade)}</td>
                                        <td>
                                            <SeloStatus status={b.status} />
                                        </td>
                                        <td>{formatarReais(b.custoTotal)}</td>
                                        <td>{formatarDataHora(b.dataProducao, true)}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
                {pagina && pagina.total > pagina.itens.length && (
                    <p className="form-campo__dica">
                        Mostrando as {pagina.itens.length} mais recentes de {pagina.total}. Use os filtros para refinar.
                    </p>
                )}
            </div>

            {erroDetalhe && (
                <p className="form-erro" role="alert">
                    {erroDetalhe}
                </p>
            )}
            {detalhe && detalhe.id === selecionada && (
                <DetalheProducao key={`${detalhe.id}-${detalhe.status}`} detalhe={detalhe} aoAtualizar={atualizar} />
            )}
        </section>
    )
}

// /bateladas (histórico) e /bateladas/:id (histórico com a produção já aberta)
export default function Producoes() {
    const { state } = useLocation()
    const { id } = useParams()
    if (state?.producao) return <ProducaoRegistrada producao={state.producao} />
    return <Historico key={id ?? 'todas'} selecionadaInicial={state?.selecionada ?? (id ? Number(id) : undefined)} />
}
