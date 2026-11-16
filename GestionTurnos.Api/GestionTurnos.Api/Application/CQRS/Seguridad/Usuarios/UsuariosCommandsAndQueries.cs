using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Seguridad;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Seguridad.Usuarios;

// =====================================================================
// LISTAR Y OBTENER USUARIOS (RF-01, Sección 5)
// =====================================================================

[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record ListarUsuariosQuery(
    int? IdRol = null,
    string? CodEstadoUsuario = null,
    string? Busqueda = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<UsuarioDto>>>;

public sealed class ListarUsuariosQueryHandler : IRequestHandler<ListarUsuariosQuery, Result<PagedResult<UsuarioDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarUsuariosQueryHandler(GestionTurnosContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResult<UsuarioDto>>> Handle(
        ListarUsuariosQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Math.Max(request.PageNumber, 1);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        IQueryable<Usuario> query = _context.Usuarios.AsNoTracking();

        if (request.IdRol.HasValue)
        {
            query = query.Where(u => u.IdRol == request.IdRol.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CodEstadoUsuario))
        {
            var estado = request.CodEstadoUsuario.Trim().ToUpperInvariant();
            query = query.Where(u => u.CodEstadoUsuario == estado);
        }

        if (!string.IsNullOrWhiteSpace(request.Busqueda))
        {
            var term = request.Busqueda.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.Correo.ToLower().Contains(term) ||
                (u.Empleado != null && (u.Empleado.Nombres.ToLower().Contains(term) ||
                                        u.Empleado.Apellidos.ToLower().Contains(term) ||
                                        u.Empleado.NumeroDocumento.ToLower().Contains(term))));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(u => u.CreadoEn)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UsuarioDto(
                u.IdUsuario,
                u.Correo,
                u.IdRol,
                u.Rol.Nombre,
                u.CodEstadoUsuario,
                u.EstadoUsuario.Nombre,
                u.UltimoAcceso,
                u.CreadoEn,
                u.Empleado != null ? u.Empleado.IdEmpleado : null,
                u.Empleado != null ? u.Empleado.Nombres + " " + u.Empleado.Apellidos : null))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<UsuarioDto>(items, totalCount, pageNumber, pageSize));
    }
}

[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record ObtenerUsuarioPorIdQuery(long IdUsuario) : IRequest<Result<UsuarioDto>>;

public sealed class ObtenerUsuarioPorIdQueryHandler : IRequestHandler<ObtenerUsuarioPorIdQuery, Result<UsuarioDto>>
{
    private readonly GestionTurnosContext _context;

    public ObtenerUsuarioPorIdQueryHandler(GestionTurnosContext context)
    {
        _context = context;
    }

    public async Task<Result<UsuarioDto>> Handle(ObtenerUsuarioPorIdQuery request, CancellationToken cancellationToken)
    {
        var usuario = await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.IdUsuario == request.IdUsuario)
            .Select(u => new UsuarioDto(
                u.IdUsuario,
                u.Correo,
                u.IdRol,
                u.Rol.Nombre,
                u.CodEstadoUsuario,
                u.EstadoUsuario.Nombre,
                u.UltimoAcceso,
                u.CreadoEn,
                u.Empleado != null ? u.Empleado.IdEmpleado : null,
                u.Empleado != null ? u.Empleado.Nombres + " " + u.Empleado.Apellidos : null))
            .FirstOrDefaultAsync(cancellationToken);

        if (usuario is null)
        {
            return Result.Failure<UsuarioDto>(AuthErrors.UsuarioNoEncontrado);
        }

        return Result.Success(usuario);
    }
}

// =====================================================================
// CREAR Y ACTUALIZAR USUARIO (RF-01, Sección 5 y 3.5)
// =====================================================================

