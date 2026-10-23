import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { obterDetalheArmario } from '../Api/armario.js'
import { mensagemDeErro } from '../Api/erros.js'
import Etiqueta from '../components/Etiqueta.jsx'
import {
    formatarData,
    formatarNumero,
    formatarQuantidade,
    hojeIso,
    rotuloCategoria,
    rotuloUnidade,
    tipoPorCor,
} from '../Utils/armario.js'
import { notaFiscalDoLote } from '../Utils/notasFiscais.js'
import './DetalheInsumo.css'

const textoDias = (dias) => (dias === 1 ? '1 dia' : `${dias} dias`)

function situacaoDoLote(lote) {
    if (lote.vencido) return { tipo: 'critica', texto: 'Vencido' }
    return { tipo: tipoPorCor(lote.situacaoValidade), texto: `Vence em ${textoDias(lote.diasParaVencer)}` }
}

function observacaoDoLote(lote, hoje) {
    const partes = []
    if (lote.ordemFefo === 1) partes.push('Usado primeiro')
    if (lote.dataEntrada === hoje) partes.push('Entrou hoje')
    const nf = notaFiscalDoLote(lote.id)
    if (nf) partes.push(`NF ${nf}`)
    if (lote.vencido) partes.push('Fora da produção')
    else if (lote.quantidadeAtual <= 0) partes.push('Esgotado')
    return partes.join(', ')
}

export default function DetalheInsumo() {
    const { id } = useParams()
    const [detalhe, setDetalhe] = useState(null)
    const [erro, setErro] = useState('')

    const carregar = useCallback(async () => {
        setErro('')
        try {
            setDetalhe(await obterDetalheArmario(id))
        } catch (e) {
            setErro(mensagemDeErro(e))
        }
    }, [id])

    useEffect(() => {
        carregar()
    }, [carregar])

    const voltar = (
        <Link to="/armario" className="detalhe__voltar">
            ‹ Voltar ao Armário
        </Link>
    )

    if (erro) {
        return (
            <section className="detalhe">
                {voltar}
                <p className="detalhe__erro" role="alert">
                    {erro}
                </p>
                <button type="button" className="botao-secundario" onClick={carregar}>
                    Tentar de novo
                </button>
            </section>
        )
    }

    if (!detalhe) {
        return (
            <section className="detalhe">
                {voltar}
                <p role="status">Carregando…</p>
            </section>
        )
    }

    const { resumo, lotes } = detalhe
    const unidade = resumo.unidadeMedida
    const hoje = hojeIso()
    const corProximo = resumo.cor ? tipoPorCor(resumo.cor) : 'vazio'

    return (
        <section className="detalhe">
            {voltar}
            <h1 className="titulo-pagina">{resumo.nome}</h1>
            <p className="subtitulo-pagina">
                {rotuloCategoria(resumo.categoria)}, medido em {rotuloUnidade(unidade)}. Estoque mínimo:{' '}
                {formatarQuantidade(resumo.estoqueMinimo, unidade)}.
            </p>

            <div className="detalhe__indicadores">
                <div className="indicador">
                    <span>Saldo total</span>
                    <strong className="indicador__valor">{formatarQuantidade(resumo.saldo, unidade)}</strong>
                </div>
                <div className="indicador">
                    <span>Lotes em estoque</span>
                    <strong className="indicador__valor indicador__valor--neutro">
                        {formatarNumero(resumo.quantidadeLotes)}
                    </strong>
                </div>
                <div className="indicador">
                    <span>Próximo a vencer</span>
                    <strong className={`indicador__valor indicador__valor--${corProximo}`}>
                        {resumo.diasParaVencer == null ? '—' : textoDias(resumo.diasParaVencer)}
                    </strong>
                </div>
            </div>

            <div className="detalhe__lotes">
                <h2>Lotes, na ordem de uso</h2>
                <p className="detalhe__dica">Na produção, o que vence primeiro sai primeiro.</p>

                {lotes.length === 0 ? (
                    <p className="detalhe__vazio">Nenhum lote com saldo no Armário.</p>
                ) : (
                    <div className="detalhe__tabela-rolagem">
                        <table className="detalhe__tabela">
                            <thead>
                                <tr>
                                    <th scope="col">Ordem</th>
                                    <th scope="col">Lote</th>
                                    <th scope="col">Fornecedor</th>
                                    <th scope="col">Validade</th>
                                    <th scope="col">Saldo</th>
                                    <th scope="col">Situação</th>
                                    <th scope="col">Observação</th>
                                </tr>
                            </thead>
                            <tbody>
                                {lotes.map((lote) => {
                                    const situacao = situacaoDoLote(lote)
                                    return (
                                        <tr key={lote.id}>
                                            <td>{lote.ordemFefo ?? '—'}</td>
                                            <td className="detalhe__codigo">{lote.codigoLote}</td>
                                            <td>{lote.fornecedorNome ?? '—'}</td>
                                            <td>{formatarData(lote.dataValidade)}</td>
                                            <td>{formatarQuantidade(lote.quantidadeAtual, unidade)}</td>
                                            <td>
                                                <Etiqueta tipo={situacao.tipo}>{situacao.texto}</Etiqueta>
                                            </td>
                                            <td className="detalhe__observacao">{observacaoDoLote(lote, hoje)}</td>
                                        </tr>
                                    )
                                })}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>
        </section>
    )
}
