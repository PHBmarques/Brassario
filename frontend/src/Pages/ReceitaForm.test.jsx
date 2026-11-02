import { screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { listarArmario } from '../Api/armario.js'
import { atualizarReceita, criarReceita, obterReceita } from '../Api/receitas.js'
import { erroApi, renderTela } from '../test/renderTela.jsx'
import ReceitaForm from './ReceitaForm.jsx'

vi.mock('../Api/armario.js')
vi.mock('../Api/receitas.js')

const INSUMOS = [
    { insumoId: 1, nome: 'Malte Pilsen', unidadeMedida: 'KG' },
    { insumoId: 4, nome: 'Lúpulo Cascade', unidadeMedida: 'KG' },
]

async function abrirNova() {
    const r = renderTela(<ReceitaForm />, { rota: '/receitas/nova' })
    await screen.findByRole('heading', { name: 'Nova receita' })
    return r
}

describe('Nova receita / Editar ficha', () => {
    beforeEach(() => listarArmario.mockResolvedValue(INSUMOS))

    it('valida nome, volume e ingredientes', async () => {
        const { user } = await abrirNova()
        await user.click(screen.getByRole('button', { name: 'Cadastrar receita' }))

        expect(screen.getByText('Informe o nome da receita.')).toBeInTheDocument()
        expect(screen.getByText('Informe o volume base.')).toBeInTheDocument()
        expect(screen.getByText('Escolha o insumo.')).toBeInTheDocument()
        expect(screen.getByText('Informe a quantidade.')).toBeInTheDocument()
        expect(criarReceita).not.toHaveBeenCalled()
    })

    it('não deixa o mesmo insumo duas vezes', async () => {
        const { user } = await abrirNova()
        await user.click(screen.getByRole('button', { name: '+ Adicionar ingrediente' }))
        await user.selectOptions(screen.getByLabelText('Insumo 1'), '1')
        await user.selectOptions(screen.getByLabelText('Insumo 2'), '1')
        await user.click(screen.getByRole('button', { name: 'Cadastrar receita' }))

        expect(screen.getByText('Este insumo já está na receita.')).toBeInTheDocument()
    })

    it('a receita precisa de pelo menos um ingrediente', async () => {
        const { user } = await abrirNova()
        await user.click(screen.getByRole('button', { name: 'Remover ingrediente 1' }))
        await user.click(screen.getByRole('button', { name: 'Cadastrar receita' }))
        expect(screen.getByText('A receita precisa ter pelo menos 1 ingrediente.')).toBeInTheDocument()
    })

    it('cadastra e abre a ficha nova', async () => {
        criarReceita.mockResolvedValue({ id: 7 })
        const { user } = await abrirNova()
        await user.type(screen.getByLabelText('Nome *'), 'Pilsen Clássica')
        await user.type(screen.getByLabelText('Estilo'), 'Pilsner')
        await user.type(screen.getByLabelText('Volume base (L) *'), '200')
        await user.selectOptions(screen.getByLabelText('Insumo 1'), '1')
        expect(screen.getByLabelText('Quantidade (kg)')).toBeInTheDocument()
        await user.type(screen.getByLabelText('Quantidade (kg)'), '36,5')
        await user.type(screen.getByLabelText('Modo de preparo'), 'Mosturar.')
        await user.click(screen.getByRole('button', { name: 'Cadastrar receita' }))

        expect(criarReceita).toHaveBeenCalledWith({
            nome: 'Pilsen Clássica',
            estilo: 'Pilsner',
            volumeBaseLitros: 200,
            modoPreparo: 'Mosturar.',
            itens: [{ insumoId: 1, quantidade: 36.5 }],
        })
        expect(await screen.findByTestId('destino')).toHaveTextContent('/receitas/7')
    })

    it('mostra o erro da API (ex.: nome repetido)', async () => {
        criarReceita.mockRejectedValue(erroApi(409, { mensagem: 'Já existe uma receita com esse nome.' }))
        const { user } = await abrirNova()
        await user.type(screen.getByLabelText('Nome *'), 'IPA Cascade')
        await user.type(screen.getByLabelText('Volume base (L) *'), '100')
        await user.selectOptions(screen.getByLabelText('Insumo 1'), '4')
        await user.type(screen.getByLabelText('Quantidade (kg)'), '0,3')
        await user.click(screen.getByRole('button', { name: 'Cadastrar receita' }))

        expect(await screen.findByRole('alert')).toHaveTextContent('Já existe uma receita com esse nome.')
    })

    it('editar carrega a ficha e salva com PUT', async () => {
        obterReceita.mockResolvedValue({
            id: 1,
            nome: 'IPA Cascade',
            estilo: 'India Pale Ale',
            volumeBaseLitros: 100,
            modoPreparo: 'Mosturar.',
            itens: [{ insumoId: 4, insumoNome: 'Lúpulo Cascade', unidadeMedida: 'KG', quantidade: 0.3 }],
        })
        atualizarReceita.mockResolvedValue({ id: 1 })
        const { user } = renderTela(<ReceitaForm />, { rota: '/receitas/1/editar', caminho: '/receitas/:id/editar' })

        expect(await screen.findByRole('heading', { name: 'Editar ficha: IPA Cascade' })).toBeInTheDocument()
        expect(screen.getByLabelText('Volume base (L) *')).toHaveValue('100')
        expect(screen.getByLabelText('Quantidade (kg)')).toHaveValue('0,3')

        await user.click(screen.getByRole('button', { name: 'Salvar ficha' }))
        expect(atualizarReceita).toHaveBeenCalledWith('1', expect.objectContaining({ itens: [{ insumoId: 4, quantidade: 0.3 }] }))
        expect(await screen.findByTestId('destino')).toHaveTextContent('/receitas/1')
    })
})
