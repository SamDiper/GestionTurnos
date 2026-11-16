using System.Globalization;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="IParametroService"/> que consulta el valor vigente en parametrizacion.parametro_valor (Sección 6.2):
/// SELECT p.tipo_dato, v.valor FROM parametrizacion.parametro p JOIN parametrizacion.parametro_valor v USING (cod_parametro)
/// WHERE p.cod_parametro = $1 AND v.vigente_desde &lt;= $fecha AND (v.vigente_hasta IS NULL OR v.vigente_hasta &gt;= $fecha);
/// </summary>
public sealed class ParametroService : IParametroService
{
    private readonly GestionTurnosContext _context;

    public ParametroService(GestionTurnosContext context)
    {
        _context = context;
    }

    public async Task<int> ObtenerEnteroAsync(
        string codParametro,
        DateOnly fecha,
        int valorDefecto,
        CancellationToken cancellationToken = default)
    {
        var raw = await ObtenerValorVigenteRawAsync(codParametro, fecha, cancellationToken);
        if (raw is not null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed;
        }
        return valorDefecto;
    }

    public async Task<decimal> ObtenerDecimalAsync(
        string codParametro,
        DateOnly fecha,
        decimal valorDefecto,
        CancellationToken cancellationToken = default)
    {
        var raw = await ObtenerValorVigenteRawAsync(codParametro, fecha, cancellationToken);
        if (raw is not null && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed))
        {
            return parsed;
        }
        return valorDefecto;
    }

    public async Task<bool> ObtenerBooleanoAsync(
        string codParametro,
        DateOnly fecha,
        bool valorDefecto,
        CancellationToken cancellationToken = default)
    {
        var raw = await ObtenerValorVigenteRawAsync(codParametro, fecha, cancellationToken);
        if (raw is not null)
        {
            if (bool.TryParse(raw, out bool parsed))
            {
                return parsed;
            }
            if (raw == "1") return true;
            if (raw == "0") return false;
        }
        return valorDefecto;
    }

    public async Task<TimeOnly> ObtenerHoraAsync(
        string codParametro,
        DateOnly fecha,
        TimeOnly valorDefecto,
        CancellationToken cancellationToken = default)
    {
        var raw = await ObtenerValorVigenteRawAsync(codParametro, fecha, cancellationToken);
        if (raw is not null && TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, out TimeOnly parsed))
        {
            return parsed;
        }
        return valorDefecto;
    }

    public async Task<string?> ObtenerTextoAsync(
        string codParametro,
        DateOnly fecha,
        string? valorDefecto = null,
        CancellationToken cancellationToken = default)
    {
        var raw = await ObtenerValorVigenteRawAsync(codParametro, fecha, cancellationToken);
        return raw ?? valorDefecto;
    }

    private async Task<string?> ObtenerValorVigenteRawAsync(
        string codParametro,
        DateOnly fecha,
        CancellationToken cancellationToken)
    {
        var codigo = codParametro.Trim().ToUpperInvariant();

        return await _context.ParametroValores
            .AsNoTracking()
            .Where(v => v.CodParametro == codigo &&
                        v.VigenteDesde <= fecha &&
                        (v.VigenteHasta == null || v.VigenteHasta >= fecha))
            .OrderByDescending(v => v.VigenteDesde)
            .Select(v => v.Valor)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

