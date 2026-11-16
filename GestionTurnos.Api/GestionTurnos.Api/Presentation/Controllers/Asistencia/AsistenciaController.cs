using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Asistencia;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Presentation.Contracts.Requests.Asistencia;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Asistencia;

/// <summary>
/// Marcación de entrada/salida, liquidación de horas y consultas de asistencia (Secciones 3.4, 3.6 y 6.3 - RF-05, CU-02, CU-05).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/asistencia")]
public sealed class AsistenciaController : BaseApiController
{
    public AsistenciaController(ISender sender) : base(sender)
    {
    }

    /// <summary>
    /// Registra la entrada del empleado en sesión usando la hora del servidor now() (Sección 3.6 - CU-02).
    /// </summary>
    [HttpPost("mi-entrada")]
    public async Task<IActionResult> RegistrarMiEntrada(
        [FromBody] RegistrarMiEntradaRequest? request,
        CancellationToken cancellationToken)
    {
        var fuente = request?.CodFuenteMarcacion ?? FuentesMarcacionCodigos.Web;
        var result = await Sender.Send(new RegistrarMiEntradaCommand(fuente), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Registra la salida del empleado en sesión usando now() y liquida las horas en la misma transacción (Sección 3.6 y 6.3).
    /// </summary>
    [HttpPost("mi-salida")]
    public async Task<IActionResult> RegistrarMiSalida(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new RegistrarMiSalidaCommand(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Detalle de horas programadas vs trabajadas por turno desde asistencia.v_horas_por_turno (Sección 3.4).
    /// </summary>
    [HttpGet("mis-horas/turnos")]
    public async Task<IActionResult> ObtenerMisHorasPorTurno(
        [FromQuery] DateOnly desde,
        [FromQuery] DateOnly hasta,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ObtenerMisHorasPorTurnoQuery(desde, hasta), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Consolidado de horas agrupado por grano ('day', 'week', 'month', 'year') desde asistencia.v_horas_por_turno (Sección 3.4).
    /// </summary>
    [HttpGet("mis-horas/consolidado")]
    public async Task<IActionResult> ObtenerMiConsolidadoHoras(
        [FromQuery] DateOnly desde,
        [FromQuery] DateOnly hasta,
        [FromQuery] string grano = "week",
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ObtenerMiConsolidadoHorasQuery(desde, hasta, grano), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Suma de horas por categoría (ordinaria, extra, recargos) desde asistencia.detalle_horas (Sección 3.4).
    /// </summary>
    [HttpGet("mis-horas/categorias")]
    public async Task<IActionResult> ObtenerMisHorasPorCategoria(
        [FromQuery] DateTimeOffset desde,
        [FromQuery] DateTimeOffset hasta,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ObtenerMisHorasPorCategoriaQuery(desde, hasta), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Registro manual de asistencia por el administrador con reliquidación automática (Sección 3.6 - permiso: asistencia.registrar_manual).
    /// </summary>
    [HttpPost("manual")]
    public async Task<IActionResult> RegistrarManual(
        [FromBody] RegistrarAsistenciaManualRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegistrarOCorregirAsistenciaManualCommand(
            null,
            request.IdEmpleado,
            request.IdAsignacion,
            request.HoraEntrada,
            request.HoraSalida,
            request.Observacion);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Corrección manual de un registro de asistencia existente con reliquidación automática (Sección 3.6 - permiso: asistencia.registrar_manual).
    /// </summary>
    [HttpPut("manual/{idRegistro:long}")]
    public async Task<IActionResult> CorregirManual(
        long idRegistro,
        [FromBody] RegistrarAsistenciaManualRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegistrarOCorregirAsistenciaManualCommand(
            idRegistro,
            request.IdEmpleado,
            request.IdAsignacion,
            request.HoraEntrada,
            request.HoraSalida,
            request.Observacion);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }
}

