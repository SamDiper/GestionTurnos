using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Seguridad.Roles;
using GestionTurnos.Api.Application.CQRS.Seguridad.Usuarios;
using GestionTurnos.Api.Presentation.Contracts.Requests.Seguridad;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Seguridad;

/// <summary>
/// Módulo administrativo de Usuarios, Roles y Permisos (Sección 2 y 5 - RF-01).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/usuarios")]
public sealed class UsuariosController : BaseApiController
{
    public UsuariosController(ISender sender) : base(sender)
    {
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int? idRol,
        [FromQuery] string? estado,
        [FromQuery] string? busqueda,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListarUsuariosQuery(idRol, estado, busqueda, pageNumber, pageSize);
        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{idUsuario:long}")]
    public async Task<IActionResult> ObtenerPorId(long idUsuario, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ObtenerUsuarioPorIdQuery(idUsuario), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Crear(
        [FromBody] CrearUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CrearUsuarioCommand(request.Correo, request.Contrasena, request.IdRol);
        var result = await Sender.Send(command, cancellationToken);
        return HandleCreatedResult(result, nameof(ObtenerPorId), new { idUsuario = result.IsSuccess ? result.Value : 0 });
    }

    [HttpPut("{idUsuario:long}")]
    public async Task<IActionResult> Actualizar(
        long idUsuario,
        [FromBody] ActualizarUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ActualizarUsuarioCommand(idUsuario, request.Correo, request.IdRol, request.CodEstadoUsuario);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> ListarRoles(
        [FromQuery] bool soloActivos = false,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarRolesQuery(soloActivos), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CrearRol(
        [FromBody] GuardarRolRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GuardarRolCommand(null, request.Nombre, request.Descripcion, request.Activo, request.CodigosPermisos);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    [HttpPut("roles/{idRol:int}")]
    public async Task<IActionResult> ActualizarRol(
        int idRol,
        [FromBody] GuardarRolRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GuardarRolCommand(idRol, request.Nombre, request.Descripcion, request.Activo, request.CodigosPermisos);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("permisos")]
    public async Task<IActionResult> ListarPermisos(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ListarPermisosQuery(), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("permisos/inicializar")]
    public async Task<IActionResult> InicializarPermisosSistema(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new InicializarPermisosSistemaCommand(), cancellationToken);
        return HandleResult(result);
    }
}

