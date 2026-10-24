namespace Brassario.Api.Services
{
    /// <summary>
    /// Converte um período em dias (datas do calendário do usuário, no fuso do servidor) para o intervalo em UTC,
    /// que é como as datas são gravadas. Uma produção feita às 23h30 do dia 20 em São Paulo (02h30 UTC do dia 21)
    /// continua sendo do dia 20.
    /// </summary>
    public static class PeriodoLocal
    {
        /// <returns>Início do dia "de" (inclusivo) e início do dia seguinte a "ate" (exclusivo), ambos em UTC.</returns>
        public static (DateTime? InicioUtc, DateTime? FimUtcExclusivo) ParaUtc(DateOnly? de, DateOnly? ate, TimeZoneInfo fuso) =>
            (de is null ? null : MeiaNoiteEmUtc(de.Value, fuso),
             ate is null ? null : MeiaNoiteEmUtc(ate.Value.AddDays(1), fuso));

        private static DateTime MeiaNoiteEmUtc(DateOnly dia, TimeZoneInfo fuso)
        {
            var local = DateTime.SpecifyKind(dia.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);

            // Em fusos com horário de verão que começa à meia-noite essa hora não existe: vale a seguinte
            if (fuso.IsInvalidTime(local)) local = local.AddHours(1);

            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(local, fuso), DateTimeKind.Utc);
        }
    }
}
