using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Seguridad;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Seguridad.Roles;

// =====================================================================
// ROLES Y PERMISOS (RF-01, Sección 2 y 5)
// =====================================================================

[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record ListarRolesQuery(bool SoloActivos = false) : IRequest<Result<IReadOnlyList<RolDto>>>;

public sealed class ListarRolesQueryHandler : IRequestHandler<ListarRolesQuery, Result<IReadOnlyList<RolDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarRolesQueryHandler(GestionTurnosContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<RolDto>>> Handle(ListarRolesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Rol> query = _context.Roles.AsNoTracking();

        if (request.SoloActivos)
        {
            query = query.Where(r => r.Activo);
        }

        var roles = await query
            .OrderBy(r => r.Nombre)
            .Select(r => new RolDto(
                r.IdRol,
                r.Nombre,
                r.Descripcion,
                r.Activo,
                r.RolPermisos.Select(rp => rp.Permiso.Codigo).ToList()))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<RolDto>>(roles);
    }
}

[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record ListarPermisosQuery : IRequest<Result<IReadOnlyList<PermisoDto>>>;

public sealed class ListarPermisosQueryHandler : IRequestHandler<ListarPermisosQuery, Result<IReadOnlyList<PermisoDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarPermisosQueryHandler(GestionTurnosContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<PermisoDto>>> Handle(ListarPermisosQuery request, CancellationToken cancellationToken)
    {
        var permisos = await _context.Permisos
            .AsNoTracking()
            .OrderBy(p => p.Codigo)
            .Select(p => new PermisoDto(p.IdPermiso, p.Codigo, p.Descripcion))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PermisoDto>>(permisos);
    }
}

[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record GuardarRolCommand(
    int? IdRol,
    string Nombre,
    string? Descripcion,
    bool Activo,
    IReadOnlyList<string> CodigosPermisos) : IRequest<Result<int>>;

public sealed class GuardarRolCommandValidator : AbstractValidator<GuardarRolCommand>
{
    public GuardarRolCommandValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre del rol es obligatorio.")
            .MaximumLength(50);

        RuleFor(x => x.Descripcion)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Descripcion));

        RuleFor(x => x.CodigosPermisos)
            .NotNull();
    }
}

public sealed class GuardarRolCommandHandler : IRequestHandler<GuardarRolCommand, Result<int>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public GuardarRolCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<int>> Handle(GuardarRolCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            Rol rol;
            if (request.IdRol.HasValue && request.IdRol.Value > 0)
            {
                var existente = await _context.Roles
                    .Include(r => r.RolPermisos)
                    .FirstOrDefaultAsync(r => r.IdRol == request.IdRol.Value, cancellationToken);

                if (existente is null)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<int>(AuthErrors.RolNoEncontrado);
                }

                existente.Nombre = request.Nombre.Trim();
                existente.Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
                existente.Activo = request.Activo;
                rol = existente;
            }
            else
            {
                rol = new Rol
                {
                    Nombre = request.Nombre.Trim(),
                    Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim(),
                    Activo = request.Activo
                };
                await _context.Roles.AddAsync(rol, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // Sincronizar permisos del rol en seguridad.rol_permiso
            var codigosSolicitados = request.CodigosPermisos
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            var permisosBd = await _context.Permisos
                .AsNoTracking()
                .Where(p => codigosSolicitados.Contains(p.Codigo.ToLower()))
                .ToListAsync(cancellationToken);

            var actuales = await _context.RolesPermisos
                .Where(rp => rp.IdRol == rol.IdRol)
                .ToListAsync(cancellationToken);

            var idsDeseados = permisosBd.Select(p => p.IdPermiso).ToHashSet();

            var aEliminar = actuales.Where(rp => !idsDeseados.Contains(rp.IdPermiso)).ToList();
            if (aEliminar.Count > 0)
            {
                _context.RolesPermisos.RemoveRange(aEliminar);
            }

            var idsActuales = actuales.Select(rp => rp.IdPermiso).ToHashSet();
            var aInsertar = idsDeseados
                .Where(id => !idsActuales.Contains(id))
                .Select(id => new RolPermiso { IdRol = rol.IdRol, IdPermiso = id })
                .ToList();

            if (aInsertar.Count > 0)
            {
                await _context.RolesPermisos.AddRangeAsync(aInsertar, cancellationToken);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success(rol.IdRol);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<int>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

/// <summary>
/// Sección 2: Carga en seguridad.permiso los códigos usados en el sistema y los asigna al rol Administrador en rol_permiso.
/// </summary>
[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record InicializarPermisosSistemaCommand : IRequest<Result<IReadOnlyList<PermisoDto>>>;

public sealed class InicializarPermisosSistemaCommandHandler
    : IRequestHandler<InicializarPermisosSistemaCommand, Result<IReadOnlyList<PermisoDto>>>
{
    private static readonly IReadOnlyList<(string Codigo, string Descripcion)> CatalogoPermisos = PermisosSistema.Catalogo;

    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public InicializarPermisosSistemaCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<PermisoDto>>> Handle(
        InicializarPermisosSistemaCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var existentes = await _context.Permisos.ToListAsync(cancellationToken);
            var codigosExistentes = existentes
                .Select(p => p.Codigo)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (codigo, descripcion) in CatalogoPermisos)
            {
                if (!codigosExistentes.Contains(codigo))
                {
                    var nuevo = new Permiso
                    {
                        Codigo = codigo,
                        Descripcion = descripcion
                    };
                    await _context.Permisos.AddAsync(nuevo, cancellationToken);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var todosPermisos = await _context.Permisos.AsNoTracking().ToListAsync(cancellationToken);
            var rolAdmin = await _context.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Nombre == "Administrador", cancellationToken);

            if (rolAdmin is not null)
            {
                var asignadosAdmin = await _context.RolesPermisos
                    .AsNoTracking()
                    .Where(rp => rp.IdRol == rolAdmin.IdRol)
                    .Select(rp => rp.IdPermiso)
                    .ToHashSetAsync(cancellationToken);

                foreach (var perm in todosPermisos)
                {
                    if (!asignadosAdmin.Contains(perm.IdPermiso))
                    {
                        await _context.RolesPermisos.AddAsync(new RolPermiso
                        {
                            IdRol = rolAdmin.IdRol,
                            IdPermiso = perm.IdPermiso
                        }, cancellationToken);
                    }
                }
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            var resultado = todosPermisos
                .OrderBy(p => p.Codigo)
                .Select(p => new PermisoDto(p.IdPermiso, p.Codigo, p.Descripcion))
                .ToList();

            return Result.Success<IReadOnlyList<PermisoDto>>(resultado);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<IReadOnlyList<PermisoDto>>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

