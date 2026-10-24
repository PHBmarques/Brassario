import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { listarArmario } from '../Api/armario.js'
import { errosPorCampo, mensagemDeErro } from '../Api/erros.js'
import { atualizarReceita, criarReceita, obterReceita } from '../Api/receitas.js'
import { lerNumero, rotuloUnidade } from '../Utils/armario.js'
import '../components/Formulario.css'
import './Receitas.css'

const itemVazio = () => ({ chave: crypto.randomUUID(), insumoId: '', quantidade: '' })

function validar(f) {
    const erros = {}
    if (!f.nome.trim()) erros.nome = 'Informe o nome da receita.'
    const volume = lerNumero(f.volumeBaseLitros)
    if (volume === null) erros.volumeBaseLitros = 'Informe o volume base.'
    else if (!(volume > 0)) erros.volumeBaseLitros = 'Use um número maior que zero.'

    const itens = {}
    const usados = new Set()
    f.itens.forEach((item) => {
        const e = {}
        if (!item.insumoId) e.insumoId = 'Escolha o insumo.'
        else if (usados.has(item.insumoId)) e.insumoId = 'Este insumo já está na receita.'
        usados.add(item.insumoId)
        const qtd = lerNumero(item.quantidade)
        if (qtd === null) e.quantidade = 'Informe a quantidade.'
        else if (!(qtd > 0)) e.quantidade = 'Maior que zero.'
        if (Object.keys(e).length) itens[item.chave] = e
    })
    if (Object.keys(itens).length) erros.itens = itens
    if (f.itens.length === 0) erros.semItens = 'A receita precisa ter pelo menos 1 ingrediente.'
    return erros
}

