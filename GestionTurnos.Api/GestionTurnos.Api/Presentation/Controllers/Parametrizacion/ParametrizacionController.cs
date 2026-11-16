using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Parametrizacion;
using GestionTurnos.Api.Presentation.Contracts.Requests.Parametrizacion;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Parametrizacion;

/// <summary>
/// Catálogos y reglas de negocio configurables con vigencia (Secciones 5 y 6.2 - RG-10).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/parametrizacion")]
public sealed class ParametrizacionController : BaseApiController
{
    public ParametrizacionController(ISender sender) : base(sender)
    {
    }

    /// <summary>
    /// Lista tipos de documento (público para el formulario de solicitud de registro).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("tipos-documento")]
    public async Task<IActionResult> ListarTiposDocumento(
        [FromQuery] bool soloActivos = true,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarTiposDocumentoQuery(soloActivos), cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("tipos-contrato")]
    public async Task<IActionResult> ListarTiposContrato(
        [FromQuery] bool soloActivos = true,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarTiposContratoQuery(soloActivos), cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("tipos-novedad")]
    public async Task<IActionResult> ListarTiposNovedad(
        [FromQuery] bool soloActivos = true,
        [FromQuery] bool soloPermitidosEmpleado = false,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarTiposNovedadQuery(soloActivos, soloPermitidosEmpleado), cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("categorias-hora")]
    public async Task<IActionResult> ListarCategoriasHora(
        [FromQuery] bool soloActivos = true,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarCategoriasHoraQuery(soloActivos), cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("dias-festivos")]
    public async Task<IActionResult> ListarDiasFestivos(
        [FromQuery] int? anio,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarDiasFestivosQuery(anio), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("dias-festivos")]
    public async Task<IActionResult> GuardarDiaFestivo(
        [FromBody] GuardarDiaFestivoRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GuardarDiaFestivoCommand(request.Fecha, request.Nombre), cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("parametros")]
    public async Task<IActionResult> ListarParametros(
        [FromQuery] DateOnly? fechaReferencia,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarParametrosQuery(fechaReferencia), cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("parametros/{codParametro}/historial")]
    public async Task<IActionResult> ListarHistorialParametro(
        string codParametro,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ListarHistorialParametroQuery(codParametro), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Cierra el valor vigente de un parámetro editable e inserta la nueva vigencia en una sola transacción (Sección 6.2).
    /// </summary>
    [HttpPost("parametros/{codParametro}/valores")]
    public async Task<IActionResult> CambiarValorParametro(
        string codParametro,
        [FromBody] CambiarValorParametroRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CambiarValorParametroCommand(codParametro, request.Valor, request.VigenteDesde);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }
}

