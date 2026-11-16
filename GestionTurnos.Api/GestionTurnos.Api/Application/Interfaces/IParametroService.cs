namespace GestionTurnos.Api.Application.Interfaces;

/// <summary>
/// Servicio para consultar los valores vigentes de parametrizacion.parametro_valor según una fecha dada (Sección 6.2).
/// Evita dejar números de reglas de negocio fijos en el código.
/// </summary>
public interface IParametroService
{
    Task<int> ObtenerEnteroAsync(string codParametro, DateOnly fecha, int valorDefecto, CancellationToken cancellationToken = default);
    Task<decimal> ObtenerDecimalAsync(string codParametro, DateOnly fecha, decimal valorDefecto, CancellationToken cancellationToken = default);
    Task<bool> ObtenerBooleanoAsync(string codParametro, DateOnly fecha, bool valorDefecto, CancellationToken cancellationToken = default);
    Task<TimeOnly> ObtenerHoraAsync(string codParametro, DateOnly fecha, TimeOnly valorDefecto, CancellationToken cancellationToken = default);
    Task<string?> ObtenerTextoAsync(string codParametro, DateOnly fecha, string? valorDefecto = null, CancellationToken cancellationToken = default);
}

