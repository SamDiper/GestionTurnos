using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GestionTurnos.Api.Presentation.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Exception occurred: {Message}", exception.Message);

        var (statusCode, title, type) = exception switch
        {
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "El registro fue modificado por otro usuario mientras intentabas actualizarlo. Por favor, recarga la información e intenta de nuevo.",
                "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8"
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Error interno del servidor",
                "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1"
            )
        };

        var env = httpContext.RequestServices.GetService(typeof(IHostEnvironment)) as IHostEnvironment;
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Detail = env?.IsDevelopment() == true ? exception.Message : null
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}

