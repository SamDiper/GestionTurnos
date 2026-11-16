using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Organizacion;
using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Organizacion;

// =====================================================================
// SUCURSALES (organizacion.sucursal - Sección 5: filtrar activo = true para formularios)
// =====================================================================

public sealed record ListarSucursalesQuery(bool SoloActivos = true)
    : IRequest<Result<IReadOnlyList<SucursalDto>>>;

public sealed class ListarSucursalesQueryHandler
    : IRequestHandler<ListarSucursalesQuery, Result<IReadOnlyList<SucursalDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarSucursalesQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<SucursalDto>>> Handle(
        ListarSucursalesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Sucursales.AsNoTracking();
        if (request.SoloActivos)
        {
            query = query.Where(s => s.Activo);
        }

        var items = await query
            .OrderBy(s => s.Nombre)
            .Select(s => new SucursalDto(s.IdSucursal, s.Nombre, s.Direccion, s.Ciudad, s.Activo))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SucursalDto>>(items);
    }
}

[RequirePermission(PermisosSistema.OrganizacionGestionar)]
public sealed record GuardarSucursalCommand(
    int? IdSucursal,
    string Nombre,
    string? Direccion,
    string? Ciudad,
    bool Activo = true) : IRequest<Result<int>>;

public sealed class GuardarSucursalCommandValidator : AbstractValidator<GuardarSucursalCommand>
{
    public GuardarSucursalCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Direccion).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Direccion));
        RuleFor(x => x.Ciudad).MaximumLength(80).When(x => !string.IsNullOrWhiteSpace(x.Ciudad));
    }
}

