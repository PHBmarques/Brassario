import { useEffect, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { listarArmario } from '../Api/armario.js'
import { registrarEntradaComposta } from '../Api/entradas.js'
import { mensagemDeErro } from '../Api/erros.js'
import { listarFornecedores } from '../Api/fornecedores.js'
import CampoBusca from '../components/CampoBusca.jsx'
import Etiqueta from '../components/Etiqueta.jsx'
import {
    CATEGORIAS,
    UNIDADES,
    diasAte,
    formatarData,
    formatarQuantidade,
    hojeIso,
    lerNumero,
    rotuloCategoria,
    rotuloUnidade,
    tipoPorCor,
} from '../Utils/armario.js'
import { formatarReais, normalizarTexto } from '../Utils/formato.js'
import { guardarNotaFiscal } from '../Utils/notasFiscais.js'
import '../components/Formulario.css'
import './EntradaEstoque.css'

// Ids "falsos" para o insumo/fornecedor novo aparecer escolhido no campo de busca
const ID_NOVO = 'novo'

// Os avisos de validade do insumo novo usam o padrão; dá para ajustar depois, no Armário
const DIAS_ATENCAO_PADRAO = 45
const DIAS_CRITICA_PADRAO = 15

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

const rascunhoInsumo = (nome = '') => ({ nome, categoria: 'MALTE', unidadeMedida: 'KG', estoqueMinimo: '' })
const rascunhoFornecedor = (nome = '') => ({ nome, contato: '' })

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

// A API devolve os erros com o caminho do campo: "lote.quantidade", "novoInsumo.nome"...
function separarErrosApi(erro) {
    const detalhes = erro.response?.data?.detalhes
    const lote = {}
    const insumo = {}
    const fornecedor = {}
    if (Array.isArray(detalhes)) {
        for (const { campo, erro: msg } of detalhes) {
            if (!campo) continue
            const [grupo, nome] = campo.includes('.') ? campo.split('.') : ['lote', campo]
            if (grupo === 'novoInsumo') insumo[nome] = msg
            else if (grupo === 'novoFornecedor') fornecedor[nome] = msg
            else lote[nome] = msg
        }
    }
    return { lote, insumo, fornecedor }
}

export default function EntradaEstoque() {
    const { state } = useLocation()

    const [insumos, setInsumos] = useState([])
    const [fornecedores, setFornecedores] = useState([])
    const [erroCarga, setErroCarga] = useState('')
    const [form, setForm] = useState(() => ({ ...vazio(), insumoId: state?.insumoId ?? null }))
    const [errosApi, setErrosApi] = useState({})
    const [erroGeral, setErroGeral] = useState('')
    const [salvando, setSalvando] = useState(false)
    const [registro, setRegistro] = useState(null)

    // Insumo/fornecedor novos: o painel aberto (rascunho) e o que já foi adicionado (vai junto com a entrada)
    const [painelInsumo, setPainelInsumo] = useState(null)
    const [errosInsumo, setErrosInsumo] = useState({})
    const [novoInsumo, setNovoInsumo] = useState(null)
    const [painelFornecedor, setPainelFornecedor] = useState(null)
    const [errosFornecedor, setErrosFornecedor] = useState({})
    const [novoFornecedor, setNovoFornecedor] = useState(null)

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
    const unidadeMedida = novoInsumo?.unidadeMedida ?? insumo?.unidadeMedida ?? null
    const unidade = unidadeMedida ? rotuloUnidade(unidadeMedida) : null
    const temInsumo = Boolean(insumo || novoInsumo)

    const erros = { ...errosDoForm(form), ...Object.fromEntries(Object.entries(errosApi).filter(([, v]) => v)) }
    const completo = temInsumo && OBRIGATORIOS.every((c) => String(form[c]).trim())
    const prontoParaSalvar = completo && Object.keys(errosDoForm(form)).length === 0 && !painelInsumo && !painelFornecedor

    const qtd = lerNumero(form.quantidade)
    const preco = lerNumero(form.precoUnitario)
    const qtdValida = qtd > 0 ? qtd : 0
    const valorTotal = qtdValida && preco >= 0 ? qtdValida * preco : 0

    // ---------- Novo insumo ----------
    function abrirNovoInsumo(nome = '') {
        setPainelInsumo(novoInsumo ? { ...novoInsumo } : rascunhoInsumo(nome))
        setErrosInsumo({})
        alterar('insumoId', null)
    }

    function adicionarInsumo() {
        const r = painelInsumo
        const e = {}
        const nome = r.nome.trim()
        if (!nome) e.nome = 'Informe o nome do insumo.'
        else if (insumos.some((i) => normalizarTexto(i.nome) === normalizarTexto(nome)))
            e.nome = 'Já existe um insumo com esse nome. Escolha-o na lista.'
        const minimo = lerNumero(r.estoqueMinimo)
        if (minimo === null) e.estoqueMinimo = 'Informe o estoque mínimo.'
        else if (Number.isNaN(minimo) || minimo < 0) e.estoqueMinimo = 'Use um número igual ou maior que zero.'
        setErrosInsumo(e)
        if (Object.keys(e).length > 0) return
        setNovoInsumo({ ...r, nome })
        setPainelInsumo(null)
    }

    function cancelarNovoInsumo() {
        setPainelInsumo(null)
        setErrosInsumo({})
    }

    // ---------- Novo fornecedor ----------
    function abrirNovoFornecedor(nome = '') {
        setPainelFornecedor(novoFornecedor ? { ...novoFornecedor } : rascunhoFornecedor(nome))
        setErrosFornecedor({})
        alterar('fornecedorId', null)
    }

    function adicionarFornecedor() {
        const r = painelFornecedor
        const e = {}
        const nome = r.nome.trim()
        if (!nome) e.nome = 'Informe o nome do fornecedor.'
        else if (fornecedores.some((f) => normalizarTexto(f.nome) === normalizarTexto(nome)))
            e.nome = 'Já existe um fornecedor com esse nome. Escolha-o na lista.'
        setErrosFornecedor(e)
        if (Object.keys(e).length > 0) return
        setNovoFornecedor({ nome, contato: r.contato.trim() })
        setPainelFornecedor(null)
    }

    function cancelarNovoFornecedor() {
        setPainelFornecedor(null)
        setErrosFornecedor({})
    }

    // ---------- Salvar (insumo e fornecedor novos vão na mesma transação) ----------
    async function salvar(e) {
        e.preventDefault()
        if (!prontoParaSalvar || salvando) return
        setSalvando(true)
        setErroGeral('')
        try {
            const nf = form.notaFiscal.trim()
            const resposta = await registrarEntradaComposta({
                lote: {
                    codigoLote: form.codigoLote.trim(),
                    notaFiscal: nf || undefined,
                    dataEntrada: form.dataEntrada,
                    dataValidade: form.dataValidade,
                    quantidade: qtd,
                    precoUnitario: preco,
                },
                ...(novoInsumo
                    ? {
                          novoInsumo: {
                              nome: novoInsumo.nome,
                              categoria: novoInsumo.categoria,
                              unidadeMedida: novoInsumo.unidadeMedida,
                              estoqueMinimo: lerNumero(novoInsumo.estoqueMinimo),
                              diasValidadeAtencao: DIAS_ATENCAO_PADRAO,
                              diasValidadeCritica: DIAS_CRITICA_PADRAO,
                          },
                      }
                    : { insumoId: form.insumoId }),
                ...(novoFornecedor
                    ? { novoFornecedor: { nome: novoFornecedor.nome, contato: novoFornecedor.contato || undefined } }
                    : form.fornecedorId != null && { fornecedorId: form.fornecedorId }),
            })
            const { lote, saldoAtual } = resposta
            // O detalhe do Armário ainda lê a NF guardada no navegador
            guardarNotaFiscal(lote.id, nf)
            setRegistro({
                lote,
                nf,
                unidade: resposta.insumo.unidadeMedida,
                saldoAntes: Math.max(0, saldoAtual - lote.quantidadeInicial),
                saldoDepois: saldoAtual,
                novos: { insumo: Boolean(novoInsumo), fornecedor: Boolean(novoFornecedor) },
            })
            // Os novos passam a existir: entram nas listas para a próxima entrada
            setInsumos((lista) => {
                const existe = lista.some((i) => i.insumoId === lote.insumoId)
                return existe
                    ? lista.map((i) => (i.insumoId === lote.insumoId ? { ...i, saldo: saldoAtual } : i))
                    : [...lista, { insumoId: lote.insumoId, nome: resposta.insumo.nome, categoria: resposta.insumo.categoria, unidadeMedida: resposta.insumo.unidadeMedida, saldo: saldoAtual }]
            })
            if (resposta.fornecedor && novoFornecedor) setFornecedores((lista) => [...lista, resposta.fornecedor])
        } catch (err) {
            const separados = separarErrosApi(err)
            setErrosApi(separados.lote)
            if (Object.keys(separados.insumo).length && novoInsumo) {
                setPainelInsumo({ ...novoInsumo })
                setErrosInsumo(separados.insumo)
            }
            if (Object.keys(separados.fornecedor).length && novoFornecedor) {
                setPainelFornecedor({ ...novoFornecedor })
                setErrosFornecedor(separados.fornecedor)
            }
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
        setNovoInsumo(null)
        setNovoFornecedor(null)
        setPainelInsumo(null)
        setPainelFornecedor(null)
    }

    // ---------- Entrada registrada ----------
    if (registro) {
        const { lote, nf, unidade: un, saldoAntes, saldoDepois, novos } = registro
        const linhas = [
            ['Insumo', `${lote.insumoNome}${novos.insumo ? ' (novo)' : ''}`],
            ['Fornecedor', lote.fornecedorNome ? `${lote.fornecedorNome}${novos.fornecedor ? ' (novo)' : ''}` : '—'],
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
                            <Link to="/armario" state={{ atualizados: [lote.insumoId] }} className="botao-secundario">
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
                        <p className="entrada__dica">O lote entra na fila de uso: o que vence primeiro sai primeiro.</p>
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
    const erroDe = (campo, lista = erros) =>
        lista[campo] && (
            <span className="form-campo__erro" id={`${campo}-erro`}>
                {lista[campo]}
            </span>
        )

    const propsCampo = (campo) => ({
        id: campo,
        value: form[campo],
        onChange: (e) => alterar(campo, e.target.value),
        disabled: salvando,
        'aria-invalid': Boolean(erros[campo]),
        'aria-describedby': erros[campo] ? `${campo}-erro` : `${campo}-dica`,
    })

    const opcoesInsumo = [
        ...insumos.map((i) => ({
            id: i.insumoId,
            nome: i.nome,
            detalhe: `saldo ${formatarQuantidade(i.saldo, i.unidadeMedida)}`,
        })),
        ...(novoInsumo ? [{ id: ID_NOVO, nome: novoInsumo.nome, detalhe: 'novo' }] : []),
    ]
    const opcoesFornecedor = [
        ...fornecedores,
        ...(novoFornecedor ? [{ id: ID_NOVO, nome: novoFornecedor.nome, detalhe: 'novo' }] : []),
    ]

    const escolherInsumo = (o) => {
        if (o?.id === ID_NOVO) return
        setNovoInsumo(null)
        setPainelInsumo(null)
        alterar('insumoId', o?.id ?? null)
    }

    const escolherFornecedor = (o) => {
        if (o?.id === ID_NOVO) return
        setNovoFornecedor(null)
        setPainelFornecedor(null)
        alterar('fornecedorId', o?.id ?? null)
    }

    const resumoLote = [
        form.codigoLote.trim() && `Lote ${form.codigoLote.trim()}`,
        form.notaFiscal.trim() && `nota fiscal ${form.notaFiscal.trim()}`,
    ]
        .filter(Boolean)
        .join(', ')

    const avisoNovos =
        (novoInsumo || painelInsumo) && (novoFornecedor || painelFornecedor)
            ? 'Insumo e fornecedor novos serão cadastrados quando você salvar a entrada.'
            : novoInsumo || painelInsumo
                ? 'O insumo novo é salvo junto com a entrada.'
                : novoFornecedor || painelFornecedor
                    ? 'O fornecedor novo é salvo junto com a entrada.'
                    : null

    const situacao = prontoParaSalvar
        ? { tipo: 'ok', texto: 'Tudo certo para salvar' }
        : Object.keys(erros).length > 0
            ? { tipo: 'critica', texto: 'Confira os campos em vermelho' }
            : painelInsumo || painelFornecedor
                ? { tipo: 'atencao', texto: 'Termine o cadastro aberto' }
                : { tipo: 'atencao', texto: 'Preencha os campos obrigatórios' }

    const dicaInsumo = painelInsumo
        ? <><span className="entrada__novo">Novo</span> Preencha os dados abaixo para cadastrar.</>
        : novoInsumo
            ? <><span className="entrada__novo">Novo</span> {rotuloCategoria(novoInsumo.categoria)}, em {rotuloUnidade(novoInsumo.unidadeMedida)}. Será salvo com a entrada.</>
            : insumo
                ? `${rotuloCategoria(insumo.categoria)}, medido em ${unidade}. Saldo atual: ${formatarQuantidade(insumo.saldo, insumo.unidadeMedida)}${insumo.saldo > 0 ? '' : ' (Armário vazio)'}.`
                : 'Digite para buscar. Se não existir, use "Novo insumo".'

    const dicaFornecedor = painelFornecedor
        ? <><span className="entrada__novo">Novo</span> Preencha os dados abaixo para cadastrar.</>
        : novoFornecedor
            ? <><span className="entrada__novo">Novo</span> Será salvo com a entrada.</>
            : `Opcional. ${fornecedores.length} ${fornecedores.length === 1 ? 'fornecedor cadastrado' : 'fornecedores cadastrados'}.`

    return (
        <section className="entrada">
            <h1 className="titulo-pagina">Entrada de estoque</h1>
            <p className="subtitulo-pagina">Registre o lote que chegou. Campos com * são obrigatórios.</p>

            {erroCarga && (
                <p className="form-erro entrada__aviso" role="alert">
                    {erroCarga}
                </p>
            )}

            {/* Veio de um alerta ou das compras sugeridas: avisa enquanto o insumo escolhido for o mesmo */}
            {state?.origem && insumo && form.insumoId === state.insumoId && (
                <p className="entrada__origem" role="status">
                    Insumo escolhido {state.origem}. Se quiser, troque por outro.
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
                                <button type="button" className="form-campo__acao" onClick={() => abrirNovoInsumo()}>
                                    + Novo insumo
                                </button>
                            </div>
                            <CampoBusca
                                id="insumo"
                                placeholder="Nome do insumo"
                                opcoes={opcoesInsumo}
                                valor={novoInsumo ? ID_NOVO : form.insumoId}
                                aoEscolher={escolherInsumo}
                                criar={{
                                    rotulo: (texto) => `Cadastrar “${texto}” como novo insumo`,
                                    aoCriar: abrirNovoInsumo,
                                }}
                                invalido={Boolean(erros.insumoId)}
                                descritoPor="insumo-dica"
                            />
                            {erroDe('insumoId') || (
                                <span className="form-campo__dica" id="insumo-dica">
                                    {dicaInsumo}
                                </span>
                            )}
                        </div>

                        <div className="form-campo">
                            <div className="form-campo__rotulo">
                                <label htmlFor="fornecedor">Fornecedor</label>
                                <button type="button" className="form-campo__acao" onClick={() => abrirNovoFornecedor()}>
                                    + Novo fornecedor
                                </button>
                            </div>
                            <CampoBusca
                                id="fornecedor"
                                placeholder="Nome do fornecedor"
                                opcoes={opcoesFornecedor}
                                valor={novoFornecedor ? ID_NOVO : form.fornecedorId}
                                aoEscolher={escolherFornecedor}
                                criar={{
                                    rotulo: (texto) => `Cadastrar “${texto}” como novo fornecedor`,
                                    aoCriar: abrirNovoFornecedor,
                                }}
                                descritoPor="fornecedor-dica"
                            />
                            {erroDe('fornecedorId') || (
                                <span className="form-campo__dica" id="fornecedor-dica">
                                    {dicaFornecedor}
                                </span>
                            )}
                        </div>
                    </div>

                    {painelInsumo && (
                        <fieldset className="entrada__painel">
                            <legend className="entrada__painel-titulo">Novo insumo</legend>
                            <div className="form-campo">
                                <label htmlFor="novoInsumoNome">Nome do insumo *</label>
                                <input
                                    id="novoInsumoNome"
                                    type="text"
                                    maxLength={120}
                                    placeholder="Ex.: Lúpulo Cascade"
                                    value={painelInsumo.nome}
                                    onChange={(e) => {
                                        setPainelInsumo((p) => ({ ...p, nome: e.target.value }))
                                        setErrosInsumo((x) => ({ ...x, nome: undefined }))
                                    }}
                                    aria-invalid={Boolean(errosInsumo.nome)}
                                    aria-describedby={errosInsumo.nome ? 'nome-erro' : undefined}
                                    autoFocus
                                />
                                {erroDe('nome', errosInsumo)}
                            </div>
                            <div className="form-linha">
                                <div className="form-campo">
                                    <label htmlFor="novoInsumoCategoria">Categoria *</label>
                                    <select
                                        id="novoInsumoCategoria"
                                        value={painelInsumo.categoria}
                                        onChange={(e) => setPainelInsumo((p) => ({ ...p, categoria: e.target.value }))}
                                    >
                                        {CATEGORIAS.map((c) => (
                                            <option key={c.valor} value={c.valor}>
                                                {c.rotulo}
                                            </option>
                                        ))}
                                    </select>
                                    {erroDe('categoria', errosInsumo)}
                                </div>
                                <div className="form-campo">
                                    <label htmlFor="novoInsumoUnidade">Unidade de medida *</label>
                                    <select
                                        id="novoInsumoUnidade"
                                        value={painelInsumo.unidadeMedida}
                                        onChange={(e) => setPainelInsumo((p) => ({ ...p, unidadeMedida: e.target.value }))}
                                    >
                                        {UNIDADES.map((u) => (
                                            <option key={u.valor} value={u.valor}>
                                                {u.rotulo}
                                            </option>
                                        ))}
                                    </select>
                                    {erroDe('unidadeMedida', errosInsumo)}
                                </div>
                            </div>
                            <div className="form-linha">
                                <div className="form-campo">
                                    <label htmlFor="novoInsumoMinimo">
                                        Estoque mínimo ({rotuloUnidade(painelInsumo.unidadeMedida)}) *
                                    </label>
                                    <input
                                        id="novoInsumoMinimo"
                                        type="text"
                                        inputMode="decimal"
                                        placeholder="0,000"
                                        value={painelInsumo.estoqueMinimo}
                                        onChange={(e) => {
                                            setPainelInsumo((p) => ({ ...p, estoqueMinimo: e.target.value }))
                                            setErrosInsumo((x) => ({ ...x, estoqueMinimo: undefined }))
                                        }}
                                        aria-invalid={Boolean(errosInsumo.estoqueMinimo)}
                                        aria-describedby={errosInsumo.estoqueMinimo ? 'estoqueMinimo-erro' : undefined}
                                    />
                                    {erroDe('estoqueMinimo', errosInsumo)}
                                </div>
                            </div>
                            <p className="form-campo__dica">
                                Os avisos de validade usam o padrão ({DIAS_ATENCAO_PADRAO} e {DIAS_CRITICA_PADRAO} dias). Para
                                ajustar, edite o insumo depois, no Armário.
                            </p>
                            <div className="form-acoes">
                                <button type="button" className="botao-secundario" onClick={cancelarNovoInsumo}>
                                    Cancelar
                                </button>
                                <button type="button" className="botao-primario" onClick={adicionarInsumo}>
                                    Adicionar insumo
                                </button>
                            </div>
                        </fieldset>
                    )}

                    {painelFornecedor && (
                        <fieldset className="entrada__painel">
                            <legend className="entrada__painel-titulo">Novo fornecedor</legend>
                            <div className="form-linha">
                                <div className="form-campo">
                                    <label htmlFor="novoFornecedorNome">Nome *</label>
                                    <input
                                        id="novoFornecedorNome"
                                        type="text"
                                        maxLength={120}
                                        placeholder="Ex.: Hop Brasil"
                                        value={painelFornecedor.nome}
                                        onChange={(e) => {
                                            setPainelFornecedor((p) => ({ ...p, nome: e.target.value }))
                                            setErrosFornecedor((x) => ({ ...x, nome: undefined }))
                                        }}
                                        aria-invalid={Boolean(errosFornecedor.nome)}
                                        aria-describedby={errosFornecedor.nome ? 'nome-erro' : undefined}
                                        autoFocus
                                    />
                                    {erroDe('nome', errosFornecedor)}
                                </div>
                                <div className="form-campo">
                                    <label htmlFor="novoFornecedorContato">Telefone ou e-mail</label>
                                    <input
                                        id="novoFornecedorContato"
                                        type="text"
                                        maxLength={160}
                                        placeholder="Opcional"
                                        value={painelFornecedor.contato}
                                        onChange={(e) => setPainelFornecedor((p) => ({ ...p, contato: e.target.value }))}
                                    />
                                    {erroDe('contato', errosFornecedor)}
                                </div>
                            </div>
                            <div className="form-acoes">
                                <button type="button" className="botao-secundario" onClick={cancelarNovoFornecedor}>
                                    Cancelar
                                </button>
                                <button type="button" className="botao-primario" onClick={adicionarFornecedor}>
                                    Adicionar fornecedor
                                </button>
                            </div>
                        </fieldset>
                    )}

                    <div className="form-linha">
                        <div className="form-campo">
                            <label htmlFor="codigoLote">Código do lote *</label>
                            <input type="text" maxLength={40} placeholder="Ex.: NF1234-A" {...propsCampo('codigoLote')} />
                            {erroDe('codigoLote') || (
                                <span className="form-campo__dica" id="codigoLote-dica">
                                    Está na embalagem ou na nota fiscal.
                                </span>
                            )}
                        </div>
                        <div className="form-campo">
                            <label htmlFor="notaFiscal">Nº da nota fiscal (NF)</label>
                            <input type="text" maxLength={30} placeholder="Ex.: 004512" {...propsCampo('notaFiscal')} />
                            {erroDe('notaFiscal')}
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
                            {erroDe('quantidade') || (
                                <span className="form-campo__dica" id="quantidade-dica">
                                    A unidade vem do insumo.
                                </span>
                            )}
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
                    {resumoLote && <p className="entrada__dica">{resumoLote}</p>}
                    {insumo && (
                        <p className="entrada__dica">
                            Saldo do {insumo.nome} depois da entrada:{' '}
                            {formatarQuantidade(insumo.saldo + qtdValida, insumo.unidadeMedida)}
                        </p>
                    )}
                    {avisoNovos && <p className="entrada__dica">{avisoNovos}</p>}
                    {!temInsumo && !avisoNovos && (
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
