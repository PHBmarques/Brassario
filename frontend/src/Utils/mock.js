// DADOS DE EXEMPLO (iguais aos do esboço).
// Troque por chamadas à API quando os endpoints estiverem prontos.

// status: 'ok' | 'atencao' | 'critica' | 'vazio'
// nivel: 0 a 100 (preenchimento da barra)
export const INSUMOS = [
    { id: 1, nome: 'Malte Pilsen', categoria: 'Malte', quantidade: 50, unidade: 'kg', nivel: 85, status: 'ok', texto: 'Vence em 120 dias' },
    { id: 2, nome: 'Lúpulo Cascade', categoria: 'Lúpulo', quantidade: 1.2, unidade: 'kg', nivel: 30, status: 'atencao', texto: 'Atenção: vence em 32 dias' },
    { id: 3, nome: 'Levedura US-05', categoria: 'Levedura', quantidade: 0.5, unidade: 'kg', nivel: 10, status: 'critica', texto: 'Validade Crítica: 6 dias' },
    { id: 4, nome: 'Malte Munich', categoria: 'Malte', quantidade: 0, unidade: 'kg', nivel: 0, status: 'vazio', texto: 'Armário vazio' },
    { id: 5, nome: 'Lúpulo Citra', categoria: 'Lúpulo', quantidade: 2, unidade: 'kg', nivel: 60, status: 'ok', texto: 'Vence em 90 dias' },
    { id: 6, nome: 'Levedura S-04', categoria: 'Levedura', quantidade: 1, unidade: 'kg', nivel: 45, status: 'ok', texto: 'Vence em 75 dias' },
]

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
