namespace Brassario.Api.Security
{
    /// <summary>
    /// Marca as rotas que uma conta com troca de senha pendente (claim pwd_change) ainda pode usar.
    /// Qualquer outra rota autenticada responde 403 TROCA_DE_SENHA_OBRIGATORIA.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class PermitirComTrocaPendenteAttribute : Attribute
    {
    }
}