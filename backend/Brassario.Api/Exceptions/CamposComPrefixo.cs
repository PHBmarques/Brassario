namespace Brassario.Api.Exceptions
{
    /// <summary>
    /// Na entrada composta (POST /entradas) os campos vêm aninhados ("lote.quantidade", "novoInsumo.nome").
    /// Os serviços reaproveitados devolvem o nome simples do campo; aqui ele ganha o prefixo.
    /// </summary>
    public static class CamposComPrefixo
    {
        public static ValidacaoException Aplicar(ValidacaoException erro, string prefixo) =>
            new(Mapear(erro.Detalhes, prefixo));

        public static ConflitoException Aplicar(ConflitoException erro, string prefixo) =>
            new(erro.Message, erro.Codigo, erro.Detalhes is null ? null : Mapear(erro.Detalhes, prefixo));

        public static Dictionary<string, string[]> Mapear(IDictionary<string, string[]> detalhes, string prefixo) =>
            detalhes.ToDictionary(d => prefixo + d.Key, d => d.Value);
    }
}
