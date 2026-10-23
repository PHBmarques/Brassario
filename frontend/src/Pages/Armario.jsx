import { useMemo, useState } from 'react'
import Barra from '../components/Barra.jsx'
import Etiqueta from '../components/Etiqueta.jsx'
import { INSUMOS } from '../Utils/mock.js'
import './Armario.css'

const FILTROS = ['Todos', 'Malte', 'Lúpulo', 'Levedura']

// "Lúpulo" e "lupulo" contam como iguais na busca
const normalizar = (texto) =>
    texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase()

const formatarQuantidade = (valor, unidade) =>
    `${valor.toLocaleString('pt-BR')} ${unidade}`

// vazio usa a etiqueta vermelha, igual à validade crítica
const tipoEtiqueta = (status) => (status === 'vazio' ? 'critica' : status)

export default function Armario() {
    const [busca, setBusca] = useState('')
    const [filtro, setFiltro] = useState('Todos')

    const emAlerta = INSUMOS.filter((i) => i.status === 'critica' || i.status === 'vazio').length

    const visiveis = useMemo(() => {
        const termo = normalizar(busca.trim())
        return INSUMOS.filter(
            (insumo) =>
                (filtro === 'Todos' || insumo.categoria === filtro) &&
                normalizar(insumo.nome).includes(termo)
        )
    }, [busca, filtro])

    return (
        <section className="armario">
            <h1 className="titulo-pagina">Armário de Ingredientes</h1>
            <p className="subtitulo-pagina">Saldo por insumo, já sem os lotes vencidos.</p>

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
                    {FILTROS.map((nome) => (
                        <button
                            key={nome}
                            type="button"
                            className={`filtro${filtro === nome ? ' filtro--ativo' : ''}`}
                            aria-pressed={filtro === nome}
                            onClick={() => setFiltro(nome)}
                        >
                            {nome}
                        </button>
                    ))}
                </div>
            </div>

            {emAlerta > 0 && (
                <p className="armario__alerta" role="status">
                    Atenção: {emAlerta} insumos estão em Validade Crítica.
                </p>
            )}

            {visiveis.length === 0 ? (
                <p className="armario__vazio">Nenhum insumo encontrado.</p>
            ) : (
                <div className="armario__grade">
                    {visiveis.map((insumo) => (
                        <article key={insumo.id} className="cartao-insumo">
                            <header className="cartao-insumo__topo">
                                <h2>{insumo.nome}</h2>
                                <strong>{formatarQuantidade(insumo.quantidade, insumo.unidade)}</strong>
                            </header>
                            <Barra nivel={insumo.nivel} status={insumo.status} rotulo={`Nível de ${insumo.nome}`} />
                            <Etiqueta tipo={tipoEtiqueta(insumo.status)}>{insumo.texto}</Etiqueta>
                        </article>
                    ))}
                </div>
            )}
        </section>
    )
}
