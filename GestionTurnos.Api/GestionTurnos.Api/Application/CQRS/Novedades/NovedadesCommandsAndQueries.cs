using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Novedades;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Novedades;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Novedades;

// =====================================================================
// 3.8 SOLICITAR PERMISO O REPORTAR NOVEDAD (EMPLEADO - RF-07)
// =====================================================================

/// <summary>
/// Sección 3.8:
/// 1. El empleado solo puede usar INCAPACIDAD, CALAMIDAD y PERMISO_PERSONAL; las ausencias las registra el administrador.
/// 2. Si requiere_soporte es verdadero, exige url_soporte.
/// 3. Copia remunerado_defecto y afecta_horas_defecto a remunerado y afecta_horas.
/// 4. Las novedades creadas por el empleado siempre nacen PENDIENTE.
/// </summary>
public sealed record SolicitarMiNovedadCommand(
    string CodTipoNovedad,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    string? Observaciones,
    string? UrlSoporte) : IRequest<Result<long>>, IRequireAuthentication;

public sealed class SolicitarMiNovedadCommandValidator : AbstractValidator<SolicitarMiNovedadCommand>
{
    public SolicitarMiNovedadCommandValidator()
    {
        RuleFor(x => x.CodTipoNovedad)
            .NotEmpty()
            .MaximumLength(30)
            .Must(c => TiposNovedadCodigos.PermitidosParaEmpleado.Contains(c.Trim()))
            .WithMessage("El empleado solo puede solicitar INCAPACIDAD, CALAMIDAD o PERMISO_PERSONAL.");

        RuleFor(x => x.Inicio).NotEmpty();
        RuleFor(x => x.Fin)
            .NotEmpty()
            .GreaterThan(x => x.Inicio)
            .WithMessage("La fecha/hora de fin debe ser posterior a la de inicio.");

        RuleFor(x => x.UrlSoporte)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.UrlSoporte));
    }
}

