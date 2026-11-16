using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Asistencia;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.Calculators;

/// <summary>
/// Implementa la sección 6.3: Liquidar horas.
/// 1. Calcula los minutos netos trabajados descontando turno.minutos_descanso.
/// 2. Si hay una novedad aprobada con afecta_horas = true que se cruza con el turno/registro, descuenta ese tiempo (RG-03).
/// 3. Reemplaza el detalle del registro en asistencia.detalle_horas (única tabla donde se usa DELETE).
/// 4. Mientras el cliente valida las reglas exactas de extras y recargos (ERS sección 10), liquida todo como ORDINARIA.
/// </summary>
public sealed class LiquidacionHorasCalculator : ILiquidacionHorasCalculator
{
    private readonly GestionTurnosContext _context;
    private readonly IParametroService _parametroService;

    public LiquidacionHorasCalculator(
        GestionTurnosContext context,
        IParametroService parametroService)
    {
        _context = context;
        _parametroService = parametroService;
    }

    public async Task LiquidarRegistroAsync(long idRegistro, CancellationToken cancellationToken = default)
    {
        var registro = await _context.RegistrosAsistencia
            .Include(r => r.Asignacion)
                .ThenInclude(a => a!.Turno)
            .FirstOrDefaultAsync(r => r.IdRegistro == idRegistro, cancellationToken);

        if (registro is null || !registro.HoraSalida.HasValue || registro.HoraSalida.Value <= registro.HoraEntrada)
        {
            return;
        }

        var entrada = registro.HoraEntrada;
        var salida = registro.HoraSalida.Value;
        var fechaReferencia = registro.Asignacion?.Fecha ?? DateOnly.FromDateTime(entrada.UtcDateTime);

        // Lectura de parámetros vigentes en la fecha del turno (Sección 6.2 y 6.3)
        _ = await _parametroService.ObtenerHoraAsync(
            ParametrosCodigos.HoraInicioNocturna, fechaReferencia, cancellationToken);
        _ = await _parametroService.ObtenerHoraAsync(
            ParametrosCodigos.HoraFinNocturna, fechaReferencia, cancellationToken);

        int minutosBrutos = (int)Math.Round((salida - entrada).TotalMinutes);
        int minutosDescanso = registro.Asignacion?.Turno.MinutosDescanso ?? 0;

        // 3. Si hay una novedad aprobada con afecta_horas = true que se cruza con el turno/registro, descontar ese tiempo (RG-03)
        var novedadesQueAfectan = await _context.Novedades
            .AsNoTracking()
            .Where(n => n.IdEmpleado == registro.IdEmpleado &&
                        n.CodEstadoSolicitud == EstadosSolicitud.Aprobada &&
                        n.AfectaHoras &&
                        n.Inicio < salida &&
                        entrada < n.Fin)
            .ToListAsync(cancellationToken);

        int minutosDescuentoNovedades = 0;
        foreach (var nov in novedadesQueAfectan)
        {
            var inicioCruce = nov.Inicio > entrada ? nov.Inicio : entrada;
            var finCruce = nov.Fin < salida ? nov.Fin : salida;
            if (finCruce > inicioCruce)
            {
                minutosDescuentoNovedades += (int)Math.Round((finCruce - inicioCruce).TotalMinutes);
            }
        }

        int minutosNetos = Math.Max(0, minutosBrutos - minutosDescanso - minutosDescuentoNovedades);

        // 2. Reemplazar el detalle del registro:
        // DELETE FROM asistencia.detalle_horas WHERE id_registro = $1;
        var detallesExistentes = await _context.DetallesHoras
            .Where(d => d.IdRegistro == idRegistro)
            .ToListAsync(cancellationToken);

        if (detallesExistentes.Count > 0)
        {
            _context.DetallesHoras.RemoveRange(detallesExistentes);
        }

        // INSERT INTO asistencia.detalle_horas (solo categorías con minutos > 0)
        // Mientras se validan las reglas de extras/recargos con el cliente (Sección 6.3), liquida todo como ORDINARIA.
        if (minutosNetos > 0)
        {
            await _context.DetallesHoras.AddAsync(new DetalleHoras
            {
                IdRegistro = idRegistro,
                CodCategoriaHora = CategoriasHoraCodigos.Ordinaria,
                Minutos = minutosNetos
            }, cancellationToken);
        }
    }
}

