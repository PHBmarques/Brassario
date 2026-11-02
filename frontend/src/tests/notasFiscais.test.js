import { describe, it, expect, vi } from 'vitest'
import { guardarNotaFiscal, notaFiscalDoLote } from '../Utils/notasFiscais'

describe('notas fiscais guardadas no navegador', () => {
    it('guarda e devolve a nota pelo id do lote', () => {
        guardarNotaFiscal(7, 'NF-123')
        expect(notaFiscalDoLote(7)).toBe('NF-123')
        expect(notaFiscalDoLote(8)).toBeNull()
    })

    it('não apaga as notas de outros lotes', () => {
        guardarNotaFiscal(1, 'A')
        guardarNotaFiscal(2, 'B')
        expect(notaFiscalDoLote(1)).toBe('A')
        expect(notaFiscalDoLote(2)).toBe('B')
    })

    it('ignora nota vazia', () => {
        guardarNotaFiscal(3, '')
        expect(notaFiscalDoLote(3)).toBeNull()
    })

    it('não quebra com conteúdo inválido no localStorage', () => {
        localStorage.setItem('brassario-nf-lotes', '{quebrado')
        expect(notaFiscalDoLote(1)).toBeNull()
    })

    it('não quebra quando o localStorage lança erro ao gravar', () => {
        const espiao = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('cheio') })
        expect(() => guardarNotaFiscal(1, 'X')).not.toThrow()
        espiao.mockRestore()
    })
})
