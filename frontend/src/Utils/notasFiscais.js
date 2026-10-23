// A API ainda não guarda o número da nota fiscal do lote.
// Enquanto isso, a NF digitada no cadastro fica guardada só neste navegador, pelo id do lote,
// para aparecer na coluna "Observação" do detalhe. Quando a API tiver o campo, apague este arquivo.
const CHAVE = 'brassario-nf-lotes'

function ler() {
    try {
        return JSON.parse(localStorage.getItem(CHAVE) ?? '{}') ?? {}
    } catch {
        return {}
    }
}

export const notaFiscalDoLote = (loteId) => ler()[loteId] ?? null

export function guardarNotaFiscal(loteId, nf) {
    if (!nf) return
    try {
        localStorage.setItem(CHAVE, JSON.stringify({ ...ler(), [loteId]: nf }))
    } catch {
        /* ignora */
    }
}
