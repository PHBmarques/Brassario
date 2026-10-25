import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Home from '../Pages/Home.jsx'

// Funções simples (sem vi.fn) para poder simular falhas sem o executor tratá-las como "não capturadas"
const api = vi.hoisted(() => ({ alertas: null, consumo: null, previsao: null, resumo: null, bateladas: null, detalhe: null }))
vi.mock('../Api/alertas.js', () => ({ listarAlertas: () => api.alertas() }))
vi.mock('../Api/dashboard.js', () => ({
    resumoDashboard: () => api.resumo(),
    consumoDashboard: () => api.consumo(),
    previsaoDashboard: () => api.previsao(),
}))
vi.mock('../Api/bateladas.js', () => ({
    listarBateladas: (filtros) => api.bateladas(filtros),
    obterBatelada: (id) => api.detalhe(id),
}))
vi.mock('../Auth/AuthContext.jsx', () => ({ useAuth: () => ({ usuario: { nome: 'Pedro Marques' } }) }))

const ok = (valor) => () => Promise.resolve(valor)
const falha = (status, mensagem) => () =>
    new Promise((_, rejeita) =>
        setTimeout(() => rejeita(Object.assign(new Error(`HTTP ${status}`), { response: { status, data: { mensagem } } })), 0)
    )

const alerta = (extra) => ({
    tipo: 'ESTOQUE_BAIXO', severidade: 'MEDIA', insumoId: 1, insumoNome: 'Lúpulo Saaz', loteId: null,
    saldo: 0.5, estoqueMinimo: 1, unidadeMedida: 'KG', ...extra,
})

const abrir = () => render(<MemoryRouter><Home /></MemoryRouter>)

describe('Início (dashboard)', () => {
    beforeEach(() => {
        api.alertas = ok([])
        api.consumo = ok([])
        api.previsao = ok([])
        api.resumo = ok({ insumosAtivos: 8, insumosSemEstoque: 1, insumosAbaixoDoMinimo: 2, insumosParaCompra: 3 })
        api.bateladas = ({ status }) => Promise.resolve(
            status === 'FINALIZADA'
                ? { itens: [{ id: 5, quantidade: 100, custoTotal: 400 }], total: 1 }
                : { itens: [], total: 2 }
        )
        api.detalhe = ok({ rendimentoPercentual: 92 })
    })

    it('cumprimenta pelo primeiro nome', async () => {
        abrir()
        expect(await screen.findByText(/, Pedro\./)).toBeInTheDocument()
    })

    it('mostra os alertas reais, no máximo 3, com link para ver todos', async () => {
        api.alertas = ok([
            alerta({ insumoId: 1, insumoNome: 'Levedura US-05', tipo: 'VALIDADE_CRITICA', severidade: 'CRITICA', loteId: 9, codigoLote: 'L-88', diasParaVencer: 6, saldo: 2 }),
            alerta({ insumoId: 2, insumoNome: 'Lúpulo Cascade' }),
            alerta({ insumoId: 3, insumoNome: 'Malte Pilsen' }),
            alerta({ insumoId: 4, insumoNome: 'Malte Munich' }),
        ])
        abrir()
        expect(await screen.findByText('Levedura US-05')).toBeInTheDocument()
        expect(screen.getByText('Validade crítica')).toBeInTheDocument()
        expect(screen.getByText(/Lote L-88, vence em 6 dias/)).toBeInTheDocument()
        expect(screen.queryByText('Malte Munich')).not.toBeInTheDocument()
        expect(screen.getByRole('button', { name: 'Ver todos os 4 alertas' })).toBeInTheDocument()
    })

    it('sem alertas mostra que está tudo certo', async () => {
        abrir()
        expect(await screen.findByText(/Nenhum alerta agora/)).toBeInTheDocument()
    })

    it('mostra os indicadores do mês calculados da API', async () => {
        api.alertas = ok([alerta({ insumoId: 1 }), alerta({ insumoId: 1, tipo: 'ATENCAO' }), alerta({ insumoId: 2 })])
        abrir()
        const cartao = (rotulo) => screen.getByText(rotulo).closest('.indicador')
        expect(await within(cartao('Produções no mês')).findByText('3')).toBeInTheDocument() // 1 finalizada + 2 em andamento
        expect(await within(cartao('Insumos em alerta')).findByText('2')).toBeInTheDocument()
        expect(within(cartao('Custo médio por litro')).getByText(/R\$\s4,00/)).toBeInTheDocument()
        expect(within(cartao('Rendimento médio')).getByText('92%')).toBeInTheDocument()
    })

    it('mostra o resumo do estoque e os litros produzidos', async () => {
        abrir()
        const cartao = (rotulo) => screen.getByText(rotulo).closest('.indicador')
        expect(await within(cartao('Insumos ativos')).findByText('8')).toBeInTheDocument()
        expect(within(cartao('Sem estoque')).getByText('1')).toBeInTheDocument()
        expect(within(cartao('Para comprar')).getByText('3')).toBeInTheDocument()
        expect(await within(cartao('Litros produzidos')).findByText(/100/)).toBeInTheDocument()
    })

    it('sem produção finalizada mostra traço no custo e no rendimento', async () => {
        api.bateladas = () => Promise.resolve({ itens: [], total: 0 })
        abrir()
        const cartao = (rotulo) => screen.getByText(rotulo).closest('.indicador')
        expect(await within(cartao('Produções no mês')).findByText('0')).toBeInTheDocument()
        expect(within(cartao('Custo médio por litro')).getByText('—')).toBeInTheDocument()
        expect(within(cartao('Rendimento médio')).getByText('—')).toBeInTheDocument()
    })

    it('mostra o consumo por categoria e a duração do estoque', async () => {
        api.consumo = ok([{ categoria: 'MALTE', unidadeMedida: 'KG', consumoUltimos30Dias: 180 }])
        api.previsao = ok([{ insumoId: 1, insumoNome: 'Levedura US-05', diasRestantes: 2.5, saldoUtilizavel: 0.5, unidadeMedida: 'KG' }])
        abrir()
        expect(await screen.findByRole('img', { name: /Consumo por categoria/ })).toBeInTheDocument()
        expect(screen.getByText('Malte')).toBeInTheDocument()
        expect(screen.getByText('180 kg')).toBeInTheDocument() // valor com a unidade, na barra
        expect(await screen.findByRole('progressbar', { name: 'Duração de Levedura US-05' })).toBeInTheDocument()
        expect(screen.getByText('2 dias')).toBeInTheDocument()
    })

    it('sem consumo explica que não dá para estimar', async () => {
        abrir()
        expect(await screen.findByText(/Nenhuma produção nos últimos 30 dias/)).toBeInTheDocument()
        expect(await screen.findByText(/Sem consumo nos últimos 30 dias para estimar/)).toBeInTheDocument()
    })

    it('se um bloco falhar, os outros continuam aparecendo', async () => {
        api.consumo = falha(500, 'erro')
        api.alertas = ok([alerta({ insumoId: 1, insumoNome: 'Lúpulo Saaz' })])
        abrir()
        expect(await screen.findByText('Lúpulo Saaz')).toBeInTheDocument()
        expect(await screen.findByRole('alert')).toHaveTextContent('Ocorreu um erro no servidor')
    })

    it('tem o link para as compras sugeridas', async () => {
        abrir()
        expect(await screen.findByRole('link', { name: 'Ver compras sugeridas' })).toHaveAttribute('href', '/compras')
    })
})
