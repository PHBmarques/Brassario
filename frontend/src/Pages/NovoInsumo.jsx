import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { criarInsumo, registrarEntrada } from '../Api/armario.js'
import { errosPorCampo, mensagemDeErro } from '../Api/erros.js'
import Barra from '../components/Barra.jsx'
import Etiqueta from '../components/Etiqueta.jsx'
import {
    CATEGORIAS,
    UNIDADES,
    corPorDias,
    diasAte,
    formatarNumero,
    formatarQuantidade,
    lerNumero,
    rotuloCategoria,
    rotuloUnidade,
    situacaoDoItem,
} from '../Utils/armario.js'
import { guardarNotaFiscal } from '../Utils/notasFiscais.js'
import '../components/Formulario.css'
import './Armario.css'
import './NovoInsumo.css'

const INICIAL = {
    nome: '',
    categoria: 'MALTE',
    unidadeMedida: 'KG',
    estoqueMinimo: '',
    diasValidadeAtencao: '45',
    diasValidadeCritica: '15',
    codigoLote: '',
    notaFiscal: '',
    quantidade: '',
    dataValidade: '',
}

// Campos da primeira entrada: se algum for preenchido, a entrada passa a ser registrada
const CAMPOS_ENTRADA = ['codigoLote', 'notaFiscal', 'quantidade', 'dataValidade']
const VAZIOS_ENTRADA = Object.fromEntries(CAMPOS_ENTRADA.map((c) => [c, '']))

function validar(f) {
    const erros = {}
    if (!f.nome.trim()) erros.nome = 'Informe o nome do insumo.'

    const minimo = lerNumero(f.estoqueMinimo)
    if (minimo === null) erros.estoqueMinimo = 'Informe o estoque mínimo.'
    else if (Number.isNaN(minimo) || minimo < 0) erros.estoqueMinimo = 'Use um número igual ou maior que zero.'

    const atencao = lerNumero(f.diasValidadeAtencao)
    const critica = lerNumero(f.diasValidadeCritica)
    if (atencao === null || !Number.isInteger(atencao) || atencao < 0) erros.diasValidadeAtencao = 'Dias inteiros.'
    if (critica === null || !Number.isInteger(critica) || critica < 0) erros.diasValidadeCritica = 'Dias inteiros.'
    else if (!erros.diasValidadeAtencao && atencao < critica) erros.diasValidadeAtencao = 'Não pode ser menor que o crítico.'

    if (CAMPOS_ENTRADA.some((c) => f[c].trim())) {
        if (!f.codigoLote.trim()) erros.codigoLote = 'Informe o código do lote.'
        const qtd = lerNumero(f.quantidade)
        if (qtd === null) erros.quantidade = 'Informe a quantidade.'
        else if (Number.isNaN(qtd) || qtd <= 0) erros.quantidade = 'Use um número maior que zero.'
        if (!f.dataValidade) erros.dataValidade = 'Informe a validade.'
        else if (diasAte(f.dataValidade) < 0) erros.dataValidade = 'Esta validade já passou.'
    }
    return erros
}

