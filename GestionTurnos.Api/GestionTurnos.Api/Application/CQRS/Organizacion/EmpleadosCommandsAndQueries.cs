using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Organizacion;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Organizacion;

// =====================================================================
// GESTIÓN ADMINISTRATIVA DE EMPLEADOS (Sección 5 - RF-02, RG-06)
// =====================================================================

[RequirePermission(PermisosSistema.EmpleadosGestionar)]
public sealed record ListarEmpleadosQuery(
    int? IdArea = null,
    int? IdSucursal = null,
    int? IdCargo = null,
    string? CodEstadoEmpleado = null,
    string? Busqueda = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<EmpleadoDto>>>;

public sealed class ListarEmpleadosQueryHandler
    : IRequestHandler<ListarEmpleadosQuery, Result<PagedResult<EmpleadoDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarEmpleadosQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<EmpleadoDto>>> Handle(
        ListarEmpleadosQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Math.Max(request.PageNumber, 1);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        IQueryable<Empleado> query = _context.Empleados.AsNoTracking();

        if (request.IdArea.HasValue)
        {
            query = query.Where(e => e.IdArea == request.IdArea.Value);
        }

        if (request.IdSucursal.HasValue)
        {
            query = query.Where(e => e.Area.IdSucursal == request.IdSucursal.Value);
        }

        if (request.IdCargo.HasValue)
        {
            query = query.Where(e => e.IdCargo == request.IdCargo.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CodEstadoEmpleado))
        {
            var estado = request.CodEstadoEmpleado.Trim().ToUpperInvariant();
            query = query.Where(e => e.CodEstadoEmpleado == estado);
        }

        if (!string.IsNullOrWhiteSpace(request.Busqueda))
        {
            var term = request.Busqueda.Trim().ToLowerInvariant();
            query = query.Where(e =>
                e.NumeroDocumento.ToLower().Contains(term) ||
                e.Nombres.ToLower().Contains(term) ||
                e.Apellidos.ToLower().Contains(term) ||
                (e.CorreoContacto != null && e.CorreoContacto.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.Apellidos)
            .ThenBy(e => e.Nombres)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EmpleadoDto(
                e.IdEmpleado,
                e.IdUsuario,
                e.Usuario != null ? e.Usuario.Correo : null,
                e.CodTipoDocumento,
                e.TipoDocumento.Nombre,
                e.NumeroDocumento,
                e.Nombres,
                e.Apellidos,
                e.CorreoContacto,
                e.Telefono,
                e.Direccion,
                e.IdArea,
                e.Area.Nombre,
                e.Area.IdSucursal,
                e.Area.Sucursal.Nombre,
                e.IdCargo,
                e.Cargo.Nombre,
                e.CodTipoContrato,
                e.TipoContrato != null ? e.TipoContrato.Nombre : null,
                e.FechaIngreso,
                e.FechaRetiro,
                e.CodEstadoEmpleado,
                e.EstadoEmpleado.Nombre,
                e.CreadoEn))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<EmpleadoDto>(items, totalCount, pageNumber, pageSize));
    }
}

[RequirePermission(PermisosSistema.EmpleadosGestionar)]
public sealed record ObtenerEmpleadoPorIdQuery(long IdEmpleado) : IRequest<Result<EmpleadoDto>>;

public sealed class ObtenerEmpleadoPorIdQueryHandler : IRequestHandler<ObtenerEmpleadoPorIdQuery, Result<EmpleadoDto>>
{
    private readonly GestionTurnosContext _context;

    public ObtenerEmpleadoPorIdQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<EmpleadoDto>> Handle(ObtenerEmpleadoPorIdQuery request, CancellationToken cancellationToken)
    {
        var empleado = await _context.Empleados
            .AsNoTracking()
            .Where(e => e.IdEmpleado == request.IdEmpleado)
            .Select(e => new EmpleadoDto(
                e.IdEmpleado,
                e.IdUsuario,
                e.Usuario != null ? e.Usuario.Correo : null,
                e.CodTipoDocumento,
                e.TipoDocumento.Nombre,
                e.NumeroDocumento,
                e.Nombres,
                e.Apellidos,
                e.CorreoContacto,
                e.Telefono,
                e.Direccion,
                e.IdArea,
                e.Area.Nombre,
                e.Area.IdSucursal,
                e.Area.Sucursal.Nombre,
                e.IdCargo,
                e.Cargo.Nombre,
                e.CodTipoContrato,
                e.TipoContrato != null ? e.TipoContrato.Nombre : null,
                e.FechaIngreso,
                e.FechaRetiro,
                e.CodEstadoEmpleado,
                e.EstadoEmpleado.Nombre,
                e.CreadoEn))
            .FirstOrDefaultAsync(cancellationToken);

        if (empleado is null)
        {
            return Result.Failure<EmpleadoDto>(new Error(
                "Organizacion.EmpleadoNoEncontrado",
                "El empleado solicitado no fue encontrado.",
                ErrorType.NotFound));
        }

        return Result.Success(empleado);
    }
}

[RequirePermission(PermisosSistema.EmpleadosGestionar)]
public sealed record CrearEmpleadoCommand(
    string CodTipoDocumento,
    string NumeroDocumento,
    string Nombres,
    string Apellidos,
    string? CorreoContacto,
    string? Telefono,
    string? Direccion,
    int IdArea,
    int IdCargo,
    string? CodTipoContrato,
    DateOnly FechaIngreso,
    long? IdUsuarioExistente = null,
    string? CorreoAccesoNuevoUsuario = null,
    string? ContrasenaNuevoUsuario = null) : IRequest<Result<long>>;

public sealed class CrearEmpleadoCommandValidator : AbstractValidator<CrearEmpleadoCommand>
{
    public CrearEmpleadoCommandValidator()
    {
        RuleFor(x => x.CodTipoDocumento).NotEmpty().MaximumLength(10);
        RuleFor(x => x.NumeroDocumento).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Nombres).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Apellidos).NotEmpty().MaximumLength(80);
        RuleFor(x => x.CorreoContacto).EmailAddress().MaximumLength(150).When(x => !string.IsNullOrWhiteSpace(x.CorreoContacto));
        RuleFor(x => x.Telefono).MaximumLength(30).When(x => !string.IsNullOrWhiteSpace(x.Telefono));
        RuleFor(x => x.Direccion).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Direccion));
        RuleFor(x => x.IdArea).GreaterThan(0);
        RuleFor(x => x.IdCargo).GreaterThan(0);
        RuleFor(x => x.CodTipoContrato).MaximumLength(20).When(x => !string.IsNullOrWhiteSpace(x.CodTipoContrato));
        RuleFor(x => x.FechaIngreso).NotEmpty();

        RuleFor(x => x.CorreoAccesoNuevoUsuario)
            .EmailAddress().MaximumLength(150)
            .When(x => !string.IsNullOrWhiteSpace(x.CorreoAccesoNuevoUsuario));

        RuleFor(x => x.ContrasenaNuevoUsuario)
            .MinimumLength(8).MaximumLength(72)
            .When(x => !string.IsNullOrWhiteSpace(x.CorreoAccesoNuevoUsuario));
    }
}

