import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { mensagemDeErro } from '../Api/erros.js'
import { disponibilidadeReceita, listarReceitas } from '../Api/receitas.js'
import { useAuth } from '../Auth/AuthContext.jsx'
import Etiqueta from '../components/Etiqueta.jsx'
import { formatarLitros, normalizarTexto } from '../Utils/formato.js'
import '../components/Formulario.css'
import './Receitas.css'

// Selo do cartão: dá para produzir o volume base agora?
function seloDisponibilidade(d) {
    if (d === undefined) return <Etiqueta tipo="atencao">Conferindo o Armário…</Etiqueta>
    if (d === null) return null
    if (d.podeProduzir) return <Etiqueta tipo="ok">Pode produzir</Etiqueta>
    const faltam = d.itens.filter((i) => !i.suficiente).length
    return (
        <Etiqueta tipo="critica">
            Falta {faltam} {faltam === 1 ? 'ingrediente' : 'ingredientes'}
        </Etiqueta>
    )
}

export default function Receitas() {
    const { usuario } = useAuth()
    const ehAdmin = usuario?.perfil === 'ADMIN'
    const [receitas, setReceitas] = useState(null)
    const [disponibilidade, setDisponibilidade] = useState({}) // id -> resposta | null (erro)
    const [busca, setBusca] = useState('')
    const [erro, setErro] = useState('')

    useEffect(() => {
        let ativo = true
        listarReceitas()
            .then((lista) => {
                if (!ativo) return
                setReceitas(lista)
                // Uma consulta por receita, no volume base (o Armário é pequeno; não trava a lista)
                lista.forEach((r) =>
                    disponibilidadeReceita(r.id, r.volumeBaseLitros)
                        .then((d) => ativo && setDisponibilidade((atual) => ({ ...atual, [r.id]: d })))
                        .catch(() => ativo && setDisponibilidade((atual) => ({ ...atual, [r.id]: null })))
                )
            })
            .catch((e) => ativo && setErro(mensagemDeErro(e)))
        return () => {
            ativo = false
        }
    }, [])

    const termo = normalizarTexto(busca)
    const visiveis = (receitas ?? []).filter(
        (r) => !termo || normalizarTexto(r.nome).includes(termo) || normalizarTexto(r.estilo).includes(termo)
    )

    return (
        <section className="receitas">
            <div className="receitas__topo">
                <div>
                    <h1 className="titulo-pagina">Receitas</h1>
                    <p className="subtitulo-pagina">Fichas de receita e o que dá para produzir agora.</p>
                </div>
                {ehAdmin && (
                    <Link to="/receitas/nova" className="botao-primario">
                        Nova receita
                    </Link>
                )}
            </div>

            <div className="form-campo receitas__busca">
                <label htmlFor="buscaReceita">Buscar receita</label>
                <input
                    id="buscaReceita"
                    type="search"
                    placeholder="Ex.: IPA"
                    value={busca}
                    onChange={(e) => setBusca(e.target.value)}
                />
            </div>

            {erro && (
                <p className="form-erro" role="alert">
                    {erro}
                </p>
            )}

            {receitas === null && !erro && (
                <p className="form-campo__dica" role="status">
                    Carregando receitas…
                </p>
            )}

            {receitas && visiveis.length === 0 && (
                <p className="form-campo__dica">
                    {receitas.length === 0 ? 'Nenhuma receita cadastrada ainda.' : 'Nenhuma receita encontrada.'}
                </p>
            )}

            <ul className="receitas__grade">
                {visiveis.map((r) => (
                    <li key={r.id} className="receitas__cartao">
                        <h2 className="receitas__nome">{r.nome}</h2>
                        <p className="receitas__detalhe">
                            {r.estilo ? `${r.estilo}, base` : 'Base'} de {formatarLitros(r.volumeBaseLitros)}
                        </p>
                        <span>{seloDisponibilidade(disponibilidade[r.id])}</span>
                        <Link to={`/receitas/${r.id}`} className="botao-secundario receitas__ver">
                            Ver ficha
                        </Link>
                    </li>
                ))}
            </ul>
        </section>
    )
}
