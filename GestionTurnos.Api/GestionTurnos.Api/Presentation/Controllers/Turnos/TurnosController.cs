using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Turnos;
using GestionTurnos.Api.Presentation.Contracts.Requests.Turnos;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Turnos;

/// <summary>
/// Catálogo de Turnos, Programación, Días de Descanso y Calendario General (Secciones 5 y 6.1 - RF-03, RF-04, RF-09).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/turnos")]
public sealed class TurnosController : BaseApiController
{
    public TurnosController(ISender sender) : base(sender)
    {
    }

    [HttpGet("catalogo")]
    public async Task<IActionResult> ListarCatalogo(
        [FromQuery] bool soloActivos = true,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarTurnosQuery(soloActivos), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("catalogo")]
    public async Task<IActionResult> CrearTurno(
        [FromBody] GuardarTurnoRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GuardarTurnoCommand(
            null,
            request.Nombre,
            request.HoraInicio,
            request.HoraFin,
            request.MinutosDescanso,
            request.Color,
            request.Activo);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    [HttpPut("catalogo/{idTurno:int}")]
    public async Task<IActionResult> ActualizarTurno(
        int idTurno,
        [FromBody] GuardarTurnoRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GuardarTurnoCommand(
            idTurno,
            request.Nombre,
            request.HoraInicio,
            request.HoraFin,
            request.MinutosDescanso,
            request.Color,
            request.Activo);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("asignaciones")]
    public async Task<IActionResult> ListarAsignaciones(
        [FromQuery] DateOnly fechaDesde,
        [FromQuery] DateOnly fechaHasta,
        [FromQuery] long? idEmpleado,
        [FromQuery] int? idArea,
        [FromQuery] int? idTurno,
        [FromQuery] string? estado,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new ListarAsignacionesTurnoQuery(
            fechaDesde,
            fechaHasta,
            idEmpleado,
            idArea,
            idTurno,
            estado,
            pageNumber,
            pageSize);

        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Programa uno o varios turnos en una sola transacción construyendo el periodo en la zona horaria de la empresa (Sección 6.1).
    /// </summary>
    [HttpPost("asignaciones")]
    public async Task<IActionResult> ProgramarTurnos(
        [FromBody] ProgramarTurnosRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ProgramarTurnosCommand(request.Asignaciones), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Modifica el turno o fecha de una asignación recalculando su periodo (CU-04, Sección 6.1).
    /// </summary>
    [HttpPut("asignaciones/{idAsignacion:long}")]
    public async Task<IActionResult> ModificarAsignacion(
        long idAsignacion,
        [FromBody] ModificarAsignacionTurnoRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ModificarAsignacionTurnoCommand(
            idAsignacion,
            request.IdTurno,
            request.Fecha,
            request.Observacion);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Anula una asignación de turno (cod_estado_asignacion = 'ANULADA') si no tiene registro de asistencia (Sección 6.1).
    /// </summary>
    [HttpPost("asignaciones/{idAsignacion:long}/anular")]
    public async Task<IActionResult> AnularAsignacion(
        long idAsignacion,
        [FromBody] AnularAsignacionTurnoRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new AnularAsignacionTurnoCommand(idAsignacion, request?.Observacion), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Publica las asignaciones programadas en un rango de fechas cambiándolas a 'CONFIRMADA' (Sección 6.1).
    /// </summary>
    [HttpPost("asignaciones/publicar")]
    public async Task<IActionResult> PublicarAsignaciones(
        [FromBody] PublicarAsignacionesTurnoRequest request,
        CancellationToken cancellationToken)
    {
        var command = new PublicarAsignacionesTurnoCommand(
            request.FechaDesde,
            request.FechaHasta,
            request.IdArea,
            request.IdEmpleado);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Programa un día de descanso validando que no exista un turno activo en esa fecha (Sección 6.1).
    /// </summary>
    [HttpPost("descansos")]
    public async Task<IActionResult> ProgramarDiaDescanso(
        [FromBody] ProgramarDiaDescansoRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ProgramarDiaDescansoCommand(request.IdEmpleado, request.Fecha), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Consulta el calendario general unificado filtrando por empleado, área o tipo de evento (Sección 5 - RF-09).
    /// </summary>
    [HttpGet("calendario-general")]
    public async Task<IActionResult> ObtenerCalendarioGeneral(
        [FromQuery] DateTimeOffset desde,
        [FromQuery] DateTimeOffset hasta,
        [FromQuery] long? idEmpleado,
        [FromQuery] int? idArea,
        [FromQuery] string? tipo,
        CancellationToken cancellationToken = default)
    {
        var query = new ObtenerCalendarioGeneralQuery(desde, hasta, idEmpleado, idArea, tipo);
        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }
}