// Nova receita (/receitas/nova) e edição (/receitas/:id/editar). Somente ADMIN.
export default function ReceitaForm() {
    const { id } = useParams()
    const editando = Boolean(id)
    const navigate = useNavigate()

    const [insumos, setInsumos] = useState([])
    const [form, setForm] = useState({ nome: '', estilo: '', volumeBaseLitros: '', modoPreparo: '', itens: [itemVazio()] })
    const [carregando, setCarregando] = useState(true)
    const [erros, setErros] = useState({})
    const [erroGeral, setErroGeral] = useState('')
    const [salvando, setSalvando] = useState(false)

    useEffect(() => {
        let ativo = true
        Promise.all([listarArmario(), editando ? obterReceita(id) : null])
            .then(([lista, receita]) => {
                if (!ativo) return
                setInsumos([...lista].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR')))
                if (receita) {
                    setForm({
                        nome: receita.nome,
                        estilo: receita.estilo ?? '',
                        volumeBaseLitros: String(receita.volumeBaseLitros).replace('.', ','),
                        modoPreparo: receita.modoPreparo ?? '',
                        itens: receita.itens.map((i) => ({
                            chave: crypto.randomUUID(),
                            insumoId: String(i.insumoId),
                            quantidade: String(i.quantidade).replace('.', ','),
                            // Insumo inativo não aparece no Armário: guarda o nome para mostrar na lista
                            nome: i.insumoNome,
                            unidadeMedida: i.unidadeMedida,
                        })),
                    })
                }
            })
            .catch((e) => ativo && setErroGeral(mensagemDeErro(e)))
            .finally(() => ativo && setCarregando(false))
        return () => {
            ativo = false
        }
    }, [id, editando])

    const alterar = (campo) => (e) => {
        setForm((f) => ({ ...f, [campo]: e.target.value }))
        setErros((x) => (x[campo] ? { ...x, [campo]: undefined } : x))
    }

    const alterarItem = (chave, campo, valor) =>
        setForm((f) => ({ ...f, itens: f.itens.map((i) => (i.chave === chave ? { ...i, [campo]: valor } : i)) }))

    const removerItem = (chave) => setForm((f) => ({ ...f, itens: f.itens.filter((i) => i.chave !== chave) }))

    async function salvar(e) {
        e.preventDefault()
        const encontrados = validar(form)
        setErros(encontrados)
        setErroGeral('')
        if (Object.keys(encontrados).length > 0) return

        const dados = {
            nome: form.nome.trim(),
            estilo: form.estilo.trim() || undefined,
            volumeBaseLitros: lerNumero(form.volumeBaseLitros),
            modoPreparo: form.modoPreparo.trim() || undefined,
            itens: form.itens.map((i) => ({ insumoId: Number(i.insumoId), quantidade: lerNumero(i.quantidade) })),
        }

        setSalvando(true)
        try {
            const salva = editando ? await atualizarReceita(id, dados) : await criarReceita(dados)
            navigate(`/receitas/${salva.id}`)
        } catch (err) {
            setErros(errosPorCampo(err))
            setErroGeral(mensagemDeErro(err))
            setSalvando(false)
        }
    }

    const erroDe = (campo) =>
        erros[campo] && (
            <span className="form-campo__erro" id={`${campo}-erro`}>
                {erros[campo]}
            </span>
        )

    const propsCampo = (campo) => ({
        id: campo,
        value: form[campo],
        onChange: alterar(campo),
        disabled: salvando,
        'aria-invalid': Boolean(erros[campo]),
        'aria-describedby': erros[campo] ? `${campo}-erro` : undefined,
    })

    if (carregando) {
        return (
            <p className="form-campo__dica" role="status">
                Carregando…
            </p>
        )
    }

    return (
        <section className="ficha">
            <h1 className="titulo-pagina">{editando ? `Editar ficha: ${form.nome}` : 'Nova receita'}</h1>
            <p className="subtitulo-pagina">
                As quantidades são para o volume base; na produção, elas são escaladas. Campos com * são obrigatórios.
            </p>

            <form className="form-cartao receita-form" onSubmit={salvar} noValidate>
                {erroGeral && (
                    <p className="form-erro" role="alert">
                        {erroGeral}
                    </p>
                )}

                <div className="form-linha">
                    <div className="form-campo">
                        <label htmlFor="nome">Nome *</label>
                        <input type="text" maxLength={120} placeholder="Ex.: IPA Cascade" {...propsCampo('nome')} />
                        {erroDe('nome')}
                    </div>
                    <div className="form-campo">
                        <label htmlFor="estilo">Estilo</label>
                        <input type="text" maxLength={80} placeholder="Ex.: India Pale Ale" {...propsCampo('estilo')} />
                        {erroDe('estilo')}
                    </div>
                </div>

                <div className="form-linha">
                    <div className="form-campo">
                        <label htmlFor="volumeBaseLitros">Volume base (L) *</label>
                        <input type="text" inputMode="decimal" placeholder="Ex.: 100" {...propsCampo('volumeBaseLitros')} />
                        {erroDe('volumeBaseLitros')}
                    </div>
                </div>

                <fieldset className="receita-form__itens">
                    <legend className="receitas__secao">Ingredientes *</legend>
                    {form.itens.map((item, n) => {
                        const escolhido = insumos.find((i) => String(i.insumoId) === item.insumoId)
                        const unidade = escolhido?.unidadeMedida ?? item.unidadeMedida
                        const e = erros.itens?.[item.chave] ?? {}
                        const semNaLista = item.insumoId && !escolhido
                        return (
                            <div key={item.chave} className="receita-form__item">
                                <div className="form-campo">
                                    <label htmlFor={`insumo-${item.chave}`}>Insumo {n + 1}</label>
                                    <select
                                        id={`insumo-${item.chave}`}
                                        value={item.insumoId}
                                        onChange={(ev) => alterarItem(item.chave, 'insumoId', ev.target.value)}
                                        aria-invalid={Boolean(e.insumoId)}
                                        disabled={salvando}
                                    >
                                        <option value="">Escolha…</option>
                                        {semNaLista && <option value={item.insumoId}>{item.nome} (inativo)</option>}
                                        {insumos.map((i) => (
                                            <option key={i.insumoId} value={String(i.insumoId)}>
                                                {i.nome}
                                            </option>
                                        ))}
                                    </select>
                                    {e.insumoId && <span className="form-campo__erro">{e.insumoId}</span>}
                                </div>
                                <div className="form-campo">
                                    <label htmlFor={`qtd-${item.chave}`}>
                                        Quantidade{unidade ? ` (${rotuloUnidade(unidade)})` : ''}
                                    </label>
                                    <input
                                        id={`qtd-${item.chave}`}
                                        type="text"
                                        inputMode="decimal"
                                        placeholder="0,000"
                                        value={item.quantidade}
                                        onChange={(ev) => alterarItem(item.chave, 'quantidade', ev.target.value)}
                                        aria-invalid={Boolean(e.quantidade)}
                                        disabled={salvando}
                                    />
                                    {e.quantidade && <span className="form-campo__erro">{e.quantidade}</span>}
                                </div>
                                <button
                                    type="button"
                                    className="receita-form__remover"
                                    onClick={() => removerItem(item.chave)}
                                    aria-label={`Remover ingrediente ${n + 1}`}
                                    title="Remover"
                                    disabled={salvando}
                                >
                                    ×
                                </button>
                            </div>
                        )
                    })}
                    {erros.semItens && <span className="form-campo__erro">{erros.semItens}</span>}
                    <div>
                        <button
                            type="button"
                            className="form-campo__acao"
                            onClick={() => setForm((f) => ({ ...f, itens: [...f.itens, itemVazio()] }))}
                            disabled={salvando}
                        >
                            + Adicionar ingrediente
                        </button>
                    </div>
                </fieldset>

                <div className="form-campo">
                    <label htmlFor="modoPreparo">Modo de preparo</label>
                    <textarea rows={6} maxLength={5000} {...propsCampo('modoPreparo')} />
                    {erroDe('modoPreparo')}
                </div>

                <div className="form-acoes">
                    <Link to={editando ? `/receitas/${id}` : '/receitas'} className="botao-secundario">
                        Cancelar
                    </Link>
                    <button type="submit" className="botao-primario" disabled={salvando}>
                        {salvando ? 'Salvando…' : editando ? 'Salvar ficha' : 'Cadastrar receita'}
                    </button>
                </div>
            </form>
        </section>
    )
}
