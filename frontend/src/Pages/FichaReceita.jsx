import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { mensagemDeErro } from '../Api/erros.js'
import { disponibilidadeReceita, obterReceita } from '../Api/receitas.js'
import { useAuth } from '../Auth/AuthContext.jsx'
import Etiqueta from '../components/Etiqueta.jsx'
import { formatarQuantidade, lerNumero } from '../Utils/armario.js'
import { formatarLitros } from '../Utils/formato.js'
import '../components/Formulario.css'
import './Receitas.css'

export default function FichaReceita() {
    const { id } = useParams()
    const { usuario } = useAuth()
    const ehAdmin = usuario?.perfil === 'ADMIN'

    const [receita, setReceita] = useState(null)
    const [erro, setErro] = useState('')
    const [volume, setVolume] = useState('')
    const [disp, setDisp] = useState(null)
    const [erroDisp, setErroDisp] = useState('')

    useEffect(() => {
        let ativo = true
        obterReceita(id)
            .then((r) => {
                if (!ativo) return
                setReceita(r)
                setVolume(String(r.volumeBaseLitros).replace('.', ','))
            })
            .catch((e) => ativo && setErro(mensagemDeErro(e)))
        return () => {
            ativo = false
        }
    }, [id])

    const litros = lerNumero(volume)
    const volumeValido = Number.isFinite(litros) && litros > 0

    // Recalcula a disponibilidade quando o volume muda (espera a pessoa parar de digitar)
    useEffect(() => {
        if (!receita || !volumeValido) return undefined
        let ativo = true
        const espera = setTimeout(() => {
            disponibilidadeReceita(receita.id, litros)
                .then((d) => {
                    if (!ativo) return
                    setDisp(d)
                    setErroDisp('')
                })
                .catch((e) => ativo && setErroDisp(mensagemDeErro(e)))
        }, 350)
        return () => {
            ativo = false
            clearTimeout(espera)
        }
    }, [receita, litros, volumeValido])

    if (erro) {
        return (
            <section className="ficha">
                <h1 className="titulo-pagina">Ficha de Receita</h1>
                <p className="form-erro" role="alert">
                    {erro}
                </p>
                <Link to="/receitas" className="botao-secundario">
                    Voltar para Receitas
                </Link>
            </section>
        )
    }

    if (!receita) {
        return (
            <p className="form-campo__dica" role="status">
                Carregando ficha…
            </p>
        )
    }

    const faltando = disp?.itens.filter((i) => !i.suficiente) ?? []

    return (
        <section className="ficha">
            <h1 className="titulo-pagina">Ficha de Receita: {receita.nome}</h1>
            <p className="subtitulo-pagina">
                {receita.estilo ? `${receita.estilo}. ` : ''}Quantidades para o volume base de{' '}
                {formatarLitros(receita.volumeBaseLitros)}.
                {!receita.ativa && ' Receita inativa: não pode ser produzida.'}
            </p>

            <div className="ficha__grade">
                <div className="form-cartao">
                    <div className="ficha__cabecalho">
                        <h2 className="receitas__secao">Ingredientes no Armário</h2>
                        <div className="form-campo ficha__volume">
                            <label htmlFor="volume">Volume a produzir (L)</label>
                            <input
                                id="volume"
                                type="text"
                                inputMode="decimal"
                                value={volume}
                                onChange={(e) => setVolume(e.target.value)}
                                aria-invalid={!volumeValido}
                                aria-describedby={!volumeValido ? 'volume-erro' : undefined}
                            />
                        </div>
                    </div>
                    {!volumeValido && (
                        <span className="form-campo__erro" id="volume-erro">
                            Informe um volume maior que zero.
                        </span>
                    )}

                    <div className="ficha__rolagem">
                        <table className="tabela-dados">
                            <thead>
                                <tr>
                                    <th scope="col">Insumo</th>
                                    <th scope="col">Precisa</th>
                                    <th scope="col">No Armário</th>
                                    <th scope="col">Situação</th>
                                </tr>
                            </thead>
                            <tbody>
                                {(disp?.itens ?? receita.itens.map((i) => ({ ...i, insumo: i.insumoNome }))).map((i) => (
                                    <tr key={i.insumoId}>
                                        <th scope="row">{i.insumo}</th>
                                        <td>{formatarQuantidade(i.necessario ?? i.quantidade, i.unidadeMedida)}</td>
                                        <td>{disp ? formatarQuantidade(i.disponivel, i.unidadeMedida) : '…'}</td>
                                        <td>
                                            {disp &&
                                                (i.suficiente ? (
                                                    <Etiqueta tipo="ok">Disponível</Etiqueta>
                                                ) : (
                                                    <Etiqueta tipo="critica">
                                                        Falta {formatarQuantidade(i.falta, i.unidadeMedida)}
                                                    </Etiqueta>
                                                ))}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>

                    {erroDisp && (
                        <p className="form-erro" role="alert">
                            {erroDisp}
                        </p>
                    )}
                    {disp && volumeValido && (
                        <p className={`ficha__aviso${faltando.length ? ' ficha__aviso--falta' : ''}`} role="status">
                            {faltando.length === 0
                                ? 'Todos os ingredientes estão disponíveis.'
                                : `Faltam ${faltando.length} ${faltando.length === 1 ? 'ingrediente' : 'ingredientes'} para ${formatarLitros(litros)}: ${faltando.map((i) => i.insumo).join(', ')}.`}
                        </p>
                    )}

                    <div className="ficha__acoes">
                        {ehAdmin && (
                            <Link to={`/receitas/${receita.id}/editar`} className="botao-secundario">
                                Editar ficha
                            </Link>
                        )}
                        {receita.ativa && (
                            <Link
                                to={`/produzir?receita=${receita.id}${volumeValido ? `&volume=${litros}` : ''}`}
                                className="botao-primario"
                            >
                                Produzir
                            </Link>
                        )}
                    </div>
                </div>

                <aside className="form-cartao">
                    <h2 className="receitas__secao">Modo de preparo</h2>
                    <div className="ficha__preparo">{receita.modoPreparo?.trim() || 'Sem modo de preparo cadastrado.'}</div>
                </aside>
            </div>
        </section>
    )
}
