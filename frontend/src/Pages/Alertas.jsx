import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { listarAlertas } from '../Api/alertas.js'
import { mensagemDeErro } from '../Api/erros.js'
import Etiqueta from '../components/Etiqueta.jsx'
import ModalPerda from '../components/ModalPerda.jsx'
import { ehUrgente, ROTULO_ALERTA } from '../Utils/alertas.js'
import { FILTROS, botoesDoAlerta, contarPorFiltro, filtrarAlertas, rotuloDoAlerta, textoDoAlerta } from '../Utils/alertasPainel.js'
import '../components/Formulario.css'
import './Alertas.css'

const chaveDe = (a) => `${a.tipo}-${a.insumoId}-${a.loteId ?? ''}`

export default function Alertas() {
    const navigate = useNavigate()
    const [alertas, setAlertas] = useState(null)
    const [erro, setErro] = useState('')
    const [filtro, setFiltro] = useState('TODOS')
    const [perda, setPerda] = useState(null) // alerta do lote em que se registra a perda
    const [aviso, setAviso] = useState('')

    const carregar = useCallback(() => {
        listarAlertas()
            .then((lista) => {
                setAlertas(lista)
                setErro('')
            })
            .catch((e) => setErro(mensagemDeErro(e)))
    }, [])

    useEffect(() => {
        carregar()
    }, [carregar])

    function agir(botao, a) {
        switch (botao) {
            case 'PERDA':
                setPerda(a)
                break
            case 'ENTRADA':
                navigate('/entrada', { state: { insumoId: a.insumoId, origem: `pelo alerta ${ROTULO_ALERTA[a.tipo] ?? ''}`.trim() } })
                break
            case 'COMPRA':
                navigate('/compras')
                break
            case 'RECEITA':
                navigate('/receitas')
                break
            default: // LOTES: os lotes ficam no detalhe do insumo
                navigate(`/armario/${a.insumoId}`)
        }
    }

    const concluirPerda = (a) => {
        setPerda(null)
        setAviso(`Perda registrada no lote ${a.codigoLote}.`)
        carregar()
    }

    const contagem = contarPorFiltro(alertas ?? [])
    const visiveis = filtrarAlertas(alertas ?? [], filtro)
    const grupos = [
        { titulo: 'Urgente', itens: visiveis.filter(ehUrgente) },
        { titulo: 'Atenção', itens: visiveis.filter((a) => !ehUrgente(a)) },
    ]

    return (
        <section className="alertas">
            <div>
                <h1 className="titulo-pagina">Alertas</h1>
                <p className="subtitulo-pagina">Validade e escassez, dos mais urgentes para os menos urgentes.</p>
            </div>

            {alertas && (
                <div className="alertas__filtros" role="group" aria-label="Filtrar alertas">
                    {FILTROS.map((f) => (
                        <button
                            key={f.id}
                            type="button"
                            className="alertas__filtro"
                            aria-pressed={filtro === f.id}
                            onClick={() => setFiltro(f.id)}
                        >
                            {f.rotulo} ({contagem[f.id]})
                        </button>
                    ))}
                </div>
            )}

            {aviso && (
                <p className="alertas__aviso" role="status">
                    {aviso}
                </p>
            )}
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
            {alertas?.length > 0 && visiveis.length === 0 && (
                <div className="form-cartao">
                    <p className="form-campo__dica">Nenhum alerta neste filtro.</p>
                </div>
            )}

            {grupos
                .filter((g) => g.itens.length > 0)
                .map((g) => (
                    <div key={g.titulo} className="alertas__grupo">
                        <h2 className="alertas__grupo-titulo">{g.titulo}</h2>
                        <ul className="alertas__grade">
                            {g.itens.map((a) => (
                                <li key={chaveDe(a)} className="alerta-card">
                                    <div className="alerta-card__topo">
                                        <p className="alerta-card__nome">{a.insumoNome}</p>
                                        <Etiqueta tipo={ehUrgente(a) ? 'critica' : 'atencao'}>{rotuloDoAlerta(a)}</Etiqueta>
                                    </div>
                                    <p className="alerta-card__texto">{textoDoAlerta(a)}</p>
                                    <div className="alerta-card__acoes">
                                        {botoesDoAlerta(a).map((b) => (
                                            <button
                                                key={b.id}
                                                type="button"
                                                className="botao-secundario alertas__botao"
                                                onClick={() => agir(b.id, a)}
                                            >
                                                {b.rotulo}
                                            </button>
                                        ))}
                                    </div>
                                </li>
                            ))}
                        </ul>
                    </div>
                ))}

            {perda && <ModalPerda alerta={perda} aoFechar={() => setPerda(null)} aoConcluir={concluirPerda} />}
        </section>
    )
}