public sealed class SolicitarMiNovedadCommandHandler : IRequestHandler<SolicitarMiNovedadCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public SolicitarMiNovedadCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<long>> Handle(SolicitarMiNovedadCommand request, CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<long>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;
        var idUsuario = _currentUserService.UserId!.Value;
        var codTipo = request.CodTipoNovedad.Trim().ToUpperInvariant();

        // 1. Lee el tipo en parametrizacion.tipo_novedad (debe estar activo)
        var tipoNovedad = await _context.TiposNovedad
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.CodTipoNovedad == codTipo && t.Activo, cancellationToken);

        if (tipoNovedad is null)
        {
            return Result.Failure<long>(new Error(
                "Novedades.TipoNovedadInvalido",
                "El tipo de novedad seleccionado no existe o fue desactivado.",
                ErrorType.NotFound));
        }

        // 2. Si requiere_soporte es verdadero, exige url_soporte
        if (tipoNovedad.RequiereSoporte && string.IsNullOrWhiteSpace(request.UrlSoporte))
        {
            return Result.Failure<long>(new Error(
                "Novedades.SoporteRequerido",
                $"El tipo de novedad '{tipoNovedad.Nombre}' exige adjuntar la URL del soporte documental.",
                ErrorType.Failure));
        }

        // 3 y 4. Copia remunerado_defecto y afecta_horas_defecto; nace siempre PENDIENTE
        var novedad = new Novedad
        {
            IdEmpleado = idEmpleado,
            CodTipoNovedad = codTipo,
            Inicio = request.Inicio,
            Fin = request.Fin,
            Remunerado = tipoNovedad.RemuneradoDefecto,
            AfectaHoras = tipoNovedad.AfectaHorasDefecto,
            CodEstadoSolicitud = EstadosSolicitud.Pendiente,
            Observaciones = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim(),
            UrlSoporte = string.IsNullOrWhiteSpace(request.UrlSoporte) ? null : request.UrlSoporte.Trim(),
            RegistradoPor = idUsuario,
            RegistradoEn = DateTimeOffset.UtcNow
        };

        try
        {
            await _context.Novedades.AddAsync(novedad, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(novedad.IdNovedad);
        }
        catch (Exception ex)
        {
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

/// <summary>
/// Sección 3.8 punto 5: Cancelación de novedad por el empleado (solo PENDIENTE y filtrando por id_empleado).
/// </summary>
public sealed record CancelarMiNovedadCommand(long IdNovedad) : IRequest<Result>, IRequireAuthentication;

public sealed class CancelarMiNovedadCommandHandler : IRequestHandler<CancelarMiNovedadCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public CancelarMiNovedadCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result> Handle(CancelarMiNovedadCommand request, CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var novedad = await _context.Novedades
            .FirstOrDefaultAsync(
                n => n.IdNovedad == request.IdNovedad &&
                     n.IdEmpleado == idEmpleado &&
                     n.CodEstadoSolicitud == EstadosSolicitud.Pendiente,
                cancellationToken);

        if (novedad is null)
        {
            return Result.Failure(new Error(
                "Novedades.NovedadNoEncontrada",
                "La novedad no fue encontrada o ya no se encuentra pendiente.",
                ErrorType.NotFound));
        }

        novedad.CodEstadoSolicitud = EstadosSolicitud.Cancelada;

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

public sealed record ObtenerMisNovedadesQuery : IRequest<Result<IReadOnlyList<NovedadDto>>>, IRequireAuthentication;

public sealed class ObtenerMisNovedadesQueryHandler
    : IRequestHandler<ObtenerMisNovedadesQuery, Result<IReadOnlyList<NovedadDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public ObtenerMisNovedadesQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<IReadOnlyList<NovedadDto>>> Handle(
        ObtenerMisNovedadesQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<NovedadDto>>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var items = await _context.Novedades
            .AsNoTracking()
            .Where(n => n.IdEmpleado == idEmpleado)
            .OrderByDescending(n => n.RegistradoEn)
            .Select(n => new NovedadDto(
                n.IdNovedad,
                n.IdEmpleado,
                n.Empleado.Nombres + " " + n.Empleado.Apellidos,
                n.CodTipoNovedad,
                n.TipoNovedad.Nombre,
                n.Inicio,
                n.Fin,
                n.Remunerado,
                n.AfectaHoras,
                n.CodEstadoSolicitud,
                n.EstadoSolicitud.Nombre,
                n.Observaciones,
                n.UrlSoporte,
                n.RegistradoPor,
                n.RegistradoEn,
                n.ResueltoPor,
                n.ResueltoEn,
                n.MotivoRechazo))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<NovedadDto>>(items);
    }
}

// =====================================================================
// 3.8 y 4. REGISTRO Y RESOLUCIÓN DE NOVEDADES POR ADMINISTRADOR (permiso: novedades.aprobar)
// =====================================================================

[RequirePermission(PermisosSistema.NovedadesAprobar)]
public sealed record RegistrarNovedadAdminCommand(
    long IdEmpleado,
    string CodTipoNovedad,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    bool? Remunerado,
    bool? AfectaHoras,
    string? Observaciones,
    string? UrlSoporte) : IRequest<Result<long>>;

public sealed class RegistrarNovedadAdminCommandValidator : AbstractValidator<RegistrarNovedadAdminCommand>
{
    public RegistrarNovedadAdminCommandValidator()
    {
        RuleFor(x => x.IdEmpleado).GreaterThan(0);
        RuleFor(x => x.CodTipoNovedad).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Inicio).NotEmpty();
        RuleFor(x => x.Fin).GreaterThan(x => x.Inicio);
        RuleFor(x => x.UrlSoporte).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.UrlSoporte));
    }
}