[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record CrearUsuarioCommand(
    string Correo,
    string Contrasena,
    int IdRol) : IRequest<Result<long>>;

public sealed class CrearUsuarioCommandValidator : AbstractValidator<CrearUsuarioCommand>
{
    public CrearUsuarioCommandValidator()
    {
        RuleFor(x => x.Correo)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("El formato del correo electrónico no es válido.")
            .MaximumLength(150);

        RuleFor(x => x.Contrasena)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(72);

        RuleFor(x => x.IdRol).GreaterThan(0).WithMessage("El rol es obligatorio.");
    }
}

public sealed class CrearUsuarioCommandHandler : IRequestHandler<CrearUsuarioCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public CrearUsuarioCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<long>> Handle(CrearUsuarioCommand request, CancellationToken cancellationToken)
    {
        var correo = request.Correo.Trim().ToLowerInvariant();

        bool rolActivo = await _context.Roles
            .AsNoTracking()
            .AnyAsync(r => r.IdRol == request.IdRol && r.Activo, cancellationToken);

        if (!rolActivo)
        {
            return Result.Failure<long>(AuthErrors.RolNoEncontrado);
        }

        bool correoExiste = await _context.Usuarios
            .AsNoTracking()
            .AnyAsync(u => u.Correo.ToLower() == correo, cancellationToken);

        if (correoExiste)
        {
            return Result.Failure<long>(AuthErrors.CorreoYaRegistrado);
        }

        var nuevoUsuario = new Usuario
        {
            Correo = correo,
            HashContrasena = _passwordHasher.Hash(request.Contrasena),
            IdRol = request.IdRol,
            CodEstadoUsuario = EstadosUsuario.Activo,
            CreadoEn = DateTimeOffset.UtcNow
        };

        try
        {
            await _context.Usuarios.AddAsync(nuevoUsuario, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(nuevoUsuario.IdUsuario);
        }
        catch (Exception ex)
        {
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

/// <summary>
/// Permite al administrador actualizar correo de acceso (Sección 3.5), rol y estado (ACTIVO, INACTIVO, BLOQUEADO - Sección 5).
/// </summary>
[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record ActualizarUsuarioCommand(
    long IdUsuario,
    string Correo,
    int IdRol,
    string CodEstadoUsuario) : IRequest<Result>;

public sealed class ActualizarUsuarioCommandValidator : AbstractValidator<ActualizarUsuarioCommand>
{
    public ActualizarUsuarioCommandValidator()
    {
        RuleFor(x => x.IdUsuario).GreaterThan(0);

        RuleFor(x => x.Correo)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("El formato del correo electrónico no es válido.")
            .MaximumLength(150);

        RuleFor(x => x.IdRol).GreaterThan(0);

        RuleFor(x => x.CodEstadoUsuario)
            .NotEmpty()
            .Must(e => e is EstadosUsuario.Activo or EstadosUsuario.Inactivo or EstadosUsuario.Bloqueado)
            .WithMessage("El estado del usuario debe ser ACTIVO, INACTIVO o BLOQUEADO.");
    }
}

public sealed class ActualizarUsuarioCommandHandler : IRequestHandler<ActualizarUsuarioCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public ActualizarUsuarioCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ActualizarUsuarioCommand request, CancellationToken cancellationToken)
    {
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.IdUsuario == request.IdUsuario, cancellationToken);

        if (usuario is null)
        {
            return Result.Failure(AuthErrors.UsuarioNoEncontrado);
        }

        bool rolActivo = await _context.Roles
            .AsNoTracking()
            .AnyAsync(r => r.IdRol == request.IdRol && r.Activo, cancellationToken);

        if (!rolActivo)
        {
            return Result.Failure(AuthErrors.RolNoEncontrado);
        }

        var nuevoCorreo = request.Correo.Trim().ToLowerInvariant();
        if (!string.Equals(usuario.Correo, nuevoCorreo, StringComparison.OrdinalIgnoreCase))
        {
            bool correoEnUso = await _context.Usuarios
                .AsNoTracking()
                .AnyAsync(u => u.IdUsuario != request.IdUsuario && u.Correo.ToLower() == nuevoCorreo, cancellationToken);

            if (correoEnUso)
            {
                return Result.Failure(AuthErrors.CorreoYaRegistrado);
            }
        }

        usuario.Correo = nuevoCorreo;
        usuario.IdRol = request.IdRol;
        usuario.CodEstadoUsuario = request.CodEstadoUsuario.Trim().ToUpperInvariant();

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

