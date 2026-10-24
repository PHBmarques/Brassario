import { useEffect, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { produzir } from '../Api/bateladas.js'
import { errosPorCampo, mensagemDeErro } from '../Api/erros.js'
import { listarReceitas, previaProducao } from '../Api/receitas.js'
import { formatarData, formatarQuantidade, lerNumero } from '../Utils/armario.js'
import { formatarLitros, formatarReais } from '../Utils/formato.js'
import '../components/Formulario.css'
import './Producao.css'

// Um texto por insumo com problema, juntando a falta e os lotes vencidos dele
function textosDosProblemas(previa) {
    const porInsumo = new Map()
    for (const p of previa.problemas) {
        if (!porInsumo.has(p.insumoId)) porInsumo.set(p.insumoId, { nome: p.insumo, falta: null, vencidos: [] })
        const grupo = porInsumo.get(p.insumoId)
        if (p.tipo === 'FALTA') grupo.falta = p
        else grupo.vencidos.push(p)
    }
    return [...porInsumo.entries()].map(([insumoId, g]) => {
        const linha = previa.itens.find((i) => i.insumoId === insumoId)
        const partes = []
        if (g.falta) {
            const un = g.falta.unidadeMedida
            partes.push(
                `faltam ${formatarQuantidade(g.falta.falta, un)}` +
                    (linha
                        ? ` (necessário ${formatarQuantidade(linha.necessario, un)}, disponível ${formatarQuantidade(linha.disponivel, un)})`
                        : '')
            )
        }
        for (const v of g.vencidos) {
            partes.push(`o lote ${v.lote} está vencido (venceu em ${formatarData(v.validade)}) e não pode ser usado`)
        }
        const texto = partes.join('. ')
        return `${g.nome}: ${texto.charAt(0).toLowerCase()}${texto.slice(1)}.`
    })
}

export default function Produzir() {
    const navigate = useNavigate()
    const [params] = useSearchParams()

    const [receitas, setReceitas] = useState(null)
    const [receitaId, setReceitaId] = useState(params.get('receita') ?? '')
    const [volume, setVolume] = useState(params.get('volume')?.replace('.', ',') ?? '')
    const [observacoes, setObservacoes] = useState('')
    const [previa, setPrevia] = useState(null)
    const [calculando, setCalculando] = useState(false)
    const [erroPrevia, setErroPrevia] = useState('')
    const [erros, setErros] = useState({})
    const [erroGeral, setErroGeral] = useState('')
    const [confirmando, setConfirmando] = useState(false)
    const [versao, setVersao] = useState(0) // força refazer a prévia (ex.: depois de um erro ao confirmar)

    useEffect(() => {
        let ativo = true
        listarReceitas()
            .then((lista) => {
                if (!ativo) return
                const ativas = lista.filter((r) => r.ativa)
                setReceitas(ativas)
                // Sem volume na URL, começa pelo volume base da receita escolhida
                const escolhida = ativas.find((r) => String(r.id) === (params.get('receita') ?? ''))
                if (escolhida && !params.get('volume')) setVolume(String(escolhida.volumeBaseLitros).replace('.', ','))
            })
            .catch((e) => ativo && setErroGeral(mensagemDeErro(e)))
        return () => {
            ativo = false
        }
        // Só na abertura da tela: os parâmetros da URL são o ponto de partida
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [])

    const receita = receitas?.find((r) => String(r.id) === receitaId) ?? null
    const litros = lerNumero(volume)
    const volumeValido = Number.isFinite(litros) && litros > 0

    // Refaz a prévia quando a receita ou o volume mudam (espera a pessoa parar de digitar)
    useEffect(() => {
        if (!receitaId || !volumeValido) return undefined
        let ativo = true
        const espera = setTimeout(() => {
            setCalculando(true)
            previaProducao(receitaId, litros)
                .then((p) => {
                    if (!ativo) return
                    setPrevia(p)
                    setErroPrevia('')
                })
                .catch((e) => ativo && setErroPrevia(mensagemDeErro(e)))
                .finally(() => ativo && setCalculando(false))
        }, 350)
        return () => {
            ativo = false
            clearTimeout(espera)
        }
    }, [receitaId, litros, volumeValido, versao])

    const escolherReceita = (e) => {
        const id = e.target.value
        setReceitaId(id)
        setPrevia(null)
        const r = receitas?.find((x) => String(x.id) === id)
        if (r) setVolume(String(r.volumeBaseLitros).replace('.', ','))
        setErros((x) => ({ ...x, receitaId: undefined }))
    }

    // A prévia mostrada precisa ser a do que está nos campos
    const previaAtual =
        previa && receitaId && String(previa.receitaId) === receitaId && volumeValido && previa.quantidade === litros
            ? previa
            : null
    const problemas = previaAtual ? textosDosProblemas(previaAtual) : []
    const emFalta = previaAtual ? new Set(previaAtual.problemas.map((p) => p.insumoId)).size : 0
    const podeConfirmar = Boolean(previaAtual?.podeConfirmar) && !calculando && !confirmando

    async function confirmar() {
        const encontrados = {}
        if (!receitaId) encontrados.receitaId = 'Escolha a receita.'
        if (!volumeValido) encontrados.volumePlanejado = 'Informe um volume maior que zero.'
        if (observacoes.length > 255) encontrados.observacoes = 'No máximo 255 caracteres.'
        setErros(encontrados)
        if (Object.keys(encontrados).length > 0 || !podeConfirmar) return

        setConfirmando(true)
        setErroGeral('')
        try {
            const producao = await produzir({
                receitaId: Number(receitaId),
                volumePlanejado: litros,
                observacoes: observacoes.trim() || undefined,
            })
            navigate('/bateladas', { state: { producao } })
        } catch (err) {
            setErros(errosPorCampo(err))
            setErroGeral(mensagemDeErro(err))
            // O estoque pode ter mudado desde a prévia: refaz a simulação
            setVersao((v) => v + 1)
            setConfirmando(false)
        }
    }

    // Linhas da tabela: o nome e os totais do insumo só na primeira linha dele
    const linhas = (previaAtual?.itens ?? []).map((item, i, todos) => ({
        ...item,
        primeira: i === 0 || todos[i - 1].insumoId !== item.insumoId,
    }))

    const custoPorLitro = previaAtual && litros > 0 ? previaAtual.custoEstimado / litros : 0

    return (
        <section className="producao">
            <h1 className="titulo-pagina">Nova produção</h1>
            <p className="subtitulo-pagina">
                Escolha a receita e o volume, e confira os lotes. O estoque só é abatido quando você confirmar.
            </p>

            {erroGeral && (
                <p className="form-erro" role="alert">
                    {erroGeral}
                </p>
            )}

            <div className="form-cartao">
                <div className="producao__linha">
                    <div className="form-campo">
                        <label htmlFor="receitaId">Receita *</label>
                        <select
                            id="receitaId"
                            value={receitaId}
                            onChange={escolherReceita}
                            disabled={!receitas || confirmando}
                            aria-invalid={Boolean(erros.receitaId)}
                            aria-describedby="receitaId-dica"
                        >
                            <option value="">{receitas ? 'Escolha a receita…' : 'Carregando receitas…'}</option>
                            {(receitas ?? []).map((r) => (
                                <option key={r.id} value={String(r.id)}>
                                    {r.nome}
                                </option>
                            ))}
                        </select>
                        <span className={erros.receitaId ? 'form-campo__erro' : 'form-campo__dica'} id="receitaId-dica">
                            {erros.receitaId ??
                                (receita
                                    ? `${receita.estilo ? `${receita.estilo}, base` : 'Base'} de ${formatarLitros(receita.volumeBaseLitros)}.`
                                    : 'Só aparecem as receitas ativas.')}
                        </span>
                    </div>
                    <div className="form-campo">
                        <label htmlFor="volume">Volume a produzir (L) *</label>
                        <input
                            id="volume"
                            type="text"
                            inputMode="decimal"
                            placeholder="Ex.: 100"
                            value={volume}
                            onChange={(e) => {
                                setVolume(e.target.value)
                                setErros((x) => ({ ...x, volumePlanejado: undefined }))
                            }}
                            disabled={confirmando}
                            aria-invalid={Boolean(erros.volumePlanejado) || (volume !== '' && !volumeValido)}
                            aria-describedby="volume-erro"
                        />
                        {(erros.volumePlanejado || (volume !== '' && !volumeValido)) && (
                            <span className="form-campo__erro" id="volume-erro">
                                {erros.volumePlanejado ?? 'Informe um volume maior que zero.'}
                            </span>
                        )}
                    </div>
                </div>

                <div className="form-campo">
                    <label htmlFor="observacoes">Observações</label>
                    <input
                        id="observacoes"
                        type="text"
                        maxLength={255}
                        placeholder="Ex.: Produção de terça"
                        value={observacoes}
                        onChange={(e) => setObservacoes(e.target.value)}
                        disabled={confirmando}
                        aria-invalid={Boolean(erros.observacoes)}
                        aria-describedby="observacoes-dica"
                    />
                    <span className={erros.observacoes ? 'form-campo__erro' : 'form-campo__dica'} id="observacoes-dica">
                        {erros.observacoes ?? 'Opcional, até 255 caracteres.'}
                    </span>
                </div>
            </div>

            <div className="form-cartao">
                <div>
                    <h2 className="producao__secao">Ingredientes e lotes que serão usados</h2>
                    <p className="form-campo__dica">O que vence primeiro sai primeiro. Lotes vencidos não entram.</p>
                </div>

                {erroPrevia && (
                    <p className="form-erro" role="alert">
                        {erroPrevia}
                    </p>
                )}

                {problemas.length > 0 && (
                    <div className="producao__falta" role="alert">
                        <strong>Faltam insumos para esta produção:</strong>
                        <ul>
                            {problemas.map((t) => (
                                <li key={t}>{t}</li>
                            ))}
                        </ul>
                    </div>
                )}

                {!receitaId || !volumeValido ? (
                    <p className="form-campo__dica">Escolha a receita e o volume para ver os lotes.</p>
                ) : !previaAtual ? (
                    <p className="form-campo__dica" role="status">
                        Simulando a produção…
                    </p>
                ) : (
                    <div className="producao__rolagem">
                        <table className="tabela-dados">
                            <thead>
                                <tr>
                                    <th scope="col">Insumo</th>
                                    <th scope="col">Necessário</th>
                                    <th scope="col">Disponível</th>
                                    <th scope="col">Lote</th>
                                    <th scope="col">Validade</th>
                                    <th scope="col">Sai do lote</th>
                                </tr>
                            </thead>
                            <tbody>
                                {linhas.map((l, i) => (
                                    <tr key={`${l.insumoId}-${l.loteId ?? i}`}>
                                        <th scope="row">{l.primeira ? l.insumo : ''}</th>
                                        <td>{l.primeira ? formatarQuantidade(l.necessario, l.unidadeMedida) : ''}</td>
                                        <td>{l.primeira ? formatarQuantidade(l.disponivel, l.unidadeMedida) : ''}</td>
                                        <td>{l.lote ?? 'Sem lote válido'}</td>
                                        <td>{l.validade ? formatarData(l.validade) : '—'}</td>
                                        <td className="producao__forte">
                                            {l.lote ? formatarQuantidade(l.saiDoLote, l.unidadeMedida) : '—'}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>

            <div className="form-cartao">
                <h2 className="producao__secao">Resumo</h2>
                <div className="producao__resumo">
                    <div>
                        <span className="form-campo__dica">Custo estimado</span>
                        <strong className="producao__custo">{formatarReais(previaAtual?.custoEstimado ?? 0)}</strong>
                        <span className="form-campo__dica">{formatarReais(custoPorLitro)} por litro</span>
                    </div>

                    {previaAtual && (
                        <p className={`producao__pilula${previaAtual.podeConfirmar ? '' : ' producao__pilula--falta'}`}>
                            {previaAtual.podeConfirmar
                                ? 'Todos os ingredientes estão disponíveis.'
                                : `${emFalta} ${emFalta === 1 ? 'ingrediente em falta' : 'ingredientes em falta'}. Confirmar está bloqueado.`}
                        </p>
                    )}

                    <div className="producao__acoes">
                        <Link to={receitaId ? `/receitas/${receitaId}` : '/receitas'} className="botao-secundario">
                            Cancelar
                        </Link>
                        <button type="button" className="botao-primario" onClick={confirmar} disabled={!podeConfirmar}>
                            {confirmando ? 'Confirmando…' : 'Confirmar produção'}
                        </button>
                    </div>
                </div>
                <p className="form-campo__dica">
                    {previaAtual && !previaAtual.podeConfirmar
                        ? 'Registre a entrada dos insumos em falta (menu Entrada de estoque) e refaça a simulação.'
                        : 'Ao confirmar, o estoque é abatido na hora. Para desfazer, cancele a produção.'}
                </p>
            </div>
        </section>
    )
}