public sealed class CrearEmpleadoCommandHandler : IRequestHandler<CrearEmpleadoCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public CrearEmpleadoCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<long>> Handle(CrearEmpleadoCommand request, CancellationToken cancellationToken)
    {
        var codTipoDoc = request.CodTipoDocumento.Trim().ToUpperInvariant();
        var numDoc = request.NumeroDocumento.Trim();

        bool areaActiva = await _context.Areas
            .AsNoTracking()
            .AnyAsync(a => a.IdArea == request.IdArea && a.Activo, cancellationToken);
        if (!areaActiva) return Result.Failure<long>(AuthErrors.AreaNoEncontrada);

        bool cargoActivo = await _context.Cargos
            .AsNoTracking()
            .AnyAsync(c => c.IdCargo == request.IdCargo && c.Activo, cancellationToken);
        if (!cargoActivo) return Result.Failure<long>(AuthErrors.CargoNoEncontrado);

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            long? idUsuario = request.IdUsuarioExistente;
            if (!idUsuario.HasValue && !string.IsNullOrWhiteSpace(request.CorreoAccesoNuevoUsuario) && !string.IsNullOrWhiteSpace(request.ContrasenaNuevoUsuario))
            {
                var rolEmpleado = await _context.Roles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Nombre == "Empleado" && r.Activo, cancellationToken);

                if (rolEmpleado is null)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<long>(AuthErrors.RolEmpleadoNoConfigurado);
                }

                var nuevoUsuario = new Usuario
                {
                    Correo = request.CorreoAccesoNuevoUsuario.Trim().ToLowerInvariant(),
                    HashContrasena = _passwordHasher.Hash(request.ContrasenaNuevoUsuario),
                    IdRol = rolEmpleado.IdRol,
                    CodEstadoUsuario = EstadosUsuario.Activo,
                    CreadoEn = DateTimeOffset.UtcNow
                };

                await _context.Usuarios.AddAsync(nuevoUsuario, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                idUsuario = nuevoUsuario.IdUsuario;
            }

            var empleado = new Empleado
            {
                IdUsuario = idUsuario,
                CodTipoDocumento = codTipoDoc,
                NumeroDocumento = numDoc,
                Nombres = request.Nombres.Trim(),
                Apellidos = request.Apellidos.Trim(),
                CorreoContacto = string.IsNullOrWhiteSpace(request.CorreoContacto) ? null : request.CorreoContacto.Trim(),
                Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim(),
                Direccion = string.IsNullOrWhiteSpace(request.Direccion) ? null : request.Direccion.Trim(),
                IdArea = request.IdArea,
                IdCargo = request.IdCargo,
                CodTipoContrato = string.IsNullOrWhiteSpace(request.CodTipoContrato) ? null : request.CodTipoContrato.Trim().ToUpperInvariant(),
                FechaIngreso = request.FechaIngreso,
                CodEstadoEmpleado = EstadosEmpleado.Activo,
                CreadoEn = DateTimeOffset.UtcNow
            };

            await _context.Empleados.AddAsync(empleado, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result.Success(empleado.IdEmpleado);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

[RequirePermission(PermisosSistema.EmpleadosGestionar)]
public sealed record ActualizarEmpleadoCommand(
    long IdEmpleado,
    string CodTipoDocumento,
    string NumeroDocumento,
    string Nombres,
    string Apellidos,
    string? CorreoContacto,
    string? Telefono,
    string? Direccion,
    int IdArea,
    int IdCargo,
    string? CodTipoContrato,
    DateOnly FechaIngreso) : IRequest<Result>;

public sealed class ActualizarEmpleadoCommandValidator : AbstractValidator<ActualizarEmpleadoCommand>
{
    public ActualizarEmpleadoCommandValidator()
    {
        RuleFor(x => x.IdEmpleado).GreaterThan(0);
        RuleFor(x => x.CodTipoDocumento).NotEmpty().MaximumLength(10);
        RuleFor(x => x.NumeroDocumento).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Nombres).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Apellidos).NotEmpty().MaximumLength(80);
        RuleFor(x => x.CorreoContacto).EmailAddress().MaximumLength(150).When(x => !string.IsNullOrWhiteSpace(x.CorreoContacto));
        RuleFor(x => x.Telefono).MaximumLength(30).When(x => !string.IsNullOrWhiteSpace(x.Telefono));
        RuleFor(x => x.Direccion).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Direccion));
        RuleFor(x => x.IdArea).GreaterThan(0);
        RuleFor(x => x.IdCargo).GreaterThan(0);
        RuleFor(x => x.FechaIngreso).NotEmpty();
    }
}

