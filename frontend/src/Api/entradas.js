import api from './cliente'

// Entrada de estoque em uma só chamada (ADMIN e OPERADOR).
// Insumo: insumoId OU novoInsumo. Fornecedor (opcional): fornecedorId OU novoFornecedor.
// Tudo é gravado na mesma transação: se algo falhar, nada fica salvo.
// Resposta: { lote, insumo, fornecedor, saldoAtual }
export const registrarEntradaComposta = (dados) => api.post('/entradas', dados).then((r) => r.data)
