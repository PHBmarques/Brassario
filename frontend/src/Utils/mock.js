// DADOS DE EXEMPLO (iguais aos do esboço).
// Troque por chamadas à API quando os endpoints estiverem prontos.

export const PRECISA_ATENCAO = [
    { id: 1, nome: 'Levedura US-05', detalhe: 'Vence em 6 dias', etiqueta: 'Validade Crítica', tipo: 'critica' },
    { id: 2, nome: 'Malte Munich', detalhe: 'Sem estoque no Armário', etiqueta: 'Armário vazio', tipo: 'critica' },
    { id: 3, nome: 'Lúpulo Cascade', detalhe: 'Abaixo do mínimo de 1,5 kg', etiqueta: 'Estoque baixo', tipo: 'atencao' },
]

export const INDICADORES = [
    { id: 'alerta', rotulo: 'Insumos em alerta', valor: '3', destaque: 'perigo' },
    { id: 'bateladas', rotulo: 'Produções no mês', valor: '8' },
    { id: 'custo', rotulo: 'Custo médio por litro', valor: 'R$ 4,13' },
    { id: 'rendimento', rotulo: 'Rendimento médio', valor: '92%' },
]

// nivel: 0 a 100 (altura da coluna), valor: kg consumidos
export const CONSUMO_30_DIAS = [
    { rotulo: 'Malte', valor: 180, nivel: 100 },
    { rotulo: 'Lúpulo', valor: 76, nivel: 42 },
    { rotulo: 'Levedura', valor: 54, nivel: 30 },
    { rotulo: 'Adjuntos', valor: 36, nivel: 20 },
]

export const DURACAO_ESTOQUE = [
    { nome: 'Malte Pilsen', dias: 12, nivel: 80, status: 'ok' },
    { nome: 'Lúpulo Cascade', dias: 5, nivel: 35, status: 'atencao' },
    { nome: 'Levedura US-05', dias: 2, nivel: 12, status: 'critica' },
]
