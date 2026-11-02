import { screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { listarArmario } from '../Api/armario.js'
import { ADMIN, definirUsuario, OPERADOR } from '../test/authFalso.jsx'
import { erroApi, renderTela } from '../test/renderTela.jsx'
import Armario from './Armario.jsx'

vi.mock('../Api/armario.js')
vi.mock('../Auth/AuthContext.jsx', () => import('../test/authFalso.jsx'))

const ITENS = [
    { insumoId: 1, nome: 'Malte Pilsen', categoria: 'MALTE', unidadeMedida: 'KG', saldo: 50, estoqueMinimo: 10, statusEscassez: 'OK', cor: 'VERDE', diasParaVencer: 120 },
    { insumoId: 4, nome: 'Lúpulo Cascade', categoria: 'LUPULO', unidadeMedida: 'KG', saldo: 0.5, estoqueMinimo: 1, statusEscassez: 'BAIXO', cor: 'AMARELO', diasParaVencer: 25 },
    { insumoId: 7, nome: 'Levedura US-05', categoria: 'LEVEDURA', unidadeMedida: 'KG', saldo: 0.2, estoqueMinimo: 0.2, statusEscassez: 'OK', cor: 'VERMELHO', diasParaVencer: 6 },
]

describe('Armário', () => {
    beforeEach(() => {
        definirUsuario(OPERADOR)
        listarArmario.mockResolvedValue(ITENS)
    })

    it('mostra os cartões com saldo e situação da validade', async () => {
        renderTela(<Armario />, { rota: '/armario' })
        expect(await screen.findByText('Malte Pilsen')).toBeInTheDocument()
        expect(screen.getByText('Validade Crítica: 6 dias')).toBeInTheDocument()
        expect(screen.getByText('Atenção: vence em 25 dias')).toBeInTheDocument()
        expect(screen.getByText('Atenção: 1 insumo está em Validade Crítica.')).toBeInTheDocument()
        expect(screen.getByRole('link', { name: /Malte Pilsen/ })).toHaveAttribute('href', '/armario/1')
    })

    it('busca sem acento e filtra por categoria', async () => {
        const { user } = renderTela(<Armario />, { rota: '/armario' })
        await screen.findByText('Malte Pilsen')

        await user.type(screen.getByLabelText('Buscar insumo'), 'lupulo')
        expect(screen.getByText('Lúpulo Cascade')).toBeInTheDocument()
        expect(screen.queryByText('Malte Pilsen')).not.toBeInTheDocument()

        await user.clear(screen.getByLabelText('Buscar insumo'))
        await user.click(screen.getByRole('button', { name: 'Levedura' }))
        expect(screen.getByText('Levedura US-05')).toBeInTheDocument()
        expect(screen.queryByText('Lúpulo Cascade')).not.toBeInTheDocument()
    })

    it('"Novo insumo" só para ADMIN', async () => {
        renderTela(<Armario />, { rota: '/armario' })
        await screen.findByText('Malte Pilsen')
        expect(screen.queryByRole('link', { name: 'Novo insumo' })).not.toBeInTheDocument()
    })

    it('ADMIN vê "Novo insumo"', async () => {
        definirUsuario(ADMIN)
        renderTela(<Armario />, { rota: '/armario' })
        expect(await screen.findByRole('link', { name: 'Novo insumo' })).toHaveAttribute('href', '/insumos/novo')
    })

    it('destaca o insumo novo vindo do cadastro', async () => {
        renderTela(<Armario />, { rota: '/armario', state: { novos: [4] } })
        expect(await screen.findByText('Novo')).toBeInTheDocument()
    })

    it('erro ao carregar permite tentar de novo', async () => {
        listarArmario.mockRejectedValueOnce(erroApi(500))
        const { user } = renderTela(<Armario />, { rota: '/armario' })
        await user.click(await screen.findByRole('button', { name: 'Tentar de novo' }))
        expect(await screen.findByText('Malte Pilsen')).toBeInTheDocument()
    })
})
