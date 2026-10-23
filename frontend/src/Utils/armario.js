// Textos e cálculos do Armário, compartilhados entre a lista, o detalhe e o cadastro.

export const CATEGORIAS = [
    { valor: 'MALTE', rotulo: 'Malte' },
    { valor: 'LUPULO', rotulo: 'Lúpulo' },
    { valor: 'LEVEDURA', rotulo: 'Levedura' },
    { valor: 'ADJUNTO', rotulo: 'Adjunto' },
    { valor: 'OUTRO', rotulo: 'Outro' },
]

export const UNIDADES = [
    { valor: 'KG', rotulo: 'kg' },
    { valor: 'G', rotulo: 'g' },
    { valor: 'L', rotulo: 'L' },
    { valor: 'ML', rotulo: 'mL' },
    { valor: 'UN', rotulo: 'un' },
]

export const rotuloCategoria = (valor) => CATEGORIAS.find((c) => c.valor === valor)?.rotulo ?? valor
export const rotuloUnidade = (valor) => UNIDADES.find((u) => u.valor === valor)?.rotulo ?? valor

export const formatarNumero = (valor) =>
    Number(valor ?? 0).toLocaleString('pt-BR', { maximumFractionDigits: 3 })

export const formatarQuantidade = (valor, unidade) => `${formatarNumero(valor)} ${rotuloUnidade(unidade)}`

// "2027-06-30" -> "30/06/2027" (sem passar por Date, para não sofrer com fuso)
export const formatarData = (iso) => (iso ? iso.split('-').reverse().join('/') : '—')

// Data de hoje no formato da API (AAAA-MM-DD), no fuso local
export function hojeIso() {
    const d = new Date()
    const mes = String(d.getMonth() + 1).padStart(2, '0')
    const dia = String(d.getDate()).padStart(2, '0')
    return `${d.getFullYear()}-${mes}-${dia}`
}

// Dias de hoje até a data (negativo se já passou)
export function diasAte(iso) {
    const [ano, mes, dia] = iso.split('-').map(Number)
    const alvo = Date.UTC(ano, mes - 1, dia)
    const [a, m, d] = hojeIso().split('-').map(Number)
    return Math.round((alvo - Date.UTC(a, m - 1, d)) / 86400000)
}

// Aceita "1,5" e "1.5"
export function lerNumero(texto) {
    const limpo = String(texto ?? '').trim().replace(/\s/g, '').replace(',', '.')
    if (limpo === '') return null
    const numero = Number(limpo)
    return Number.isFinite(numero) ? numero : NaN
}

// Cor da validade (VERDE | AMARELO | VERMELHO) -> tipo de Barra/Etiqueta
const TIPO_POR_COR = { VERDE: 'ok', AMARELO: 'atencao', VERMELHO: 'critica' }
export const tipoPorCor = (cor) => TIPO_POR_COR[cor] ?? 'ok'

// Mesma regra da API: crítico tem prioridade sobre atenção
export function corPorDias(dias, diasCritica, diasAtencao) {
    if (dias <= diasCritica) return 'VERMELHO'
    if (dias <= diasAtencao) return 'AMARELO'
    return 'VERDE'
}

// Barra cheia = o dobro do estoque mínimo (assim, metade da barra é o mínimo)
export function nivelDoSaldo(saldo, estoqueMinimo) {
    if (!saldo || saldo <= 0) return 0
    if (!estoqueMinimo || estoqueMinimo <= 0) return 100
    return Math.min(100, Math.max(4, (saldo / (estoqueMinimo * 2)) * 100))
}

// Como um item do Armário aparece no cartão: barra, etiqueta e texto
export function situacaoDoItem({ saldo, statusEscassez, cor, diasParaVencer, estoqueMinimo }) {
    if (statusEscassez === 'VAZIO' || !saldo) {
        return { barra: 'vazio', etiqueta: 'critica', texto: 'Armário vazio', nivel: 0 }
    }
    const tipo = tipoPorCor(cor)
    const textos = {
        ok: `Vence em ${diasParaVencer} dias`,
        atencao: `Atenção: vence em ${diasParaVencer} dias`,
        critica: `Validade Crítica: ${diasParaVencer} ${diasParaVencer === 1 ? 'dia' : 'dias'}`,
    }
    return { barra: tipo, etiqueta: tipo, texto: textos[tipo], nivel: nivelDoSaldo(saldo, estoqueMinimo) }
}
