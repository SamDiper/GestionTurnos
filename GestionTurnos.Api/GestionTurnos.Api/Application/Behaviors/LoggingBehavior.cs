using System.Diagnostics;
using GestionTurnos.Api.Domain.Shared;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GestionTurnos.Api.Application.Behaviors;

/// <summary>
/// Pipeline Behavior de MediatR que intercepta todas las peticiones
/// para registrar su ejecución, parámetros y tiempo de respuesta.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Iniciando solicitud: {RequestName} - {@Request}", requestName, request);

        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        stopwatch.Stop();

        if (response is Result result && result.IsFailure)
        {
            _logger.LogWarning("La solicitud {RequestName} falló en {ElapsedMilliseconds} ms. Error: {@Error}",
                requestName, stopwatch.ElapsedMilliseconds, result.Error);
        }
        else
        {
            _logger.LogInformation("Solicitud {RequestName} completada exitosamente en {ElapsedMilliseconds} ms.",
                requestName, stopwatch.ElapsedMilliseconds);
        }

        return response;
    }
}

