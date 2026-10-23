namespace Brassario.Api.DTOs.Response
{
    /// <summary>Senha temporária gerada pelo ADMIN. Só aparece nesta resposta; no banco fica o hash.</summary>
    public record SenhaTemporariaResponse(UsuarioResponse Usuario, string SenhaTemporaria)
    {
        public override string ToString() => $"SenhaTemporariaResponse {{ Usuario = {Usuario.Email}, SenhaTemporaria = *** }}";
    }
}
