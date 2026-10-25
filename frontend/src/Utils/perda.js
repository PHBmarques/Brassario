import { lerNumero } from './armario'

export const MOTIVO_MINIMO = 5
export const MOTIVO_MAXIMO = 255

// Valida o formulário de perda. Devolve { erros, quantidade } (quantidade já como número).
export function validarPerda({ quantidade, motivo }, saldo, formatarSaldo = String) {
    const erros = {}
    const numero = lerNumero(quantidade)
    if (numero === null || Number.isNaN(numero)) erros.quantidade = 'Informe a quantidade.'
    else if (numero <= 0) erros.quantidade = 'A quantidade precisa ser maior que zero.'
    else if (numero > saldo) erros.quantidade = `A perda não pode ser maior que o saldo (${formatarSaldo(saldo)}).`

    const texto = (motivo ?? '').trim()
    if (texto.length < MOTIVO_MINIMO) erros.motivo = `Informe o motivo (mínimo de ${MOTIVO_MINIMO} caracteres).`
    else if (texto.length > MOTIVO_MAXIMO) erros.motivo = `O motivo pode ter até ${MOTIVO_MAXIMO} caracteres.`

    return { erros, quantidade: numero }
}
