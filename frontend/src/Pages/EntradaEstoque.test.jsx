import { screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { listarArmario } from '../Api/armario.js'
import { registrarEntradaComposta } from '../Api/entradas.js'
import { listarFornecedores } from '../Api/fornecedores.js'
import { definirUsuario, OPERADOR } from '../test/authFalso.jsx'
import { erroApi, renderTela } from '../test/renderTela.jsx'
import { hojeIso } from '../Utils/armario.js'
import EntradaEstoque from './EntradaEstoque.jsx'

vi.mock('../Api/armario.js')
vi.mock('../Api/entradas.js')
vi.mock('../Api/fornecedores.js')
vi.mock('../Auth/AuthContext.jsx', () => import('../test/authFalso.jsx'))

const INSUMOS = [
    { insumoId: 1, nome: 'Malte Munich', categoria: 'MALTE', unidadeMedida: 'KG', saldo: 0 },
    { insumoId: 4, nome: 'Lúpulo Cascade', categoria: 'LUPULO', unidadeMedida: 'KG', saldo: 1.2 },
]
const FORNECEDORES = [
    { id: 1, nome: 'Hop Brasil', ativo: true },
    { id: 2, nome: 'Maltaria Sul', ativo: true },
]

// Validade daqui a um ano
const VALIDADE = `${Number(hojeIso().slice(0, 4)) + 1}${hojeIso().slice(4)}`

async function abrir(state) {
    const r = renderTela(<EntradaEstoque />, { rota: '/entrada', state })
    await screen.findByText('Opcional. 2 fornecedores cadastrados.')
    return r
}

async function preencherLote(user) {
    await user.type(screen.getByLabelText('Código do lote *'), 'MOS-001')
    await user.type(screen.getByLabelText('Nº da nota fiscal (NF)'), '004512')
    await user.type(screen.getByLabelText('Validade *'), VALIDADE)
    await user.type(screen.getByLabelText(/^Quantidade/), '2')
    await user.type(screen.getByLabelText(/^Preço por/), '10,5')
}

const RESPOSTA = {
    lote: {
        id: 50,
        insumoId: 99,
        insumoNome: 'Lúpulo Mosaic',
        fornecedorNome: 'Hop Brasil',
        codigoLote: 'MOS-001',
        dataEntrada: hojeIso(),
        dataValidade: VALIDADE,
        quantidadeInicial: 2,
        precoUnitario: 10.5,
        diasParaVencer: 365,
        situacaoValidade: 'VERDE',
    },
    insumo: { id: 99, nome: 'Lúpulo Mosaic', categoria: 'LUPULO', unidadeMedida: 'KG' },
    fornecedor: null,
    saldoAtual: 2,
}

describe('Entrada de estoque', () => {
    beforeEach(() => {
        definirUsuario(OPERADOR)
        listarArmario.mockResolvedValue(INSUMOS)
        listarFornecedores.mockResolvedValue(FORNECEDORES)
        registrarEntradaComposta.mockResolvedValue(RESPOSTA)
    })

    it('"Salvar entrada" só libera com os campos obrigatórios', async () => {
        const { user } = await abrir()
        const salvar = screen.getByRole('button', { name: 'Salvar entrada' })
        expect(salvar).toBeDisabled()
        expect(screen.getByText('Preencha os campos obrigatórios')).toBeInTheDocument()

        await user.type(screen.getByLabelText('Insumo *'), 'Cascade')
        await user.click(await screen.findByRole('option', { name: /Lúpulo Cascade/ }))
        await preencherLote(user)

        expect(salvar).toBeEnabled()
        expect(screen.getByText('Tudo certo para salvar')).toBeInTheDocument()
        expect(screen.getByText('R$ 21,00')).toBeInTheDocument()
    })

    it('registra a entrada de um insumo existente', async () => {
        registrarEntradaComposta.mockResolvedValue({
            ...RESPOSTA,
            lote: { ...RESPOSTA.lote, insumoId: 4, insumoNome: 'Lúpulo Cascade' },
            insumo: { id: 4, nome: 'Lúpulo Cascade', categoria: 'LUPULO', unidadeMedida: 'KG' },
            saldoAtual: 3.2,
        })
        const { user } = await abrir()
        await user.type(screen.getByLabelText('Insumo *'), 'Cascade')
        await user.click(await screen.findByRole('option', { name: /Lúpulo Cascade/ }))
        await preencherLote(user)
        await user.click(screen.getByRole('button', { name: 'Salvar entrada' }))

        expect(registrarEntradaComposta).toHaveBeenCalledWith({
            lote: {
                codigoLote: 'MOS-001',
                notaFiscal: '004512',
                dataEntrada: hojeIso(),
                dataValidade: VALIDADE,
                quantidade: 2,
                precoUnitario: 10.5,
            },
            insumoId: 4,
        })
        expect(await screen.findByText(/Entrada registrada/)).toHaveTextContent('3,2 kg')
    })

    it('novo insumo: não aceita nome que já existe', async () => {
        const { user } = await abrir()
        await user.click(screen.getByRole('button', { name: '+ Novo insumo' }))
        expect(screen.getByText('Preencha os dados abaixo para cadastrar.', { exact: false })).toBeInTheDocument()

        await user.type(screen.getByLabelText('Nome do insumo *'), 'lupulo cascade')
        await user.type(screen.getByLabelText(/^Estoque mínimo/), '1,5')
        await user.click(screen.getByRole('button', { name: 'Adicionar insumo' }))

        expect(screen.getByText('Já existe um insumo com esse nome. Escolha-o na lista.')).toBeInTheDocument()
    })

    it('insumo e fornecedor novos vão junto com a entrada', async () => {
        const { user } = await abrir()

        await user.click(screen.getByRole('button', { name: '+ Novo insumo' }))
        await user.type(screen.getByLabelText('Nome do insumo *'), 'Lúpulo Mosaic')
        await user.selectOptions(screen.getByLabelText('Categoria *'), 'LUPULO')
        await user.type(screen.getByLabelText(/^Estoque mínimo/), '1')
        await user.click(screen.getByRole('button', { name: 'Adicionar insumo' }))
        expect(screen.getByText(/Lúpulo, em kg. Será salvo com a entrada./)).toBeInTheDocument()

        await user.click(screen.getByRole('button', { name: '+ Novo fornecedor' }))
        await user.type(screen.getByLabelText('Nome *'), 'Lúpulos do Sul')
        await user.type(screen.getByLabelText('Telefone ou e-mail'), 'vendas@lupulos.com')
        await user.click(screen.getByRole('button', { name: 'Adicionar fornecedor' }))
        expect(screen.getByText('Insumo e fornecedor novos serão cadastrados quando você salvar a entrada.')).toBeInTheDocument()

        await preencherLote(user)
        await user.click(screen.getByRole('button', { name: 'Salvar entrada' }))

        expect(registrarEntradaComposta).toHaveBeenCalledWith(
            expect.objectContaining({
                novoInsumo: {
                    nome: 'Lúpulo Mosaic',
                    categoria: 'LUPULO',
                    unidadeMedida: 'KG',
                    estoqueMinimo: 1,
                    diasValidadeAtencao: 45,
                    diasValidadeCritica: 15,
                },
                novoFornecedor: { nome: 'Lúpulos do Sul', contato: 'vendas@lupulos.com' },
            })
        )
        const chamada = registrarEntradaComposta.mock.calls[0][0]
        expect(chamada).not.toHaveProperty('insumoId')
        expect(await screen.findByText('Lúpulo Mosaic (novo)')).toBeInTheDocument()
    })

    it('com o painel de novo insumo aberto, não dá para salvar', async () => {
        const { user } = await abrir()
        await user.type(screen.getByLabelText('Insumo *'), 'Cascade')
        await user.click(await screen.findByRole('option', { name: /Lúpulo Cascade/ }))
        await preencherLote(user)
        await user.click(screen.getByRole('button', { name: '+ Novo insumo' }))

        expect(screen.getByRole('button', { name: 'Salvar entrada' })).toBeDisabled()
        expect(screen.getByText('Termine o cadastro aberto')).toBeInTheDocument()
    })

    it('vindo de um alerta, o insumo já vem escolhido com o aviso', async () => {
        await abrir({ insumoId: 1, origem: 'pelo alerta Armário vazio' })
        expect(screen.getByRole('status')).toHaveTextContent(
            'Insumo escolhido pelo alerta Armário vazio. Se quiser, troque por outro.'
        )
        expect(screen.getByLabelText('Insumo *')).toHaveValue('Malte Munich')
        expect(screen.getByText(/Saldo atual: 0 kg \(Armário vazio\)/)).toBeInTheDocument()
    })

    it('erros por campo da API aparecem no lugar certo', async () => {
        registrarEntradaComposta.mockRejectedValue(
            erroApi(409, {
                mensagem: 'Já existe um lote com esse código para o insumo.',
                detalhes: [{ campo: 'lote.codigoLote', erro: 'Código de lote repetido.' }],
            })
        )
        const { user } = await abrir()
        await user.type(screen.getByLabelText('Insumo *'), 'Cascade')
        await user.click(await screen.findByRole('option', { name: /Lúpulo Cascade/ }))
        await preencherLote(user)
        await user.click(screen.getByRole('button', { name: 'Salvar entrada' }))

        expect(await screen.findByText('Código de lote repetido.')).toBeInTheDocument()
        expect(screen.getByRole('alert')).toHaveTextContent('Já existe um lote com esse código')
    })
})
