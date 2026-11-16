using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Turnos;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Turnos;

/// <summary>
/// Consultas de calendario e historial de turnos para el empleado autenticado (Sección 3.4 - RF-09).
/// Filtra obligatoriamente por el id_empleado de la sesión.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/mi-calendario")]
public sealed class MiCalendarioController : BaseApiController
{
    public MiCalendarioController(ISender sender) : base(sender)
    {
    }

    /// <summary>
    /// Consulta el calendario unificado (turnos, descansos, vacaciones, novedades) sobre turnos.v_calendario (Sección 3.4).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ObtenerMiCalendario(
        [FromQuery] DateTimeOffset desde,
        [FromQuery] DateTimeOffset hasta,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ObtenerMiCalendarioQuery(desde, hasta), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Consulta el historial de turnos pasados (fecha &lt; current_date) del empleado en sesión (Sección 3.4).
    /// </summary>
    [HttpGet("historial-turnos")]
    public async Task<IActionResult> ObtenerMiHistorialTurnos(
        [FromQuery] DateOnly? desde,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ObtenerMiHistorialTurnosQuery(desde, pageNumber, pageSize), cancellationToken);
        return HandleResult(result);
    }
}