public sealed class ActualizarEmpleadoCommandHandler : IRequestHandler<ActualizarEmpleadoCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public ActualizarEmpleadoCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ActualizarEmpleadoCommand request, CancellationToken cancellationToken)
    {
        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.IdEmpleado == request.IdEmpleado, cancellationToken);

        if (empleado is null)
        {
            return Result.Failure(new Error("Organizacion.EmpleadoNoEncontrado", "El empleado no fue encontrado.", ErrorType.NotFound));
        }

        bool areaActiva = await _context.Areas.AsNoTracking().AnyAsync(a => a.IdArea == request.IdArea && a.Activo, cancellationToken);
        if (!areaActiva) return Result.Failure(AuthErrors.AreaNoEncontrada);

        bool cargoActivo = await _context.Cargos.AsNoTracking().AnyAsync(c => c.IdCargo == request.IdCargo && c.Activo, cancellationToken);
        if (!cargoActivo) return Result.Failure(AuthErrors.CargoNoEncontrado);

        empleado.CodTipoDocumento = request.CodTipoDocumento.Trim().ToUpperInvariant();
        empleado.NumeroDocumento = request.NumeroDocumento.Trim();
        empleado.Nombres = request.Nombres.Trim();
        empleado.Apellidos = request.Apellidos.Trim();
        empleado.CorreoContacto = string.IsNullOrWhiteSpace(request.CorreoContacto) ? null : request.CorreoContacto.Trim();
        empleado.Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim();
        empleado.Direccion = string.IsNullOrWhiteSpace(request.Direccion) ? null : request.Direccion.Trim();
        empleado.IdArea = request.IdArea;
        empleado.IdCargo = request.IdCargo;
        empleado.CodTipoContrato = string.IsNullOrWhiteSpace(request.CodTipoContrato) ? null : request.CodTipoContrato.Trim().ToUpperInvariant();
        empleado.FechaIngreso = request.FechaIngreso;

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

/// <summary>
/// Sección 5 (RF-02, RG-06): Desactivar empleado.
/// Desactivar = cod_estado_empleado = 'INACTIVO', fecha_retiro y, en la misma transacción,
/// usuario.cod_estado_usuario = 'INACTIVO'. Nunca borrar.
/// </summary>
[RequirePermission(PermisosSistema.EmpleadosGestionar)]
public sealed record DesactivarEmpleadoCommand(
    long IdEmpleado,
    DateOnly FechaRetiro) : IRequest<Result>;

public sealed class DesactivarEmpleadoCommandValidator : AbstractValidator<DesactivarEmpleadoCommand>
{
    public DesactivarEmpleadoCommandValidator()
    {
        RuleFor(x => x.IdEmpleado).GreaterThan(0);
        RuleFor(x => x.FechaRetiro).NotEmpty().WithMessage("La fecha de retiro es obligatoria al desactivar un empleado.");
    }
}

public sealed class DesactivarEmpleadoCommandHandler : IRequestHandler<DesactivarEmpleadoCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public DesactivarEmpleadoCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DesactivarEmpleadoCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var empleado = await _context.Empleados
                .Include(e => e.Usuario)
                .FirstOrDefaultAsync(e => e.IdEmpleado == request.IdEmpleado, cancellationToken);

            if (empleado is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(new Error("Organizacion.EmpleadoNoEncontrado", "El empleado no fue encontrado.", ErrorType.NotFound));
            }

            if (request.FechaRetiro < empleado.FechaIngreso)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(new Error(
                    "Organizacion.FechaRetiroInvalida",
                    "La fecha de retiro no puede ser anterior a la fecha de ingreso del empleado.",
                    ErrorType.Failure));
            }

            empleado.CodEstadoEmpleado = EstadosEmpleado.Inactivo;
            empleado.FechaRetiro = request.FechaRetiro;

            if (empleado.Usuario is not null)
            {
                empleado.Usuario.CodEstadoUsuario = EstadosUsuario.Inactivo;
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

