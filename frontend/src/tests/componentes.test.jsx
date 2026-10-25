import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import Barra from '../components/Barra.jsx'
import Etiqueta from '../components/Etiqueta.jsx'

describe('Barra', () => {
    it('expõe o nível como barra de progresso acessível', () => {
        render(<Barra nivel={60} status="atencao" rotulo="Nível do estoque" />)
        const barra = screen.getByRole('progressbar', { name: 'Nível do estoque' })
        expect(barra).toHaveAttribute('aria-valuenow', '60')
        expect(barra.firstChild).toHaveClass('barra__preenchimento--atencao')
        expect(barra.firstChild).toHaveStyle({ width: '60%' })
    })
})

describe('Etiqueta', () => {
    it('usa o tipo pedido e "ok" por padrão', () => {
        const { rerender } = render(<Etiqueta tipo="critica">Vencido</Etiqueta>)
        expect(screen.getByText('Vencido')).toHaveClass('etiqueta--critica')
        rerender(<Etiqueta>Normal</Etiqueta>)
        expect(screen.getByText('Normal')).toHaveClass('etiqueta--ok')
    })
})
