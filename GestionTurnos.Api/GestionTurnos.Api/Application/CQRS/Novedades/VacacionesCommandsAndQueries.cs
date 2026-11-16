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
// 3.4 SALDO DE VACACIONES DEL EMPLEADO (novedades.v_saldo_vacaciones - RF-06)
// =====================================================================

public sealed record ObtenerMiSaldoVacacionesQuery : IRequest<Result<SaldoVacacionesDto>>, IRequireAuthentication;

public sealed class ObtenerMiSaldoVacacionesQueryHandler
    : IRequestHandler<ObtenerMiSaldoVacacionesQuery, Result<SaldoVacacionesDto>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public ObtenerMiSaldoVacacionesQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<SaldoVacacionesDto>> Handle(
        ObtenerMiSaldoVacacionesQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<SaldoVacacionesDto>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var vista = await _context.VistaSaldosVacaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.IdEmpleado == idEmpleado, cancellationToken);

        if (vista is null)
        {
            return Result.Success(new SaldoVacacionesDto(idEmpleado, 0m, 0m, 0m, 0m, 0m));
        }

        return Result.Success(new SaldoVacacionesDto(
            vista.IdEmpleado,
            vista.DiasOtorgados,
            vista.DiasUtilizados,
            vista.DiasEnSolicitud,
            vista.DiasDisponibles,
            vista.DiasDisponibles - vista.DiasEnSolicitud));
    }
}

// =====================================================================
// 3.7 SOLICITAR Y CANCELAR VACACIONES (EMPLEADO - CU-03)
// =====================================================================

public sealed record SolicitarMisVacacionesCommand(
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string? Observacion) : IRequest<Result<ResultadoSolicitudVacacionesDto>>, IRequireAuthentication;

public sealed class SolicitarMisVacacionesCommandValidator : AbstractValidator<SolicitarMisVacacionesCommand>
{
    public SolicitarMisVacacionesCommandValidator()
    {
        RuleFor(x => x.FechaInicio).NotEmpty();
        RuleFor(x => x.FechaFin)
            .NotEmpty()
            .GreaterThanOrEqualTo(x => x.FechaInicio)
            .WithMessage("La fecha de fin debe ser mayor o igual a la fecha de inicio.");
        RuleFor(x => x.Observacion).MaximumLength(300).When(x => !string.IsNullOrWhiteSpace(x.Observacion));
    }
}

