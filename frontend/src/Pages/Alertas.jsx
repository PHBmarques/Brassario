import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { listarAlertas } from '../Api/alertas.js'
import { mensagemDeErro } from '../Api/erros.js'
import Etiqueta from '../components/Etiqueta.jsx'
import { formatarData, formatarQuantidade } from '../Utils/armario.js'
import '../components/Formulario.css'
import './Alertas.css'

// Nome do alerta na tela (também vai para o aviso da Entrada de estoque)
const ROTULO_ALERTA = {
    VENCIDO: 'Lote vencido',
    VALIDADE_CRITICA: 'Validade crítica',
    ATENCAO: 'Vence em breve',
    ESTOQUE_VAZIO: 'Armário vazio',
    ESTOQUE_BAIXO: 'Estoque baixo',
}

const URGENTES = ['CRITICA', 'ALTA']

function detalheDoAlerta(a) {
    if (a.loteId) {
        const quando =
            a.diasParaVencer < 0
                ? `venceu em ${formatarData(a.dataValidade)}`
                : a.diasParaVencer === 0
                    ? 'vence hoje'
                    : `vence em ${a.diasParaVencer} ${a.diasParaVencer === 1 ? 'dia' : 'dias'}`
        return `Lote ${a.codigoLote}, ${quando}. Saldo: ${formatarQuantidade(a.saldo, a.unidadeMedida)}.`
    }
    return `Saldo: ${formatarQuantidade(a.saldo, a.unidadeMedida)}. Mínimo: ${formatarQuantidade(a.estoqueMinimo, a.unidadeMedida)}.`
}

export default function Alertas() {
    const navigate = useNavigate()
    const [alertas, setAlertas] = useState(null)
    const [erro, setErro] = useState('')

    useEffect(() => {
        let ativo = true
        listarAlertas()
            .then((lista) => ativo && setAlertas(lista))
            .catch((e) => ativo && setErro(mensagemDeErro(e)))
        return () => {
            ativo = false
        }
    }, [])

    function agir(a) {
        if (a.acao === 'DAR_ENTRADA') {
            navigate('/entrada', { state: { insumoId: a.insumoId, origem: `pelo alerta ${ROTULO_ALERTA[a.tipo] ?? ''}`.trim() } })
        } else if (a.acao === 'VER_COMPRAS') {
            navigate('/compras')
        } else {
            // VER_LOTES e REGISTRAR_PERDA: os lotes ficam no detalhe do insumo
            navigate(`/armario/${a.insumoId}`)
        }
    }

    const rotuloAcao = { DAR_ENTRADA: 'Dar entrada', VER_COMPRAS: 'Ver compras', VER_LOTES: 'Ver lotes', REGISTRAR_PERDA: 'Ver lote' }

    const grupos = [
        { titulo: 'Urgente', tipo: 'critica', itens: (alertas ?? []).filter((a) => URGENTES.includes(a.severidade)) },
        { titulo: 'Atenção', tipo: 'atencao', itens: (alertas ?? []).filter((a) => !URGENTES.includes(a.severidade)) },
    ]

    return (
        <section className="alertas">
            <div className="alertas__topo">
                <div>
                    <h1 className="titulo-pagina">Alertas</h1>
                    <p className="subtitulo-pagina">O que precisa de cuidado no Armário. Os mais urgentes primeiro.</p>
                </div>
                <Link to="/compras" className="botao-secundario">
                    Compras sugeridas
                </Link>
            </div>

            {erro && (
                <p className="form-erro" role="alert">
                    {erro}
                </p>
            )}
            {alertas === null && !erro && (
                <p className="form-campo__dica" role="status">
                    Carregando alertas…
                </p>
            )}
            {alertas?.length === 0 && (
                <div className="form-cartao">
                    <p className="form-campo__dica">Nenhum alerta agora. Tudo certo no Armário.</p>
                </div>
            )}

            {grupos
                .filter((g) => g.itens.length > 0)
                .map((g) => (
                    <div key={g.titulo} className="form-cartao">
                        <h2 className="alertas__secao">
                            {g.titulo} <span className="alertas__contagem">({g.itens.length})</span>
                        </h2>
                        <ul className="alertas__lista">
                            {g.itens.map((a) => (
                                <li key={`${a.tipo}-${a.insumoId}-${a.loteId ?? ''}`} className="alertas__item">
                                    <div>
                                        <p className="alertas__nome">
                                            {a.insumoNome} <Etiqueta tipo={g.tipo}>{ROTULO_ALERTA[a.tipo] ?? a.tipo}</Etiqueta>
                                        </p>
                                        <p className="form-campo__dica">{detalheDoAlerta(a)}</p>
                                    </div>
                                    <button type="button" className="botao-secundario alertas__botao" onClick={() => agir(a)}>
                                        {rotuloAcao[a.acao] ?? 'Ver'}
                                    </button>
                                </li>
                            ))}
                        </ul>
                    </div>
                ))}
        </section>
    )
}
