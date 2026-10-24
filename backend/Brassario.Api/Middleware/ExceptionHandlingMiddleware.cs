using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Brassario.Api.Middleware
{
    /// <summary>
    /// Captura as exceções da API e devolve sempre o mesmo formato de erro.
    /// 400 validação | 404 não encontrado | 409 duplicidade | 422 regra de negócio | 500 inesperado.
    /// O erro 500 nunca expõe a mensagem interna nem o stack trace: o detalhe vai só para o log.
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // O cliente desistiu da requisição: não é erro do servidor
            }
            catch (Exception ex)
            {
                if (context.Response.HasStarted)
                {
                    _logger.LogError(ex, "Erro depois de iniciar a resposta; não dá para trocar o corpo.");
                    throw;
                }

                var erro = Mapear(ex);

                if (erro.Status >= 500)
                    _logger.LogError(ex, "Erro inesperado em {Metodo} {Caminho}", context.Request.Method, context.Request.Path);
                else
                    _logger.LogInformation("Erro tratado ({Codigo}) em {Metodo} {Caminho}: {Mensagem}",
                        erro.Codigo, context.Request.Method, context.Request.Path, erro.Mensagem);

                context.Response.Clear();
                context.Response.StatusCode = erro.Status;
                await context.Response.WriteAsJsonAsync(erro);
            }
        }

        // A ordem importa: as exceções mais específicas vêm antes das gerais
        internal static ErroResponse Mapear(Exception ex) => ex switch
        {
            ValidacaoException v => new ErroResponse(400, "VALIDACAO", v.Message, v.Detalhes),
            RecursoNaoEncontradoException r => new ErroResponse(404, "RECURSO_NAO_ENCONTRADO", r.Message),
            ConflitoException c => new ErroResponse(409, c.Codigo, c.Message, c.Detalhes),
            MuitasTentativasException m => new ErroResponse(429, "MUITAS_TENTATIVAS", m.Message),
            NegocioException n => new ErroResponse(422, n.Codigo, n.Message, n.Detalhes),
            // A Versao do lote mudou entre a leitura e a gravação: outra operação mexeu no saldo
            DbUpdateConcurrencyException =>
                new ErroResponse(409, "CONFLITO_CONCORRENCIA", "O estoque foi alterado por outra operação ao mesmo tempo. Tente novamente."),
            DbUpdateException { InnerException: PostgresException pg } => MapearBanco(pg),
            _ => new ErroResponse(500, "ERRO_INTERNO", "Ocorreu um erro inesperado. Tente novamente em instantes.")
        };

        // Últimas barreiras do banco (UNIQUE, FK e CHECK) quando a regra não foi barrada antes na aplicação
        private static ErroResponse MapearBanco(PostgresException pg) => pg.SqlState switch
        {
            // Duas entradas simultâneas com o mesmo código passam pela checagem da aplicação; o índice único barra a segunda
            PostgresErrorCodes.UniqueViolation when pg.ConstraintName == "uq_lote_insumo_codigo" =>
                new ErroResponse(409, "CODIGO_LOTE_DUPLICADO", "Já existe um lote com esse código para este insumo. Informe outro código."),
            PostgresErrorCodes.UniqueViolation =>
                new ErroResponse(409, "CONFLITO", "Já existe um registro com esses dados."),
            PostgresErrorCodes.ForeignKeyViolation =>
                new ErroResponse(409, "REGISTRO_EM_USO", "O registro está em uso ou depende de outro que não existe."),
            PostgresErrorCodes.CheckViolation =>
                new ErroResponse(422, "REGRA_NEGOCIO", "A operação viola uma regra de negócio (por exemplo, saldo negativo)."),
            _ => new ErroResponse(500, "ERRO_INTERNO", "Ocorreu um erro inesperado. Tente novamente em instantes.")
        };
    }
}
