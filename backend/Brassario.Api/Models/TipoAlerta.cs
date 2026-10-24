namespace Brassario.Api.Models
{
    public enum TipoAlerta { VENCIDO, VALIDADE_CRITICA, ATENCAO, ESTOQUE_VAZIO, ESTOQUE_BAIXO }

    public enum SeveridadeAlerta
    {
        BAIXA = 1,
        MEDIA = 2,
        ALTA = 3,
        CRITICA = 4
    }
}
