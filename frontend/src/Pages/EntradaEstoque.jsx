import { useEffect, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { listarArmario, registrarEntrada } from '../Api/armario.js'
import { errosPorCampo, mensagemDeErro } from '../Api/erros.js'
import { criarFornecedor, listarFornecedores } from '../Api/fornecedores.js'
import { useAuth } from '../Auth/AuthContext.jsx'
import CampoBusca from '../components/CampoBusca.jsx'
import Etiqueta from '../components/Etiqueta.jsx'
import {
    diasAte,
    formatarData,
    formatarQuantidade,
    hojeIso,
    lerNumero,
    rotuloCategoria,
    rotuloUnidade,
    tipoPorCor,
} from '../Utils/armario.js'
import { guardarNotaFiscal } from '../Utils/notasFiscais.js'
import '../components/Formulario.css'
import './EntradaEstoque.css'

const formatarReais = (valor) => valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

const vazio = () => ({
    insumoId: null,
    fornecedorId: null,
    codigoLote: '',
    notaFiscal: '',
    dataEntrada: hojeIso(),
    dataValidade: '',
    quantidade: '',
    precoUnitario: '',
})

// Erros dos campos já preenchidos (aparecem embaixo de cada campo)
function errosDoForm(f) {
    const erros = {}
    if (f.dataEntrada && diasAte(f.dataEntrada) > 0) erros.dataEntrada = 'A data de entrada não pode ser futura.'
    if (f.dataValidade && diasAte(f.dataValidade) < 0) erros.dataValidade = 'Esta validade já passou.'
    const qtd = lerNumero(f.quantidade)
    if (qtd !== null && !(qtd > 0)) erros.quantidade = 'Use um número maior que zero.'
    const preco = lerNumero(f.precoUnitario)
    if (preco !== null && !(preco >= 0)) erros.precoUnitario = 'Use um número igual ou maior que zero.'
    return erros
}

const OBRIGATORIOS = ['codigoLote', 'dataEntrada', 'dataValidade', 'quantidade', 'precoUnitario']

export default function EntradaEstoque() {
    const navigate = useNavigate()
    const { state } = useLocation()
    const { usuario } = useAuth()
    const ehAdmin = usuario?.perfil === 'ADMIN'

    const [insumos, setInsumos] = useState([])
    const [fornecedores, setFornecedores] = useState([])
    const [erroCarga, setErroCarga] = useState('')
    const [form, setForm] = useState(() => ({ ...vazio(), insumoId: state?.insumoId ?? null }))
    const [errosApi, setErrosApi] = useState({})
    const [erroGeral, setErroGeral] = useState('')
    const [salvando, setSalvando] = useState(false)
    const [registro, setRegistro] = useState(null)

    useEffect(() => {
        let ativo = true
        Promise.all([listarArmario(), listarFornecedores()])
            .then(([listaInsumos, listaFornecedores]) => {
                if (!ativo) return
                setInsumos(listaInsumos)
                setFornecedores(listaFornecedores.filter((f) => f.ativo))
            })
            .catch((e) => ativo && setErroCarga(mensagemDeErro(e)))
        return () => {
            ativo = false
        }
    }, [])

    const alterar = (campo, valor) => {
        setForm((f) => ({ ...f, [campo]: valor }))
        setErrosApi((atual) => (atual[campo] ? { ...atual, [campo]: undefined } : atual))
    }

    const insumo = insumos.find((i) => i.insumoId === form.insumoId) ?? null
    const unidade = insumo ? rotuloUnidade(insumo.unidadeMedida) : null

    const erros = { ...errosDoForm(form), ...Object.fromEntries(Object.entries(errosApi).filter(([, v]) => v)) }
    const completo = form.insumoId != null && OBRIGATORIOS.every((c) => String(form[c]).trim())
    const prontoParaSalvar = completo && Object.keys(errosDoForm(form)).length === 0

    const qtd = lerNumero(form.quantidade)
    const preco = lerNumero(form.precoUnitario)
    const qtdValida = qtd > 0 ? qtd : 0
    const valorTotal = qtdValida && preco >= 0 ? qtdValida * preco : 0

    // ---------- Novo insumo / novo fornecedor ----------
    const irParaNovoInsumo = (nome) => navigate('/insumos/novo', { state: { origem: 'entrada', nome } })

    async function cadastrarFornecedor(nome) {
        try {
            const novo = await criarFornecedor({ nome })
            setFornecedores((lista) => [...lista, novo].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR')))
            alterar('fornecedorId', novo.id)
        } catch (e) {
            setErroGeral(mensagemDeErro(e))
        }
    }

    async function salvar(e) {
        e.preventDefault()
        if (!prontoParaSalvar || salvando) return
        setSalvando(true)
        setErroGeral('')
        try {
            const lote = await registrarEntrada({
                insumoId: form.insumoId,
                fornecedorId: form.fornecedorId ?? undefined,
                codigoLote: form.codigoLote.trim(),
                dataEntrada: form.dataEntrada,
                dataValidade: form.dataValidade,
                quantidade: qtd,
                precoUnitario: preco,
            })
            const nf = form.notaFiscal.trim()
            guardarNotaFiscal(lote.id, nf)
            setRegistro({ lote, nf, unidade: insumo.unidadeMedida, saldoAntes: insumo.saldo })
            // Atualiza o saldo na lista, para a próxima entrada já mostrar o valor certo
            setInsumos((lista) =>
                lista.map((i) => (i.insumoId === lote.insumoId ? { ...i, saldo: i.saldo + lote.quantidadeInicial } : i))
            )
        } catch (err) {
            setErrosApi(errosPorCampo(err))
            setErroGeral(mensagemDeErro(err))
        } finally {
            setSalvando(false)
        }
    }

    function registrarOutra() {
        setRegistro(null)
        setForm(vazio())
        setErrosApi({})
        setErroGeral('')
    }

    // ---------- Entrada registrada ----------
    if (registro) {
        const { lote, nf, unidade: un, saldoAntes } = registro
        const saldoDepois = saldoAntes + lote.quantidadeInicial
        const linhas = [
            ['Insumo', lote.insumoNome],
            ['Fornecedor', lote.fornecedorNome ?? '—'],
            ['Código do lote', lote.codigoLote],
            ['Nota fiscal', nf || '—'],
            ['Entrada', formatarData(lote.dataEntrada)],
            ['Validade', formatarData(lote.dataValidade)],
            ['Quantidade', formatarQuantidade(lote.quantidadeInicial, un)],
            ['Valor total', formatarReais(lote.quantidadeInicial * lote.precoUnitario)],
        ]
        return (
            <section className="entrada">
                <h1 className="titulo-pagina">Entrada de estoque</h1>
                <p className="entrada__sucesso" role="status">
                    Entrada registrada. O saldo do {lote.insumoNome} agora é {formatarQuantidade(saldoDepois, un)}.
                </p>

                <div className="entrada__grade">
                    <div className="form-cartao">
                        <h2 className="entrada__secao">Lote registrado</h2>
                        <table className="entrada__tabela">
                            <tbody>
                                {linhas.map(([rotulo, valor]) => (
                                    <tr key={rotulo}>
                                        <th scope="row">{rotulo}</th>
                                        <td>{valor}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                        <div className="form-acoes">
                            <Link
                                to="/armario"
                                state={{ atualizados: [lote.insumoId] }}
                                className="botao-secundario"
                            >
                                Ver no Armário
                            </Link>
                            <button type="button" className="botao-primario" onClick={registrarOutra}>
                                Registrar outra entrada
                            </button>
                        </div>
                    </div>

                    <aside className="form-cartao entrada__resumo">
                        <h2 className="entrada__secao">O que mudou no estoque</h2>
                        <span className="entrada__dica">Saldo do {lote.insumoNome}</span>
                        <strong className="entrada__mudanca">
                            {formatarQuantidade(saldoAntes, un)} para {formatarQuantidade(saldoDepois, un)}
                        </strong>
                        <p className="entrada__dica">
                            O lote entra na fila de uso: o que vence primeiro sai primeiro.
                        </p>
                        <span>
                            <Etiqueta tipo={tipoPorCor(lote.situacaoValidade)}>
                                Validade em {lote.diasParaVencer} {lote.diasParaVencer === 1 ? 'dia' : 'dias'}
                            </Etiqueta>
                        </span>
                    </aside>
                </div>
            </section>
        )
    }

    // ---------- Formulário ----------
    const erroDe = (campo) =>
        erros[campo] && (
            <span className="form-campo__erro" id={`${campo}-erro`}>
                {erros[campo]}
            </span>
        )

    const propsCampo = (campo) => ({
        id: campo,
        value: form[campo],
        onChange: (e) => alterar(campo, e.target.value),
        'aria-invalid': Boolean(erros[campo]),
        'aria-describedby': erros[campo] ? `${campo}-erro` : `${campo}-dica`,
    })

    const opcoesInsumo = insumos.map((i) => ({
        id: i.insumoId,
        nome: i.nome,
        detalhe: `saldo ${formatarQuantidade(i.saldo, i.unidadeMedida)}`,
    }))

    const resumoLote = [
        form.codigoLote.trim() && `Lote ${form.codigoLote.trim()}`,
        form.notaFiscal.trim() && `nota fiscal ${form.notaFiscal.trim()}`,
    ]
        .filter(Boolean)
        .join(', ')

    const situacao = prontoParaSalvar
        ? { tipo: 'ok', texto: 'Tudo certo para salvar' }
        : Object.keys(erros).length > 0
            ? { tipo: 'critica', texto: 'Confira os campos em vermelho' }
            : { tipo: 'atencao', texto: 'Preencha os campos obrigatórios' }

    return (
        <section className="entrada">
            <h1 className="titulo-pagina">Entrada de estoque</h1>
            <p className="subtitulo-pagina">Registre o lote que chegou. Campos com * são obrigatórios.</p>

            {erroCarga && (
                <p className="form-erro entrada__aviso" role="alert">
                    {erroCarga}
                </p>
            )}

            <div className="entrada__grade">
                <form className="form-cartao" onSubmit={salvar} noValidate>
                    {erroGeral && (
                        <p className="form-erro" role="alert">
                            {erroGeral}
                        </p>
                    )}

                    <div className="form-linha">
                        <div className="form-campo">
                            <div className="form-campo__rotulo">
                                <label htmlFor="insumo">Insumo *</label>
                                {ehAdmin && (
                                    <button type="button" className="form-campo__acao" onClick={() => irParaNovoInsumo('')}>
                                        + Novo insumo
                                    </button>
                                )}
                            </div>
                            <CampoBusca
                                id="insumo"
                                opcoes={opcoesInsumo}
                                valor={form.insumoId}
                                aoEscolher={(o) => alterar('insumoId', o?.id ?? null)}
                                criar={
                                    ehAdmin && {
                                        rotulo: (texto) => `Cadastrar “${texto}” como novo insumo`,
                                        aoCriar: irParaNovoInsumo,
                                    }
                                }
                                invalido={Boolean(erros.insumoId)}
                                descritoPor="insumo-dica"
                            />
                            {erroDe('insumoId') || (
                                <span className="form-campo__dica" id="insumo-dica">
                                    {insumo
                                        ? `${rotuloCategoria(insumo.categoria)}, medido em ${unidade}. Saldo atual: ${formatarQuantidade(insumo.saldo, insumo.unidadeMedida)}.`
                                        : ehAdmin
                                            ? 'Digite para buscar. Se não existir, use "Novo insumo".'
                                            : 'Digite para buscar.'}
                                </span>
                            )}
                        </div>

                        <div className="form-campo">
                            <div className="form-campo__rotulo">
                                <label htmlFor="fornecedor">Fornecedor</label>
                                {ehAdmin && (
                                    <button
                                        type="button"
                                        className="form-campo__acao"
                                        onClick={() => document.getElementById('fornecedor')?.focus()}
                                    >
                                        + Novo fornecedor
                                    </button>
                                )}
                            </div>
                            <CampoBusca
                                id="fornecedor"
                                opcoes={fornecedores}
                                valor={form.fornecedorId}
                                aoEscolher={(o) => alterar('fornecedorId', o?.id ?? null)}
                                criar={
                                    ehAdmin && {
                                        rotulo: (texto) => `Cadastrar “${texto}” como novo fornecedor`,
                                        aoCriar: cadastrarFornecedor,
                                    }
                                }
                                descritoPor="fornecedor-dica"
                            />
                            {erroDe('fornecedorId') || (
                                <span className="form-campo__dica" id="fornecedor-dica">
                                    Opcional.
                                </span>
                            )}
                        </div>
                    </div>

                    <div className="form-linha">
                        <div className="form-campo">
                            <label htmlFor="codigoLote">Código do lote *</label>
                            <input type="text" maxLength={60} placeholder="Ex.: NF1234-A" {...propsCampo('codigoLote')} />
                            {erroDe('codigoLote') || (
                                <span className="form-campo__dica" id="codigoLote-dica">
                                    Está na embalagem ou na nota fiscal.
                                </span>
                            )}
                        </div>
                        <div className="form-campo">
                            <label htmlFor="notaFiscal">Nº da nota fiscal (NF)</label>
                            <input type="text" placeholder="Ex.: 004512" {...propsCampo('notaFiscal')} />
                            <span className="form-campo__dica" id="notaFiscal-dica">
                                Ajuda a rastrear a compra.
                            </span>
                        </div>
                    </div>

                    <div className="form-linha">
                        <div className="form-campo">
                            <label htmlFor="dataEntrada">Data de entrada *</label>
                            <input type="date" max={hojeIso()} {...propsCampo('dataEntrada')} />
                            {erroDe('dataEntrada')}
                        </div>
                        <div className="form-campo">
                            <label htmlFor="dataValidade">Validade *</label>
                            <input type="date" min={hojeIso()} {...propsCampo('dataValidade')} />
                            {erroDe('dataValidade')}
                        </div>
                    </div>

                    <div className="form-linha">
                        <div className="form-campo">
                            <label htmlFor="quantidade">Quantidade{unidade && ` (${unidade})`} *</label>
                            <input type="text" inputMode="decimal" placeholder="0,000" {...propsCampo('quantidade')} />
                            {erroDe('quantidade')}
                        </div>
                        <div className="form-campo">
                            <label htmlFor="precoUnitario">Preço por {unidade ?? 'unidade'} (R$) *</label>
                            <input type="text" inputMode="decimal" placeholder="0,00" {...propsCampo('precoUnitario')} />
                            {erroDe('precoUnitario')}
                        </div>
                    </div>

                    <div className="form-acoes">
                        <Link to="/armario" className="botao-secundario">
                            Cancelar
                        </Link>
                        <button type="submit" className="botao-primario" disabled={!prontoParaSalvar || salvando}>
                            {salvando ? 'Salvando…' : 'Salvar entrada'}
                        </button>
                    </div>
                </form>

                <aside className="form-cartao entrada__resumo" aria-label="Resumo do lote">
                    <h2 className="entrada__secao">Resumo do lote</h2>
                    <div>
                        <span className="entrada__dica">Valor total</span>
                        <strong className="entrada__valor">{formatarReais(valorTotal)}</strong>
                    </div>
                    {insumo ? (
                        <>
                            {resumoLote && <p className="entrada__dica">{resumoLote}</p>}
                            <p className="entrada__dica">
                                Saldo do {insumo.nome} depois da entrada:{' '}
                                {formatarQuantidade(insumo.saldo + qtdValida, insumo.unidadeMedida)}
                            </p>
                        </>
                    ) : (
                        <p className="entrada__dica">Escolha o insumo para ver o saldo atual.</p>
                    )}
                    <span>
                        <Etiqueta tipo={situacao.tipo}>{situacao.texto}</Etiqueta>
                    </span>
                </aside>
            </div>
        </section>
    )
}
