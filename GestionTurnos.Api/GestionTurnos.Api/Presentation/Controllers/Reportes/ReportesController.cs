using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Reportes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Reportes;

/// <summary>
/// Módulo de reportes operativos y consolidados (Sección 5 - RF-10 - permiso: reportes.consultar).
/// Todas las consultas son de solo lectura y están acotadas por rango de fechas.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reportes")]
public sealed class ReportesController : BaseApiController
{
    public ReportesController(ISender sender) : base(sender)
    {
    }

    [HttpGet("cumplimiento-turnos")]
    public async Task<IActionResult> ReporteCumplimientoTurnos(
        [FromQuery] DateOnly fechaDesde,
        [FromQuery] DateOnly fechaHasta,
        [FromQuery] long? idEmpleado,
        [FromQuery] int? idArea,
        [FromQuery] int? idCargo,
        [FromQuery] int? idTurno,
        [FromQuery] bool? soloSinAsistencia,
        [FromQuery] bool? soloConRetardo,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new ReporteCumplimientoTurnosQuery(
            fechaDesde,
            fechaHasta,
            idEmpleado,
            idArea,
            idCargo,
            idTurno,
            soloSinAsistencia,
            soloConRetardo,
            pageNumber,
            pageSize);

        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("consolidado-empleados")]
    public async Task<IActionResult> ReporteConsolidadoPorEmpleado(
        [FromQuery] DateOnly fechaDesde,
        [FromQuery] DateOnly fechaHasta,
        [FromQuery] int? idArea,
        [FromQuery] int? idCargo,
        [FromQuery] long? idEmpleado,
        CancellationToken cancellationToken = default)
    {
        var query = new ReporteConsolidadoPorEmpleadoQuery(
            fechaDesde,
            fechaHasta,
            idArea,
            idCargo,
            idEmpleado);

        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }
}