public sealed class SolicitarMisVacacionesCommandHandler
    : IRequestHandler<SolicitarMisVacacionesCommand, Result<ResultadoSolicitudVacacionesDto>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmpleadoSesionService _empleadoSesionService;
    private readonly IDiasVacacionesCalculator _diasCalculator;

    public SolicitarMisVacacionesCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IEmpleadoSesionService empleadoSesionService,
        IDiasVacacionesCalculator diasCalculator)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _empleadoSesionService = empleadoSesionService;
        _diasCalculator = diasCalculator;
    }

    public async Task<Result<ResultadoSolicitudVacacionesDto>> Handle(
        SolicitarMisVacacionesCommand request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<ResultadoSolicitudVacacionesDto>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;
        var idUsuario = _currentUserService.UserId!.Value;

        // 1. Calcula dias_solicitados en el backend excluyendo festivos y fines de semana (Sección 3.7 punto 1)
        decimal diasSolicitados = await _diasCalculator.CalcularDiasSolicitadosAsync(
            request.FechaInicio, request.FechaFin, cancellationToken);

        if (diasSolicitados <= 0)
        {
            return Result.Failure<ResultadoSolicitudVacacionesDto>(new Error(
                "Vacaciones.SinDiasHabiles",
                "El rango de fechas seleccionado no contiene días hábiles laborables.",
                ErrorType.Failure));
        }

        // 2. Lee novedades.v_saldo_vacaciones y exige dias_disponibles - dias_en_solicitud >= dias_solicitados
        var saldo = await _context.VistaSaldosVacaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.IdEmpleado == idEmpleado, cancellationToken);

        decimal saldoEfectivo = (saldo?.DiasDisponibles ?? 0m) - (saldo?.DiasEnSolicitud ?? 0m);
        if (saldoEfectivo < diasSolicitados)
        {
            return Result.Failure<ResultadoSolicitudVacacionesDto>(new Error(
                "Vacaciones.SaldoInsuficiente",
                $"Saldo de vacaciones insuficiente. Días requeridos: {diasSolicitados:0.##}, saldo disponible descontando solicitudes en curso: {saldoEfectivo:0.##}.",
                ErrorType.Conflict));
        }

        // Validar cruce con solicitudes vigentes (ex_vacaciones_sin_cruce)
        bool existeCruce = await _context.SolicitudesVacaciones
            .AsNoTracking()
            .AnyAsync(v => v.IdEmpleado == idEmpleado &&
                           (v.CodEstadoSolicitud == EstadosSolicitud.Pendiente ||
                            v.CodEstadoSolicitud == EstadosSolicitud.Aprobada) &&
                           request.FechaInicio <= v.FechaFin &&
                           v.FechaInicio <= request.FechaFin, cancellationToken);

        if (existeCruce)
        {
            return Result.Failure<ResultadoSolicitudVacacionesDto>(new Error(
                "Vacaciones.CruceFechas",
                "Ya existe una solicitud de vacaciones en esas fechas.",
                ErrorType.Conflict));
        }

        // 3. Inserta en novedades.solicitud_vacaciones
        var solicitud = new SolicitudVacaciones
        {
            IdEmpleado = idEmpleado,
            FechaInicio = request.FechaInicio,
            FechaFin = request.FechaFin,
            DiasSolicitados = diasSolicitados,
            CodEstadoSolicitud = EstadosSolicitud.Pendiente,
            Observacion = string.IsNullOrWhiteSpace(request.Observacion) ? null : request.Observacion.Trim(),
            SolicitadoPor = idUsuario,
            SolicitadoEn = DateTimeOffset.UtcNow
        };

        try
        {
            await _context.SolicitudesVacaciones.AddAsync(solicitud, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 4. Informa (sin bloquear) si existen turnos programados en ese rango consultando turnos.asignacion
            var fechasTurnosEnRango = await _context.Asignaciones
                .AsNoTracking()
                .Where(a => a.IdEmpleado == idEmpleado &&
                            a.CodEstadoAsignacion != EstadosAsignacion.Anulada &&
                            a.Fecha >= request.FechaInicio &&
                            a.Fecha <= request.FechaFin)
                .OrderBy(a => a.Fecha)
                .Select(a => a.Fecha)
                .Distinct()
                .ToListAsync(cancellationToken);

            string? advertencia = fechasTurnosEnRango.Count > 0
                ? $"Advertencia: existen {fechasTurnosEnRango.Count} día(s) con turnos programados en el rango solicitado."
                : null;

            return Result.Success(new ResultadoSolicitudVacacionesDto(
                solicitud.IdSolicitudVacaciones,
                diasSolicitados,
                fechasTurnosEnRango,
                advertencia));
        }
        catch (Exception ex)
        {
            return Result.Failure<ResultadoSolicitudVacacionesDto>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

/// <summary>
/// Sección 3.7: Cancelar solicitud de vacaciones (solo el dueño y solo si sigue PENDIENTE).
/// Si afecta 0 filas, responde "no encontrado" (Sección 2).
/// </summary>
public sealed record CancelarMiSolicitudVacacionesCommand(
    long IdSolicitudVacaciones) : IRequest<Result>, IRequireAuthentication;

public sealed class CancelarMiSolicitudVacacionesCommandHandler
    : IRequestHandler<CancelarMiSolicitudVacacionesCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public CancelarMiSolicitudVacacionesCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result> Handle(
        CancelarMiSolicitudVacacionesCommand request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var solicitud = await _context.SolicitudesVacaciones
            .FirstOrDefaultAsync(
                s => s.IdSolicitudVacaciones == request.IdSolicitudVacaciones &&
                     s.IdEmpleado == idEmpleado &&
                     s.CodEstadoSolicitud == EstadosSolicitud.Pendiente,
                cancellationToken);

        if (solicitud is null)
        {
            return Result.Failure(new Error(
                "Vacaciones.SolicitudNoEncontrada",
                "La solicitud de vacaciones no fue encontrada o ya no se encuentra pendiente.",
                ErrorType.NotFound));
        }

        solicitud.CodEstadoSolicitud = EstadosSolicitud.Cancelada;

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

public sealed record ObtenerMisSolicitudesVacacionesQuery
    : IRequest<Result<IReadOnlyList<SolicitudVacacionesDto>>>, IRequireAuthentication;

public sealed class ObtenerMisSolicitudesVacacionesQueryHandler
    : IRequestHandler<ObtenerMisSolicitudesVacacionesQuery, Result<IReadOnlyList<SolicitudVacacionesDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public ObtenerMisSolicitudesVacacionesQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<IReadOnlyList<SolicitudVacacionesDto>>> Handle(
        ObtenerMisSolicitudesVacacionesQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<SolicitudVacacionesDto>>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var items = await _context.SolicitudesVacaciones
            .AsNoTracking()
            .Where(s => s.IdEmpleado == idEmpleado)
            .OrderByDescending(s => s.SolicitadoEn)
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

        return Result.Success<IReadOnlyList<SolicitudVacacionesDto>>(items);
    }
}

// =====================================================================
// 4 y 5. GESTIÓN Y RESOLUCIÓN ADMINISTRATIVA DE VACACIONES (permiso: vacaciones.aprobar)
// =====================================================================

/// <summary>
/// Sección 5 (RF-06): Registrar los días causados por período en novedades.periodo_vacacional.
/// Valor base: parámetro VACACIONES_DIAS_POR_ANIO si no se envía dias_otorgados explícito.
/// </summary>
[RequirePermission(PermisosSistema.VacacionesAprobar)]
public sealed record RegistrarPeriodoVacacionalCommand(
    long IdEmpleado,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    decimal? DiasOtorgados = null) : IRequest<Result<long>>;

public sealed class RegistrarPeriodoVacacionalCommandValidator : AbstractValidator<RegistrarPeriodoVacacionalCommand>
{
    public RegistrarPeriodoVacacionalCommandValidator()
    {
        RuleFor(x => x.IdEmpleado).GreaterThan(0);
        RuleFor(x => x.FechaInicio).NotEmpty();
        RuleFor(x => x.FechaFin)
            .GreaterThan(x => x.FechaInicio)
            .WithMessage("La fecha de fin del período vacacional debe ser posterior a la de inicio.");
        RuleFor(x => x.DiasOtorgados)
            .GreaterThanOrEqualTo(0)
            .When(x => x.DiasOtorgados.HasValue);
    }
}

public sealed class RegistrarPeriodoVacacionalCommandHandler
    : IRequestHandler<RegistrarPeriodoVacacionalCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IParametroService _parametroService;

    public RegistrarPeriodoVacacionalCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IParametroService parametroService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _parametroService = parametroService;
    }

    public async Task<Result<long>> Handle(
        RegistrarPeriodoVacacionalCommand request,
        CancellationToken cancellationToken)
    {
        decimal dias = request.DiasOtorgados ?? await _parametroService.ObtenerDecimalAsync(
            ParametrosCodigos.VacacionesDiasPorAnio, request.FechaInicio, cancellationToken);

        var periodo = new PeriodoVacacional
        {
            IdEmpleado = request.IdEmpleado,
            FechaInicio = request.FechaInicio,
            FechaFin = request.FechaFin,
            DiasOtorgados = dias
        };

        try
        {
            await _context.PeriodosVacacionales.AddAsync(periodo, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(periodo.IdPeriodoVacacional);
        }
        catch (Exception ex)
        {
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

/// <summary>
/// Sección 4: Resolución de solicitudes de vacaciones (APROBADA o RECHAZADA).
/// - resuelto_por y resuelto_en van siempre juntos.
/// - motivo_rechazo es obligatorio al rechazar.
/// - Al aprobar revalida el saldo dentro de la transacción y evalúa CONFLICTO_BLOQUEA frente a turnos programados.
/// </summary>
[RequirePermission(PermisosSistema.VacacionesAprobar)]
public sealed record ResolverSolicitudVacacionesCommand(
    long IdSolicitudVacaciones,
    bool Aprobar,
    string? MotivoRechazo = null,
    bool ConfirmarAnulacionTurnos = false) : IRequest<Result>;

public sealed class ResolverSolicitudVacacionesCommandValidator
    : AbstractValidator<ResolverSolicitudVacacionesCommand>
{
    public ResolverSolicitudVacacionesCommandValidator()
    {
        RuleFor(x => x.IdSolicitudVacaciones).GreaterThan(0);
        RuleFor(x => x.MotivoRechazo)
            .NotEmpty().WithMessage("El motivo de rechazo es obligatorio al rechazar una solicitud.")
            .MaximumLength(300)
            .When(x => !x.Aprobar);
    }
}

public sealed class ResolverSolicitudVacacionesCommandHandler
    : IRequestHandler<ResolverSolicitudVacacionesCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IParametroService _parametroService;

    public ResolverSolicitudVacacionesCommandHandler(
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

    public async Task<Result> Handle(
        ResolverSolicitudVacacionesCommand request,
        CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId!.Value;

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var solicitud = await _context.SolicitudesVacaciones
                .FirstOrDefaultAsync(
                    s => s.IdSolicitudVacaciones == request.IdSolicitudVacaciones &&
                         s.CodEstadoSolicitud == EstadosSolicitud.Pendiente,
                    cancellationToken);

            if (solicitud is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(new Error(
                    "Vacaciones.SolicitudNoPendiente",
                    "La solicitud de vacaciones no existe o ya fue resuelta.",
                    ErrorType.NotFound));
            }

            if (request.Aprobar)
            {
                // 1. Al aprobar vacaciones revalida el saldo dentro de la transacción (Sección 4)
                var saldo = await _context.VistaSaldosVacaciones
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.IdEmpleado == solicitud.IdEmpleado, cancellationToken);

                decimal diasDisponibles = saldo?.DiasDisponibles ?? 0m;
                if (diasDisponibles < solicitud.DiasSolicitados)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure(new Error(
                        "Vacaciones.SaldoInsuficiente",
                        $"El empleado no cuenta con saldo disponible suficiente ({diasDisponibles:0.##} días) para aprobar {solicitud.DiasSolicitados:0.##} días.",
                        ErrorType.Conflict));
                }

                // 2. Buscar asignaciones no anuladas del empleado en el rango (Sección 4)
                var turnosEnConflicto = await _context.Asignaciones
                    .Where(a => a.IdEmpleado == solicitud.IdEmpleado &&
                                a.CodEstadoAsignacion != EstadosAsignacion.Anulada &&
                                a.Fecha >= solicitud.FechaInicio &&
                                a.Fecha <= solicitud.FechaFin)
                    .ToListAsync(cancellationToken);

                if (turnosEnConflicto.Count > 0)
                {
                    bool conflictoBloquea = await _parametroService.ObtenerBooleanoAsync(
                        ParametrosCodigos.ConflictoBloquea, solicitud.FechaInicio, cancellationToken);

                    var fechasStr = string.Join(", ", turnosEnConflicto.Select(t => t.Fecha.ToString("yyyy-MM-dd")).Distinct());

                    if (conflictoBloquea)
                    {
                        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                        return Result.Failure(new Error(
                            "Vacaciones.ConflictoConTurnos",
                            $"No se puede aprobar porque el empleado tiene turnos programados en las fechas: {fechasStr} (parámetro CONFLICTO_BLOQUEA activo).",
                            ErrorType.Conflict));
                    }

                    if (!request.ConfirmarAnulacionTurnos)
                    {
                        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                        return Result.Failure(new Error(
                            "Vacaciones.ConfirmacionRequerida",
                            $"El empleado tiene turnos programados en las fechas: {fechasStr}. Confirme la aprobación para anular esas asignaciones en la misma transacción.",
                            ErrorType.Conflict));
                    }

                    // Anular esas asignaciones en la misma transacción (verificando que no tengan asistencia)
                    var idsConflicto = turnosEnConflicto.Select(t => t.IdAsignacion).ToList();
                    bool algunoConAsistencia = await _context.RegistrosAsistencia
                        .AsNoTracking()
                        .AnyAsync(r => r.IdAsignacion != null && idsConflicto.Contains(r.IdAsignacion.Value), cancellationToken);

                    if (algunoConAsistencia)
                    {
                        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                        return Result.Failure(new Error(
                            "Vacaciones.TurnoConAsistencia",
                            "Uno o más turnos en el rango ya tienen registro de asistencia y no pueden ser anulados.",
                            ErrorType.Conflict));
                    }

                    foreach (var turno in turnosEnConflicto)
                    {
                        turno.CodEstadoAsignacion = EstadosAsignacion.Anulada;
                    }
                }

                solicitud.CodEstadoSolicitud = EstadosSolicitud.Aprobada;
                solicitud.ResueltoPor = adminId;
                solicitud.ResueltoEn = DateTimeOffset.UtcNow;
                solicitud.MotivoRechazo = null;
            }
            else
            {
                solicitud.CodEstadoSolicitud = EstadosSolicitud.Rechazada;
                solicitud.ResueltoPor = adminId;
                solicitud.ResueltoEn = DateTimeOffset.UtcNow;
                solicitud.MotivoRechazo = request.MotivoRechazo!.Trim();
            }

            // No insertar filas manuales en bitácora: el disparador tg_aud_vacaciones ya lo registra (Sección 4)
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
public sealed record ListarPeriodosVacacionalesAdminQuery(long? IdEmpleado = null)
    : IRequest<Result<IReadOnlyList<PeriodoVacacionalDto>>>;

public sealed class ListarPeriodosVacacionalesAdminQueryHandler
    : IRequestHandler<ListarPeriodosVacacionalesAdminQuery, Result<IReadOnlyList<PeriodoVacacionalDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarPeriodosVacacionalesAdminQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<PeriodoVacacionalDto>>> Handle(
        ListarPeriodosVacacionalesAdminQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.PeriodosVacacionales.AsNoTracking();
        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(p => p.IdEmpleado == request.IdEmpleado.Value);
        }

        var items = await query
            .OrderByDescending(p => p.FechaInicio)
            .Select(p => new PeriodoVacacionalDto(
                p.IdPeriodoVacacional,
                p.IdEmpleado,
                p.Empleado.Nombres + " " + p.Empleado.Apellidos,
                p.FechaInicio,
                p.FechaFin,
                p.DiasOtorgados))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PeriodoVacacionalDto>>(items);
    }
}

[RequirePermission(PermisosSistema.VacacionesAprobar)]
public sealed record ListarSaldosVacacionesAdminQuery(long? IdEmpleado = null)
    : IRequest<Result<IReadOnlyList<SaldoVacacionesDto>>>;

public sealed class ListarSaldosVacacionesAdminQueryHandler
    : IRequestHandler<ListarSaldosVacacionesAdminQuery, Result<IReadOnlyList<SaldoVacacionesDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarSaldosVacacionesAdminQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<SaldoVacacionesDto>>> Handle(
        ListarSaldosVacacionesAdminQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.VistaSaldosVacaciones.AsNoTracking();
        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(v => v.IdEmpleado == request.IdEmpleado.Value);
        }

        var items = await query
            .OrderBy(v => v.IdEmpleado)
            .Select(v => new SaldoVacacionesDto(
                v.IdEmpleado,
                v.DiasOtorgados,
                v.DiasUtilizados,
                v.DiasEnSolicitud,
                v.DiasDisponibles,
                v.DiasDisponibles - v.DiasEnSolicitud))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SaldoVacacionesDto>>(items);
    }
}

