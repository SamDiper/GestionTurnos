using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Auditoria;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Auditoria;

/// <summary>
/// Consulta de la bitácora inmutable de negocio (Sección 5 - RF-08, RG-09 - permiso: auditoria.consultar).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auditoria")]
public sealed class AuditoriaController : BaseApiController
{
    public AuditoriaController(ISender sender) : base(sender)
    {
    }

    [HttpGet("bitacora")]
    public async Task<IActionResult> ConsultarBitacora(
        [FromQuery] long? idEmpleado,
        [FromQuery] long? idUsuario,
        [FromQuery] string? codTipoAccion,
        [FromQuery] string? esquema,
        [FromQuery] string? tabla,
        [FromQuery] DateTimeOffset? fechaDesde,
        [FromQuery] DateTimeOffset? fechaHasta,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var query = new ConsultarBitacoraQuery(
            idEmpleado,
            idUsuario,
            codTipoAccion,
            esquema,
            tabla,
            fechaDesde,
            fechaHasta,
            pageNumber,
            pageSize);

        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }
}