export default function NovoInsumo() {
    const navigate = useNavigate()
    // Vindo da Entrada de estoque: o nome já chega digitado e, ao salvar, volta para lá com o insumo escolhido
    const { state } = useLocation()
    const daEntrada = state?.origem === 'entrada'
    const voltarPara = daEntrada ? '/entrada' : '/armario'
    const [form, setForm] = useState(() => ({ ...INICIAL, nome: state?.nome ?? '' }))
    const [erros, setErros] = useState({})
    const [erroGeral, setErroGeral] = useState('')
    const [salvando, setSalvando] = useState(false)

    const alterar = (campo) => (e) => {
        setForm((f) => ({ ...f, [campo]: e.target.value }))
        setErros((atual) => (atual[campo] ? { ...atual, [campo]: undefined } : atual))
    }

    const unidade = rotuloUnidade(form.unidadeMedida)
    const temEntrada = !daEntrada && CAMPOS_ENTRADA.some((c) => form[c].trim())

    // ---------- Pré-visualização do cartão ----------
    const minimo = lerNumero(form.estoqueMinimo)
    const minimoValido = Number.isFinite(minimo) ? minimo : 0
    const qtdPrevia = lerNumero(form.quantidade)
    const saldoPrevia = Number.isFinite(qtdPrevia) && qtdPrevia > 0 && form.dataValidade ? qtdPrevia : 0
    const diasPrevia = form.dataValidade ? diasAte(form.dataValidade) : null
    const previa = situacaoDoItem({
        saldo: saldoPrevia,
        statusEscassez: saldoPrevia > 0 ? 'OK' : 'VAZIO',
        cor:
            diasPrevia === null
                ? null
                : corPorDias(diasPrevia, lerNumero(form.diasValidadeCritica) ?? 15, lerNumero(form.diasValidadeAtencao) ?? 45),
        diasParaVencer: diasPrevia,
        estoqueMinimo: minimoValido,
    })

    async function salvar(e) {
        e.preventDefault()
        const encontrados = validar(daEntrada ? { ...form, ...VAZIOS_ENTRADA } : form)
        setErros(encontrados)
        setErroGeral('')
        if (Object.keys(encontrados).length > 0) return

        setSalvando(true)
        let insumo
        try {
            insumo = await criarInsumo({
                nome: form.nome.trim(),
                categoria: form.categoria,
                unidadeMedida: form.unidadeMedida,
                estoqueMinimo: lerNumero(form.estoqueMinimo),
                diasValidadeCritica: lerNumero(form.diasValidadeCritica),
                diasValidadeAtencao: lerNumero(form.diasValidadeAtencao),
            })
        } catch (err) {
            setErros(errosPorCampo(err))
            setErroGeral(mensagemDeErro(err))
            setSalvando(false)
            return
        }

        // O insumo já existe; se a primeira entrada falhar, avisa no Armário em vez de prender o usuário aqui
        let aviso
        if (temEntrada) {
            try {
                const lote = await registrarEntrada({
                    insumoId: insumo.id,
                    codigoLote: form.codigoLote.trim(),
                    quantidade: lerNumero(form.quantidade),
                    dataValidade: form.dataValidade,
                    precoUnitario: 0,
                })
                guardarNotaFiscal(lote.id, form.notaFiscal.trim())
            } catch (err) {
                aviso = `${insumo.nome} foi cadastrado, mas a primeira entrada não foi registrada: ${mensagemDeErro(err)}`
            }
        }

        if (daEntrada) navigate('/entrada', { state: { insumoId: insumo.id } })
        else navigate('/armario', { state: { novos: [insumo.id], aviso } })
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
        'aria-invalid': Boolean(erros[campo]),
        'aria-describedby': erros[campo] ? `${campo}-erro` : undefined,
    })

    return (
        <section className="novo-insumo">
            <h1 className="titulo-pagina">Novo insumo</h1>
            <p className="subtitulo-pagina">Cadastre um ingrediente novo. Campos com * são obrigatórios.</p>

            <div className="novo-insumo__grade">
                <form className="novo-insumo__form form-cartao" onSubmit={salvar} noValidate>
                    {erroGeral && (
                        <p className="form-erro" role="alert">
                            {erroGeral}
                        </p>
                    )}

                    <div className="form-campo">
                        <label htmlFor="nome">Nome do insumo *</label>
                        <input type="text" maxLength={120} placeholder="Ex.: Lúpulo Mosaic" {...propsCampo('nome')} />
                        {erroDe('nome')}
                    </div>

                    <div className="form-linha">
                        <div className="form-campo">
                            <label htmlFor="categoria">Categoria *</label>
                            <select {...propsCampo('categoria')}>
                                {CATEGORIAS.map((c) => (
                                    <option key={c.valor} value={c.valor}>
                                        {c.rotulo}
                                    </option>
                                ))}
                            </select>
                            {erroDe('categoria')}
                        </div>
                        <div className="form-campo">
                            <label htmlFor="unidadeMedida">Unidade de medida *</label>
                            <select {...propsCampo('unidadeMedida')}>
                                {UNIDADES.map((u) => (
                                    <option key={u.valor} value={u.valor}>
                                        {u.rotulo}
                                    </option>
                                ))}
                            </select>
                            {erroDe('unidadeMedida')}
                        </div>
                    </div>

                    <div className="form-linha">
                        <div className="form-campo">
                            <label htmlFor="estoqueMinimo">Estoque mínimo ({unidade}) *</label>
                            <input type="text" inputMode="decimal" placeholder="Ex.: 1,5" {...propsCampo('estoqueMinimo')} />
                            {erroDe('estoqueMinimo') || <span className="form-campo__dica">Abaixo disso o Armário mostra alerta.</span>}
                        </div>
                        <fieldset className="form-campo novo-insumo__dias">
                            <legend>Avisar antes de vencer (dias)</legend>
                            <div className="form-linha">
                                <div className="form-campo">
                                    <input
                                        type="text"
                                        inputMode="numeric"
                                        aria-label="Dias para atenção"
                                        {...propsCampo('diasValidadeAtencao')}
                                    />
                                    {erroDe('diasValidadeAtencao') || <span className="form-campo__dica">Atenção</span>}
                                </div>
                                <div className="form-campo">
                                    <input
                                        type="text"
                                        inputMode="numeric"
                                        aria-label="Dias para crítico"
                                        {...propsCampo('diasValidadeCritica')}
                                    />
                                    {erroDe('diasValidadeCritica') || <span className="form-campo__dica">Crítico</span>}
                                </div>
                            </div>
                        </fieldset>
                    </div>

                    {/* Quem veio da Entrada de estoque registra o lote lá */}
                    {!daEntrada && (
                        <>
                        <hr className="novo-insumo__divisor" />

                        <div>
                            <h2 className="novo-insumo__secao">Primeira entrada (opcional)</h2>
                            <p className="form-campo__dica">
                                Já recebeu este insumo? Registre o lote agora e pule a tela de entrada.
                            </p>
                        </div>

                        <div className="form-linha">
                            <div className="form-campo">
                                <label htmlFor="codigoLote">Código do lote</label>
                                <input type="text" maxLength={60} placeholder="Ex.: MOS-001" {...propsCampo('codigoLote')} />
                                {erroDe('codigoLote')}
                            </div>
                            <div className="form-campo">
                                <label htmlFor="notaFiscal">Nº da nota fiscal (NF)</label>
                                <input type="text" placeholder="Ex.: 004530" {...propsCampo('notaFiscal')} />
                            </div>
                        </div>

                        <div className="form-linha">
                            <div className="form-campo">
                                <label htmlFor="quantidade">Quantidade ({unidade})</label>
                                <input type="text" inputMode="decimal" placeholder="Ex.: 2" {...propsCampo('quantidade')} />
                                {erroDe('quantidade')}
                            </div>
                            <div className="form-campo">
                                <label htmlFor="dataValidade">Validade</label>
                                <input type="date" {...propsCampo('dataValidade')} />
                                {erroDe('dataValidade')}
                            </div>
                        </div>
                        </>
                    )}

                    <div className="form-acoes">
                        <Link to={voltarPara} className="botao-secundario">
                            Cancelar
                        </Link>
                        <button type="submit" className="botao-primario" disabled={salvando}>
                            {salvando ? 'Salvando…' : 'Salvar insumo'}
                        </button>
                    </div>
                </form>

                <aside className="novo-insumo__previa form-cartao" aria-label="Pré-visualização">
                    <h2 className="novo-insumo__secao">Como vai aparecer no Armário</h2>
                    <div className="cartao-insumo">
                        <span className="cartao-insumo__topo">
                            <span className="cartao-insumo__nome">
                                <strong>{form.nome.trim() || 'Nome do insumo'}</strong>
                            </span>
                            <strong>{formatarQuantidade(saldoPrevia, form.unidadeMedida)}</strong>
                        </span>
                        <Barra nivel={previa.nivel} status={previa.barra} rotulo="Nível do novo insumo" />
                        <Etiqueta tipo={previa.etiqueta}>{previa.texto}</Etiqueta>
                    </div>
                    <p className="form-campo__dica">
                        Categoria: {rotuloCategoria(form.categoria)}. Unidade: {unidade}. Mínimo:{' '}
                        {formatarNumero(minimoValido)} {unidade}.
                    </p>
                </aside>
            </div>
        </section>
    )
}
