using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Seguridad.Auth;
using GestionTurnos.Api.Application.CQRS.Seguridad.Registro;
using GestionTurnos.Api.Presentation.Contracts.Requests.Seguridad;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Seguridad;

/// <summary>
/// Endpoints públicos de autenticación (Sección 3.3) y solicitud de registro (Sección 3.1).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController : BaseApiController
{
    public AuthController(ISender sender) : base(sender)
    {
    }

    /// <summary>
    /// Inicia sesión validando estado en seguridad.usuario y seguridad.solicitud_registro (Sección 3.3).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var command = new IniciarSesionCommand(request.Correo, request.Contrasena);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Solicita el registro de un nuevo empleado, quedando en estado PENDIENTE (Sección 3.1).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("solicitar-registro")]
    public async Task<IActionResult> SolicitarRegistro(
        [FromBody] SolicitarRegistroRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SolicitarRegistroCommand(
            request.CodTipoDocumento,
            request.NumeroDocumento,
            request.Nombres,
            request.Apellidos,
            request.Correo,
            request.Telefono,
            request.Contrasena);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }
}

