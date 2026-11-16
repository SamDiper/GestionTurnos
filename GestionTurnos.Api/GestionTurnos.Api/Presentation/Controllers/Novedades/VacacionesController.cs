using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Novedades;
using GestionTurnos.Api.Presentation.Contracts.Requests.Novedades;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Novedades;

/// <summary>
/// Saldo, solicitudes, períodos vacacionales y resolución de vacaciones (Secciones 3.4, 3.7, 4 y 5 - RF-06, CU-03).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/vacaciones")]
public sealed class VacacionesController : BaseApiController
{
    public VacacionesController(ISender sender) : base(sender)
    {
    }

    /// <summary>
    /// Consulta el saldo de vacaciones del empleado en sesión desde novedades.v_saldo_vacaciones (Sección 3.4).
    /// </summary>
    [HttpGet("mi-saldo")]
    public async Task<IActionResult> ObtenerMiSaldo(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ObtenerMiSaldoVacacionesQuery(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Lista las solicitudes de vacaciones propias del empleado en sesión (Sección 3.4).
    /// </summary>
    [HttpGet("mis-solicitudes")]
    public async Task<IActionResult> ObtenerMisSolicitudes(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ObtenerMisSolicitudesVacacionesQuery(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Solicita vacaciones calculando los días hábiles en el backend y validando el saldo disponible (Sección 3.7 - CU-03).
    /// </summary>
    [HttpPost("mis-solicitudes")]
    public async Task<IActionResult> SolicitarMisVacaciones(
        [FromBody] SolicitarMisVacacionesRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SolicitarMisVacacionesCommand(request.FechaInicio, request.FechaFin, request.Observacion);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Cancela una solicitud de vacaciones propia que siga en estado PENDIENTE (Sección 3.7).
    /// </summary>
    [HttpPost("mis-solicitudes/{idSolicitudVacaciones:long}/cancelar")]
    public async Task<IActionResult> CancelarMiSolicitud(
        long idSolicitudVacaciones,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CancelarMiSolicitudVacacionesCommand(idSolicitudVacaciones), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Lista todas las solicitudes de vacaciones para gestión administrativa (permiso: vacaciones.aprobar).
    /// </summary>
    [HttpGet("solicitudes")]
    public async Task<IActionResult> ListarSolicitudesAdmin(
        [FromQuery] long? idEmpleado,
        [FromQuery] string? estado,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListarSolicitudesVacacionesAdminQuery(idEmpleado, estado, pageNumber, pageSize);
        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Aprueba o rechaza una solicitud de vacaciones revalidando el saldo y aplicando CONFLICTO_BLOQUEA (Sección 4).
    /// </summary>
    [HttpPost("solicitudes/{idSolicitudVacaciones:long}/resolver")]
    public async Task<IActionResult> ResolverSolicitud(
        long idSolicitudVacaciones,
        [FromBody] ResolverSolicitudRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ResolverSolicitudVacacionesCommand(
            idSolicitudVacaciones,
            request.Aprobar,
            request.MotivoRechazo,
            request.ConfirmarAnulacionTurnos);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Registra los días causados por período vacacional que alimentan el saldo (Sección 5 - RF-06).
    /// </summary>
    [HttpPost("periodos")]
    public async Task<IActionResult> RegistrarPeriodoVacacional(
        [FromBody] RegistrarPeriodoVacacionalRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegistrarPeriodoVacacionalCommand(
            request.IdEmpleado,
            request.FechaInicio,
            request.FechaFin,
            request.DiasOtorgados);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }
}

