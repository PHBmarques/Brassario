import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { listarComprasSugeridas } from '../Api/alertas.js'
import { mensagemDeErro } from '../Api/erros.js'
import Etiqueta from '../components/Etiqueta.jsx'
import { formatarQuantidade } from '../Utils/armario.js'
import '../components/Formulario.css'
import './Alertas.css'

// Uma semana ou menos é urgente (vermelho); até 15 dias, atenção (amarelo)
const DIAS_URGENTE = 7

const diasInteiros = (dias) => Math.floor(Number(dias))
const textoDias = (dias) => `${diasInteiros(dias)} ${diasInteiros(dias) === 1 ? 'dia' : 'dias'}`

function Situacao({ item }) {
    if (item.saldoUtilizavel <= 0) return <Etiqueta tipo="critica">Sem estoque</Etiqueta>
    if (item.diasRestantes == null) return <Etiqueta tipo="atencao">Abaixo do mínimo</Etiqueta>
    return (
        <Etiqueta tipo={item.diasRestantes <= DIAS_URGENTE ? 'critica' : 'atencao'}>{textoDias(item.diasRestantes)}</Etiqueta>
    )
}

export default function ComprasSugeridas() {
    const navigate = useNavigate()
    const [itens, setItens] = useState(null)
    const [erro, setErro] = useState('')

    useEffect(() => {
        let ativo = true
        listarComprasSugeridas()
            .then((lista) => ativo && setItens(lista))
            .catch((e) => ativo && setErro(mensagemDeErro(e)))
        return () => {
            ativo = false
        }
    }, [])

    const darEntrada = (item) =>
        navigate('/entrada', { state: { insumoId: item.insumoId, origem: 'pelas compras sugeridas' } })

    return (
        <section className="alertas">
            <button type="button" className="alertas__voltar" onClick={() => navigate(-1)}>
                ‹ Voltar
            </button>
            <h1 className="titulo-pagina">Compras sugeridas</h1>
            <p className="subtitulo-pagina">
                Insumos que acabam em 15 dias ou menos (pelo consumo dos últimos 30 dias) ou que estão abaixo do estoque mínimo. Os mais urgentes primeiro.
            </p>

            <div className="form-cartao">
                <h2 className="alertas__secao">O que repor</h2>

                {erro && (
                    <p className="form-erro" role="alert">
                        {erro}
                    </p>
                )}
                {itens === null && !erro && (
                    <p className="form-campo__dica" role="status">
                        Calculando…
                    </p>
                )}
                {itens?.length === 0 && (
                    <p className="form-campo__dica">Nada para repor agora: nenhum insumo está abaixo do mínimo nem acaba em 15 dias.</p>
                )}

                {itens?.length > 0 && (
                    <div className="alertas__rolagem">
                        <table className="tabela-dados">
                            <thead>
                                <tr>
                                    <th scope="col">Insumo</th>
                                    <th scope="col">Saldo</th>
                                    <th scope="col">Mínimo</th>
                                    <th scope="col">Consumo por dia</th>
                                    <th scope="col">Estoque dura</th>
                                    <th scope="col">Situação</th>
                                    <th scope="col">
                                        <span className="visualmente-oculto">Ação</span>
                                    </th>
                                </tr>
                            </thead>
                            <tbody>
                                {itens.map((item) => (
                                    <tr key={item.insumoId}>
                                        <th scope="row">{item.insumoNome}</th>
                                        <td>{formatarQuantidade(item.saldoUtilizavel, item.unidadeMedida)}</td>
                                        <td>{formatarQuantidade(item.estoqueMinimo, item.unidadeMedida)}</td>
                                        <td>{formatarQuantidade(Number(item.consumoMedioDiario.toFixed(3)), item.unidadeMedida)}</td>
                                        <td>{item.diasRestantes == null ? '—' : textoDias(item.diasRestantes)}</td>
                                        <td>
                                            <Situacao item={item} />
                                        </td>
                                        <td>
                                            <button
                                                type="button"
                                                className="botao-secundario alertas__botao"
                                                onClick={() => darEntrada(item)}
                                            >
                                                Dar entrada
                                            </button>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}

                <p className="alertas__explicacao">
                    <strong>Como calculamos:</strong> consumo por dia = saídas de produção dos últimos 30 dias ÷ 30. Dias
                    restantes = saldo ÷ consumo por dia. O Brassário não faz o pedido: quando a compra chegar, use Dar
                    entrada.
                </p>
            </div>
        </section>
    )
}