public sealed class GuardarSucursalCommandHandler : IRequestHandler<GuardarSucursalCommand, Result<int>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public GuardarSucursalCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<int>> Handle(GuardarSucursalCommand request, CancellationToken cancellationToken)
    {
        Sucursal sucursal;
        if (request.IdSucursal.HasValue && request.IdSucursal.Value > 0)
        {
            var existente = await _context.Sucursales
                .FirstOrDefaultAsync(s => s.IdSucursal == request.IdSucursal.Value, cancellationToken);
            if (existente is null)
            {
                return Result.Failure<int>(new Error("Organizacion.SucursalNoEncontrada", "La sucursal no fue encontrada.", ErrorType.NotFound));
            }

            existente.Nombre = request.Nombre.Trim();
            existente.Direccion = string.IsNullOrWhiteSpace(request.Direccion) ? null : request.Direccion.Trim();
            existente.Ciudad = string.IsNullOrWhiteSpace(request.Ciudad) ? null : request.Ciudad.Trim();
            existente.Activo = request.Activo;
            sucursal = existente;
        }
        else
        {
            sucursal = new Sucursal
            {
                Nombre = request.Nombre.Trim(),
                Direccion = string.IsNullOrWhiteSpace(request.Direccion) ? null : request.Direccion.Trim(),
                Ciudad = string.IsNullOrWhiteSpace(request.Ciudad) ? null : request.Ciudad.Trim(),
                Activo = request.Activo
            };
            await _context.Sucursales.AddAsync(sucursal, cancellationToken);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(sucursal.IdSucursal);
        }
        catch (Exception ex)
        {
            return Result.Failure<int>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

// =====================================================================
// ÁREAS (organizacion.area)
// =====================================================================

public sealed record ListarAreasQuery(int? IdSucursal = null, bool SoloActivos = true)
    : IRequest<Result<IReadOnlyList<AreaDto>>>;

public sealed class ListarAreasQueryHandler
    : IRequestHandler<ListarAreasQuery, Result<IReadOnlyList<AreaDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarAreasQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<AreaDto>>> Handle(
        ListarAreasQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Areas.AsNoTracking();
        if (request.IdSucursal.HasValue)
        {
            query = query.Where(a => a.IdSucursal == request.IdSucursal.Value);
        }

        if (request.SoloActivos)
        {
            query = query.Where(a => a.Activo);
        }

        var items = await query
            .OrderBy(a => a.Sucursal.Nombre)
            .ThenBy(a => a.Nombre)
            .Select(a => new AreaDto(a.IdArea, a.IdSucursal, a.Sucursal.Nombre, a.Nombre, a.Activo))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AreaDto>>(items);
    }
}

[RequirePermission(PermisosSistema.OrganizacionGestionar)]
public sealed record GuardarAreaCommand(
    int? IdArea,
    int IdSucursal,
    string Nombre,
    bool Activo = true) : IRequest<Result<int>>;

public sealed class GuardarAreaCommandValidator : AbstractValidator<GuardarAreaCommand>
{
    public GuardarAreaCommandValidator()
    {
        RuleFor(x => x.IdSucursal).GreaterThan(0);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
    }
}

public sealed class GuardarAreaCommandHandler : IRequestHandler<GuardarAreaCommand, Result<int>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public GuardarAreaCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<int>> Handle(GuardarAreaCommand request, CancellationToken cancellationToken)
    {
        bool sucursalActiva = await _context.Sucursales
            .AsNoTracking()
            .AnyAsync(s => s.IdSucursal == request.IdSucursal && s.Activo, cancellationToken);

        if (!sucursalActiva)
        {
            return Result.Failure<int>(new Error(
                "Organizacion.SucursalNoEncontrada",
                "La sucursal seleccionada no existe o fue desactivada.",
                ErrorType.NotFound));
        }

        Area area;
        if (request.IdArea.HasValue && request.IdArea.Value > 0)
        {
            var existente = await _context.Areas
                .FirstOrDefaultAsync(a => a.IdArea == request.IdArea.Value, cancellationToken);
            if (existente is null)
            {
                return Result.Failure<int>(new Error("Organizacion.AreaNoEncontrada", "El área no fue encontrada.", ErrorType.NotFound));
            }

            existente.IdSucursal = request.IdSucursal;
            existente.Nombre = request.Nombre.Trim();
            existente.Activo = request.Activo;
            area = existente;
        }
        else
        {
            area = new Area
            {
                IdSucursal = request.IdSucursal,
                Nombre = request.Nombre.Trim(),
                Activo = request.Activo
            };
            await _context.Areas.AddAsync(area, cancellationToken);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(area.IdArea);
        }
        catch (Exception ex)
        {
            return Result.Failure<int>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

// =====================================================================
// CARGOS (organizacion.cargo)
// =====================================================================

public sealed record ListarCargosQuery(bool SoloActivos = true)
    : IRequest<Result<IReadOnlyList<CargoDto>>>;

public sealed class ListarCargosQueryHandler
    : IRequestHandler<ListarCargosQuery, Result<IReadOnlyList<CargoDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarCargosQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<CargoDto>>> Handle(
        ListarCargosQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Cargos.AsNoTracking();
        if (request.SoloActivos)
        {
            query = query.Where(c => c.Activo);
        }

        var items = await query
            .OrderBy(c => c.Nombre)
            .Select(c => new CargoDto(c.IdCargo, c.Nombre, c.Activo))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CargoDto>>(items);
    }
}

[RequirePermission(PermisosSistema.OrganizacionGestionar)]
public sealed record GuardarCargoCommand(
    int? IdCargo,
    string Nombre,
    bool Activo = true) : IRequest<Result<int>>;

public sealed class GuardarCargoCommandValidator : AbstractValidator<GuardarCargoCommand>
{
    public GuardarCargoCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
    }
}

public sealed class GuardarCargoCommandHandler : IRequestHandler<GuardarCargoCommand, Result<int>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public GuardarCargoCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<int>> Handle(GuardarCargoCommand request, CancellationToken cancellationToken)
    {
        Cargo cargo;
        if (request.IdCargo.HasValue && request.IdCargo.Value > 0)
        {
            var existente = await _context.Cargos
                .FirstOrDefaultAsync(c => c.IdCargo == request.IdCargo.Value, cancellationToken);
            if (existente is null)
            {
                return Result.Failure<int>(new Error("Organizacion.CargoNoEncontrado", "El cargo no fue encontrado.", ErrorType.NotFound));
            }

            existente.Nombre = request.Nombre.Trim();
            existente.Activo = request.Activo;
            cargo = existente;
        }
        else
        {
            cargo = new Cargo
            {
                Nombre = request.Nombre.Trim(),
                Activo = request.Activo
            };
            await _context.Cargos.AddAsync(cargo, cancellationToken);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(cargo.IdCargo);
        }
        catch (Exception ex)
        {
            return Result.Failure<int>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

