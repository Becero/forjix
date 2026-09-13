using Forjix.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Forjix.Api.Middleware;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private static readonly Action<ILogger, Exception?> LogUnhandledException =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5000, nameof(GlobalExceptionHandler)),
            "An unhandled error occurred while processing the request.");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            RequestValidationException validation => (StatusCodes.Status400BadRequest, "Dados inválidos.", string.Join(" ", validation.Errors)),
            PermissionDeniedException => (StatusCodes.Status403Forbidden, "Acesso negado.", exception.Message),
            ResourceNotFoundException => (StatusCodes.Status404NotFound, "Recurso não encontrado.", exception.Message),
            ResourceConflictException => (StatusCodes.Status409Conflict, "Conflito.", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", (string?)null)
        };
        if (status == StatusCodes.Status500InternalServerError) LogUnhandledException(logger, exception);
        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Type = $"https://httpstatuses.com/{status}"
            }
        });
    }
}