public sealed class RegistrarNovedadAdminCommandHandler : IRequestHandler<RegistrarNovedadAdminCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public RegistrarNovedadAdminCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<long>> Handle(RegistrarNovedadAdminCommand request, CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId!.Value;
        var codTipo = request.CodTipoNovedad.Trim().ToUpperInvariant();

        var tipoNovedad = await _context.TiposNovedad
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.CodTipoNovedad == codTipo && t.Activo, cancellationToken);

        if (tipoNovedad is null)
        {
            return Result.Failure<long>(new Error(
                "Novedades.TipoNovedadInvalido",
                "El tipo de novedad seleccionado no existe o fue desactivado.",
                ErrorType.NotFound));
        }

        if (tipoNovedad.RequiereSoporte && string.IsNullOrWhiteSpace(request.UrlSoporte))
        {
            return Result.Failure<long>(new Error(
                "Novedades.SoporteRequerido",
                $"El tipo de novedad '{tipoNovedad.Nombre}' exige adjuntar la URL del soporte.",
                ErrorType.Failure));
        }

        var ahora = DateTimeOffset.UtcNow;
        var novedad = new Novedad
        {
            IdEmpleado = request.IdEmpleado,
            CodTipoNovedad = codTipo,
            Inicio = request.Inicio,
            Fin = request.Fin,
            Remunerado = request.Remunerado ?? tipoNovedad.RemuneradoDefecto,
            AfectaHoras = request.AfectaHoras ?? tipoNovedad.AfectaHorasDefecto,
            CodEstadoSolicitud = EstadosSolicitud.Aprobada,
            Observaciones = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim(),
            UrlSoporte = string.IsNullOrWhiteSpace(request.UrlSoporte) ? null : request.UrlSoporte.Trim(),
            RegistradoPor = adminId,
            RegistradoEn = ahora,
            ResueltoPor = adminId,
            ResueltoEn = ahora
        };

        try
        {
            await _context.Novedades.AddAsync(novedad, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(novedad.IdNovedad);
        }
        catch (Exception ex)
        {
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

/// <summary>
/// Sección 4: Resolución administrativa de novedad (APROBADA o RECHAZADA).
/// Evalúa CONFLICTO_BLOQUEA frente a turnos programados en el intervalo.
/// </summary>
[RequirePermission(PermisosSistema.NovedadesAprobar)]
public sealed record ResolverNovedadCommand(
    long IdNovedad,
    bool Aprobar,
    string? MotivoRechazo = null,
    bool ConfirmarAnulacionTurnos = false) : IRequest<Result>;

public sealed class ResolverNovedadCommandValidator : AbstractValidator<ResolverNovedadCommand>
{
    public ResolverNovedadCommandValidator()
    {
        RuleFor(x => x.IdNovedad).GreaterThan(0);
        RuleFor(x => x.MotivoRechazo)
            .NotEmpty().WithMessage("El motivo de rechazo es obligatorio al rechazar una novedad.")
            .MaximumLength(300)
            .When(x => !x.Aprobar);
    }
}

public sealed class ResolverNovedadCommandHandler : IRequestHandler<ResolverNovedadCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IParametroService _parametroService;

    public ResolverNovedadCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IParametroService parametroService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _parametroService = parametroService;
    }

    public async Task<Result> Handle(ResolverNovedadCommand request, CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId!.Value;

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var novedad = await _context.Novedades
                .FirstOrDefaultAsync(
                    n => n.IdNovedad == request.IdNovedad &&
                         n.CodEstadoSolicitud == EstadosSolicitud.Pendiente,
                    cancellationToken);

            if (novedad is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(new Error(
                    "Novedades.NovedadNoPendiente",
                    "La novedad no existe o ya fue resuelta.",
                    ErrorType.NotFound));
            }

            if (request.Aprobar)
            {
                var fechaInicioRef = DateOnly.FromDateTime(novedad.Inicio.UtcDateTime);
                var fechaFinRef = DateOnly.FromDateTime(novedad.Fin.UtcDateTime);

                var candidatos = await _context.Asignaciones
                    .Where(a => a.IdEmpleado == novedad.IdEmpleado &&
                                a.CodEstadoAsignacion != EstadosAsignacion.Anulada &&
                                a.Fecha >= fechaInicioRef.AddDays(-1) &&
                                a.Fecha <= fechaFinRef.AddDays(1))
                    .ToListAsync(cancellationToken);

                var turnosEnConflicto = candidatos
                    .Where(a => a.Periodo.SeCruzaCon(novedad.Inicio, novedad.Fin))
                    .ToList();

                if (turnosEnConflicto.Count > 0)
                {
                    bool conflictoBloquea = await _parametroService.ObtenerBooleanoAsync(
                        ParametrosCodigos.ConflictoBloquea, fechaInicioRef, cancellationToken);

                    var fechasStr = string.Join(", ", turnosEnConflicto.Select(t => t.Fecha.ToString("yyyy-MM-dd")).Distinct());

                    if (conflictoBloquea)
                    {
                        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                        return Result.Failure(new Error(
                            "Novedades.ConflictoConTurnos",
                            $"No se puede aprobar porque el empleado tiene turnos programados que se cruzan en las fechas: {fechasStr} (parámetro CONFLICTO_BLOQUEA activo).",
                            ErrorType.Conflict));
                    }

                    if (!request.ConfirmarAnulacionTurnos)
                    {
                        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                        return Result.Failure(new Error(
                            "Novedades.ConfirmacionRequerida",
                            $"El empleado tiene turnos programados que se cruzan en las fechas: {fechasStr}. Confirme la aprobación para anular esas asignaciones.",
                            ErrorType.Conflict));
                    }

                    foreach (var turno in turnosEnConflicto)
                    {
                        turno.CodEstadoAsignacion = EstadosAsignacion.Anulada;
                    }
                }

                novedad.CodEstadoSolicitud = EstadosSolicitud.Aprobada;
                novedad.ResueltoPor = adminId;
                novedad.ResueltoEn = DateTimeOffset.UtcNow;
                novedad.MotivoRechazo = null;
            }
            else
            {
                novedad.CodEstadoSolicitud = EstadosSolicitud.Rechazada;
                novedad.ResueltoPor = adminId;
                novedad.ResueltoEn = DateTimeOffset.UtcNow;
                novedad.MotivoRechazo = request.MotivoRechazo!.Trim();
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

[RequirePermission(PermisosSistema.VacacionesAprobar)]
public sealed record ListarSolicitudesVacacionesAdminQuery(
    long? IdEmpleado = null,
    string? CodEstadoSolicitud = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<SolicitudVacacionesDto>>>;

public sealed class ListarSolicitudesVacacionesAdminQueryHandler
    : IRequestHandler<ListarSolicitudesVacacionesAdminQuery, Result<PagedResult<SolicitudVacacionesDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarSolicitudesVacacionesAdminQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<SolicitudVacacionesDto>>> Handle(
        ListarSolicitudesVacacionesAdminQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Math.Max(request.PageNumber, 1);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        var query = _context.SolicitudesVacaciones.AsNoTracking();
        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(s => s.IdEmpleado == request.IdEmpleado.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CodEstadoSolicitud))
        {
            var estado = request.CodEstadoSolicitud.Trim().ToUpperInvariant();
            query = query.Where(s => s.CodEstadoSolicitud == estado);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(s => s.SolicitadoEn)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SolicitudVacacionesDto(
                s.IdSolicitudVacaciones,
                s.IdEmpleado,
                s.Empleado.Nombres + " " + s.Empleado.Apellidos,
                s.FechaInicio,
                s.FechaFin,
                s.DiasSolicitados,
                s.CodEstadoSolicitud,
                s.EstadoSolicitud.Nombre,
                s.Observacion,
                s.SolicitadoPor,
                s.SolicitadoEn,
                s.ResueltoPor,
                s.ResueltoEn,
                s.MotivoRechazo))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<SolicitudVacacionesDto>(items, totalCount, pageNumber, pageSize));
    }
}

[RequirePermission(PermisosSistema.NovedadesAprobar)]
public sealed record ListarNovedadesAdminQuery(
    long? IdEmpleado = null,
    string? CodEstadoSolicitud = null,
    string? CodTipoNovedad = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<NovedadDto>>>;

public sealed class ListarNovedadesAdminQueryHandler
    : IRequestHandler<ListarNovedadesAdminQuery, Result<PagedResult<NovedadDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarNovedadesAdminQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<NovedadDto>>> Handle(
        ListarNovedadesAdminQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Math.Max(request.PageNumber, 1);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        var query = _context.Novedades.AsNoTracking();
        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(n => n.IdEmpleado == request.IdEmpleado.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CodEstadoSolicitud))
        {
            var estado = request.CodEstadoSolicitud.Trim().ToUpperInvariant();
            query = query.Where(n => n.CodEstadoSolicitud == estado);
        }

        if (!string.IsNullOrWhiteSpace(request.CodTipoNovedad))
        {
            var tipo = request.CodTipoNovedad.Trim().ToUpperInvariant();
            query = query.Where(n => n.CodTipoNovedad == tipo);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(n => n.RegistradoEn)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NovedadDto(
                n.IdNovedad,
                n.IdEmpleado,
                n.Empleado.Nombres + " " + n.Empleado.Apellidos,
                n.CodTipoNovedad,
                n.TipoNovedad.Nombre,
                n.Inicio,
                n.Fin,
                n.Remunerado,
                n.AfectaHoras,
                n.CodEstadoSolicitud,
                n.EstadoSolicitud.Nombre,
                n.Observaciones,
                n.UrlSoporte,
                n.RegistradoPor,
                n.RegistradoEn,
                n.ResueltoPor,
                n.ResueltoEn,
                n.MotivoRechazo))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<NovedadDto>(items, totalCount, pageNumber, pageSize));
    }
}
