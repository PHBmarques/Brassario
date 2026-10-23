import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { listarArmario } from '../Api/armario.js'
import { mensagemDeErro } from '../Api/erros.js'
import { useAuth } from '../Auth/AuthContext.jsx'
import Barra from '../components/Barra.jsx'
import Etiqueta from '../components/Etiqueta.jsx'
import { formatarQuantidade, situacaoDoItem } from '../Utils/armario.js'
import './Armario.css'

const FILTROS = [
    { rotulo: 'Todos', categoria: null },
    { rotulo: 'Malte', categoria: 'MALTE' },
    { rotulo: 'Lúpulo', categoria: 'LUPULO' },
    { rotulo: 'Levedura', categoria: 'LEVEDURA' },
]

// "Lúpulo" e "lupulo" contam como iguais na busca
const normalizar = (texto) =>
    texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase()

export default function Armario() {
    const { usuario } = useAuth()
    const ehAdmin = usuario?.perfil === 'ADMIN'

    // Quem acabou de ser cadastrado ou recebeu entrada chega pelo state da navegação
    const { state } = useLocation()
    const novos = state?.novos ?? []
    const atualizados = state?.atualizados ?? []
    const aviso = state?.aviso
    const houveMudanca = novos.length > 0 || atualizados.length > 0

    const [itens, setItens] = useState([])
    const [carregando, setCarregando] = useState(true)
    const [erro, setErro] = useState('')
    const [busca, setBusca] = useState('')
    const [filtro, setFiltro] = useState('Todos')

    const carregar = useCallback(async () => {
        setCarregando(true)
        setErro('')
        try {
            setItens(await listarArmario())
        } catch (e) {
            setErro(mensagemDeErro(e))
        } finally {
            setCarregando(false)
        }
    }, [])

    useEffect(() => {
        carregar()
    }, [carregar])

    const emAlerta = itens.filter((i) => i.cor === 'VERMELHO' || i.statusEscassez === 'VAZIO').length

    const visiveis = useMemo(() => {
        const termo = normalizar(busca.trim())
        const categoria = FILTROS.find((f) => f.rotulo === filtro)?.categoria
        return itens.filter(
            (item) =>
                (!categoria || item.categoria === categoria) &&
                normalizar(item.nome).includes(termo)
        )
    }, [itens, busca, filtro])

    return (
        <section className="armario">
            <header className="armario__cabecalho">
                <div>
                    <h1 className="titulo-pagina">Armário de Ingredientes</h1>
                    <p className="subtitulo-pagina">
                        Saldo por insumo, já sem os lotes vencidos.
                        {houveMudanca && ' Os cartões tracejados mudaram com as últimas entradas.'}
                    </p>
                </div>
                {ehAdmin && (
                    <Link to="/insumos/novo" className="botao-primario">
                        Novo insumo
                    </Link>
                )}
            </header>

            <div className="armario__busca">
                <div className="armario__campo">
                    <label htmlFor="busca">Buscar insumo</label>
                    <input
                        id="busca"
                        type="search"
                        placeholder="Ex.: Cascade"
                        value={busca}
                        onChange={(e) => setBusca(e.target.value)}
                    />
                </div>

                <div className="armario__filtros" role="group" aria-label="Filtrar por categoria">
                    {FILTROS.map(({ rotulo }) => (
                        <button
                            key={rotulo}
                            type="button"
                            className={`filtro${filtro === rotulo ? ' filtro--ativo' : ''}`}
                            aria-pressed={filtro === rotulo}
                            onClick={() => setFiltro(rotulo)}
                        >
                            {rotulo}
                        </button>
                    ))}
                </div>
            </div>

            {aviso && (
                <p className="armario__alerta" role="alert">
                    {aviso}
                </p>
            )}

            {emAlerta > 0 && (
                <p className="armario__alerta" role="status">
                    {emAlerta === 1
                        ? 'Atenção: 1 insumo está em Validade Crítica.'
                        : `Atenção: ${emAlerta} insumos estão em Validade Crítica.`}
                </p>
            )}

            {carregando ? (
                <p className="armario__vazio" role="status">Carregando…</p>
            ) : erro ? (
                <div className="armario__erro" role="alert">
                    <p>{erro}</p>
                    <button type="button" className="filtro" onClick={carregar}>
                        Tentar de novo
                    </button>
                </div>
            ) : visiveis.length === 0 ? (
                <p className="armario__vazio">Nenhum insumo encontrado.</p>
            ) : (
                <div className="armario__grade">
                    {visiveis.map((item) => {
                        const situacao = situacaoDoItem(item)
                        const novo = novos.includes(item.insumoId)
                        const atualizado = atualizados.includes(item.insumoId)
                        return (
                            <Link
                                key={item.insumoId}
                                to={`/armario/${item.insumoId}`}
                                className={`cartao-insumo${novo || atualizado ? ' cartao-insumo--mudou' : ''}`}
                            >
                                <span className="cartao-insumo__topo">
                                    <span className="cartao-insumo__nome">
                                        <strong>{item.nome}</strong>
                                        {novo && <span className="selo">Novo</span>}
                                    </span>
                                    <strong>{formatarQuantidade(item.saldo, item.unidadeMedida)}</strong>
                                </span>
                                <Barra nivel={situacao.nivel} status={situacao.barra} rotulo={`Nível de ${item.nome}`} />
                                <Etiqueta tipo={situacao.etiqueta}>{situacao.texto}</Etiqueta>
                                {atualizado && <span className="selo">Atualizado</span>}
                            </Link>
                        )
                    })}
                </div>
            )}
        </section>
    )
}
