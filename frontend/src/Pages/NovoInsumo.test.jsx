import { screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { criarInsumo, registrarEntrada } from '../Api/armario.js'
import { erroApi, renderTela, stateDoDestino } from '../test/renderTela.jsx'
import NovoInsumo from './NovoInsumo.jsx'

vi.mock('../Api/armario.js')

const abrir = (state) => renderTela(<NovoInsumo />, { rota: '/insumos/novo', state })

describe('Novo insumo', () => {
    it('valida o nome e o estoque mínimo', async () => {
        const { user } = abrir()
        await user.click(screen.getByRole('button', { name: 'Salvar insumo' }))

        expect(screen.getByText('Informe o nome do insumo.')).toBeInTheDocument()
        expect(screen.getByText('Informe o estoque mínimo.')).toBeInTheDocument()
        expect(criarInsumo).not.toHaveBeenCalled()
    })

    it('o aviso de atenção não pode ser menor que o crítico', async () => {
        const { user } = abrir()
        await user.clear(screen.getByLabelText('Dias para atenção'))
        await user.type(screen.getByLabelText('Dias para atenção'), '10')
        await user.click(screen.getByRole('button', { name: 'Salvar insumo' }))
        expect(screen.getByText('Não pode ser menor que o crítico.')).toBeInTheDocument()
    })

    it('a pré-visualização acompanha o que é digitado', async () => {
        const { user } = abrir()
        await user.type(screen.getByLabelText('Nome do insumo *'), 'Lúpulo Mosaic')
        expect(screen.getAllByText('Lúpulo Mosaic').length).toBeGreaterThan(0)
        expect(screen.getByText('Armário vazio')).toBeInTheDocument()
    })

    it('cadastra e volta ao Armário com o destaque do novo', async () => {
        criarInsumo.mockResolvedValue({ id: 99, nome: 'Lúpulo Mosaic' })
        const { user } = abrir()
        await user.type(screen.getByLabelText('Nome do insumo *'), 'Lúpulo Mosaic')
        await user.selectOptions(screen.getByLabelText('Categoria *'), 'LUPULO')
        await user.type(screen.getByLabelText(/^Estoque mínimo/), '1,5')
        await user.click(screen.getByRole('button', { name: 'Salvar insumo' }))

        expect(criarInsumo).toHaveBeenCalledWith({
            nome: 'Lúpulo Mosaic',
            categoria: 'LUPULO',
            unidadeMedida: 'KG',
            estoqueMinimo: 1.5,
            diasValidadeCritica: 15,
            diasValidadeAtencao: 45,
        })
        expect(registrarEntrada).not.toHaveBeenCalled()
        const destino = await screen.findByTestId('destino')
        expect(destino).toHaveTextContent('/armario')
        expect(stateDoDestino(destino).novos).toEqual([99])
    })

    it('nome repetido (409) aparece no formulário', async () => {
        criarInsumo.mockRejectedValue(erroApi(409, { mensagem: 'Já existe um insumo com esse nome.' }))
        const { user } = abrir()
        await user.type(screen.getByLabelText('Nome do insumo *'), 'Malte Pilsen')
        await user.type(screen.getByLabelText(/^Estoque mínimo/), '10')
        await user.click(screen.getByRole('button', { name: 'Salvar insumo' }))

        expect(await screen.findByRole('alert')).toHaveTextContent('Já existe um insumo com esse nome.')
    })
})
