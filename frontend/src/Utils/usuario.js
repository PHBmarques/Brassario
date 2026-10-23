// Iniciais para o avatar: primeira letra do primeiro e do último nome ("Carlos Lima" -> "CL")
export function iniciais(nome = '') {
    const partes = (nome ?? '').trim().split(/\s+/).filter(Boolean)
    if (partes.length === 0) return '?'
    const primeira = partes[0][0]
    const ultima = partes.length > 1 ? partes[partes.length - 1][0] : ''
    return (primeira + ultima).toUpperCase()
}
