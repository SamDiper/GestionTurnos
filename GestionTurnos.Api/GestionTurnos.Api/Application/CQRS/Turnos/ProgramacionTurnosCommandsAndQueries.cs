using FluentValidation;
using GestionTurnos.Api.Application.Common;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Turnos;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Turnos;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Domain.ValueObjects;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestionTurnos.Api.Application.CQRS.Turnos;

// =====================================================================
// 6.1 PROGRAMAR, MODIFICAR, PUBLICAR Y ANULAR TURNOS (RF-04, CU-01, CU-04)
// =====================================================================

public sealed record ItemProgramacionTurnoDto(
    long IdEmpleado,
    int IdTurno,
    DateOnly Fecha,
    string? Observacion = null);

/// <summary>
/// Sección 6.1: Programación por día, semana, mes o grupo.
/// Un INSERT por empleado y fecha, todos en una transacción; si uno falla se revierte todo y se informa cuál falló.
/// </summary>
[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record ProgramarTurnosCommand(
    IReadOnlyList<ItemProgramacionTurnoDto> Asignaciones) : IRequest<Result<IReadOnlyList<long>>>;

public sealed class ProgramarTurnosCommandValidator : AbstractValidator<ProgramarTurnosCommand>
{
    public ProgramarTurnosCommandValidator()
    {
        RuleFor(x => x.Asignaciones)
            .NotEmpty().WithMessage("Debe incluir al menos una asignación de turno para programar.");

        RuleForEach(x => x.Asignaciones).ChildRules(item =>
        {
            item.RuleFor(i => i.IdEmpleado).GreaterThan(0);
            item.RuleFor(i => i.IdTurno).GreaterThan(0);
            item.RuleFor(i => i.Fecha).NotEmpty();
            item.RuleFor(i => i.Observacion).MaximumLength(300).When(i => !string.IsNullOrWhiteSpace(i.Observacion));
        });
    }
}

public sealed class ProgramarTurnosCommandHandler
    : IRequestHandler<ProgramarTurnosCommand, Result<IReadOnlyList<long>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IParametroService _parametroService;
    private readonly IConfiguration _configuration;

    public ProgramarTurnosCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IParametroService parametroService,
        IConfiguration configuration)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _parametroService = parametroService;
        _configuration = configuration;
    }

    public async Task<Result<IReadOnlyList<long>>> Handle(
        ProgramarTurnosCommand request,
        CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId!.Value;
        var idsCreados = new List<long>(request.Asignaciones.Count);

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            foreach (var item in request.Asignaciones)
            {
                var validacion = await ValidarYConstruirAsignacionAsync(
                    idAsignacionActual: null,
                    item.IdEmpleado,
                    item.IdTurno,
                    item.Fecha,
                    cancellationToken);

                if (validacion.IsFailure)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<IReadOnlyList<long>>(new Error(
                        validacion.Error.Code,
                        $"Error al programar empleado #{item.IdEmpleado} en la fecha {item.Fecha:yyyy-MM-dd}: {validacion.Error.Message}",
                        validacion.Error.Type));
                }

                var asignacion = new Asignacion
                {
                    IdEmpleado = item.IdEmpleado,
                    IdTurno = item.IdTurno,
                    Fecha = item.Fecha,
                    Periodo = validacion.Value,
                    CodEstadoAsignacion = EstadosAsignacion.Programada,
                    Observacion = string.IsNullOrWhiteSpace(item.Observacion) ? null : item.Observacion.Trim(),
                    CreadoPor = adminId,
                    CreadoEn = DateTimeOffset.UtcNow
                };

                await _context.Asignaciones.AddAsync(asignacion, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                idsCreados.Add(asignacion.IdAsignacion);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success<IReadOnlyList<long>>(idsCreados);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<IReadOnlyList<long>>(DatabaseErrorTranslator.Translate(ex));
        }
    }

    private async Task<Result<PeriodoRango>> ValidarYConstruirAsignacionAsync(
        long? idAsignacionActual,
        long idEmpleado,
        int idTurno,
        DateOnly fecha,
        CancellationToken cancellationToken)
    {
        // 1. Solo empleados ACTIVO (Sección 6.1)
        bool empleadoActivo = await _context.Empleados
            .AsNoTracking()
            .AnyAsync(e => e.IdEmpleado == idEmpleado && e.CodEstadoEmpleado == EstadosEmpleado.Activo, cancellationToken);

        if (!empleadoActivo)
        {
            return Result.Failure<PeriodoRango>(new Error(
                "Turnos.EmpleadoNoActivo",
                "Solo se pueden programar turnos a empleados en estado ACTIVO.",
                ErrorType.Failure));
        }

        // 2. El turno debe existir y estar activo (Sección 6.1: WHERE t.id_turno = $2 AND t.activo)
        var turno = await _context.Turnos
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdTurno == idTurno && t.Activo, cancellationToken);

        if (turno is null)
        {
            return Result.Failure<PeriodoRango>(new Error(
                "Turnos.TurnoNoActivo",
                "El turno seleccionado no existe o fue desactivado.",
                ErrorType.NotFound));
        }

        // 3. Construir el periodo en la zona horaria de la empresa (RG-05: si cruza_medianoche suma 1 día)
        var offset = TimeZoneHelper.GetOffsetForDate(_configuration, fecha);
        bool cruzaMedianoche = turno.HoraFin <= turno.HoraInicio;
        var periodo = PeriodoRango.DesdeTurno(fecha, turno.HoraInicio, turno.HoraFin, cruzaMedianoche, offset);

        // 4. Validar RG-02 / RG-03 (vacaciones aprobadas, novedad aprobada o día de descanso)
        bool tieneVacaciones = await _context.SolicitudesVacaciones
            .AsNoTracking()
            .AnyAsync(v => v.IdEmpleado == idEmpleado &&
                           v.CodEstadoSolicitud == EstadosSolicitud.Aprobada &&
                           fecha >= v.FechaInicio && fecha <= v.FechaFin, cancellationToken);

        if (tieneVacaciones)
        {
            return Result.Failure<PeriodoRango>(new Error(
                "Turnos.ValidacionAsignacion",
                $"El empleado tiene vacaciones aprobadas en la fecha {fecha:yyyy-MM-dd}",
                ErrorType.Conflict));
        }

        bool tieneNovedad = await _context.Novedades
            .AsNoTracking()
            .AnyAsync(n => n.IdEmpleado == idEmpleado &&
                           n.CodEstadoSolicitud == EstadosSolicitud.Aprobada &&
                           n.Inicio < periodo.Fin && periodo.Inicio < n.Fin, cancellationToken);

        if (tieneNovedad)
        {
            return Result.Failure<PeriodoRango>(new Error(
                "Turnos.ValidacionAsignacion",
                "El empleado tiene un permiso o ausencia aprobada que se cruza con el turno",
                ErrorType.Conflict));
        }

        bool tieneDescanso = await _context.DiasDescanso
            .AsNoTracking()
            .AnyAsync(d => d.IdEmpleado == idEmpleado && d.Fecha == fecha, cancellationToken);

        if (tieneDescanso)
        {
            return Result.Failure<PeriodoRango>(new Error(
                "Turnos.ValidacionAsignacion",
                $"El empleado tiene día de descanso programado el {fecha:yyyy-MM-dd}",
                ErrorType.Conflict));
        }

        // 5. Validar parámetros vigentes en la fecha del turno:
        //    DESCANSO_MIN_ENTRE_TURNOS, JORNADA_MAX_HORAS_DIA, JORNADA_MAX_HORAS_SEMANA (Sección 6.1)
        decimal descansoMinHoras = await _parametroService.ObtenerDecimalAsync(
            ParametrosCodigos.DescansoMinEntreTurnos, fecha, 12m, cancellationToken);
        decimal jornadaMaxDiaHoras = await _parametroService.ObtenerDecimalAsync(
            ParametrosCodigos.JornadaMaxHorasDia, fecha, 10m, cancellationToken);
        decimal jornadaMaxSemanaHoras = await _parametroService.ObtenerDecimalAsync(
            ParametrosCodigos.JornadaMaxHorasSemana, fecha, 44m, cancellationToken);

        // Calcular lunes y domingo de la semana de 'fecha'
        int diffLunes = ((int)fecha.DayOfWeek + 6) % 7;
        var inicioSemana = fecha.AddDays(-diffLunes);
        var finSemana = inicioSemana.AddDays(6);

        // Consultar asignaciones vigentes del empleado alrededor de esa semana (incluyendo 1 día antes y después para descanso entre turnos)
        var asignacionesCercanas = await _context.Asignaciones
            .AsNoTracking()
            .Include(a => a.Turno)
            .Where(a => a.IdEmpleado == idEmpleado &&
                        a.CodEstadoAsignacion != EstadosAsignacion.Anulada &&
                        (idAsignacionActual == null || a.IdAsignacion != idAsignacionActual.Value) &&
                        a.Fecha >= inicioSemana.AddDays(-1) &&
                        a.Fecha <= finSemana.AddDays(1))
            .ToListAsync(cancellationToken);

        // 5a. Verificar cruce de horario (RG-01) y descanso mínimo entre turnos
        foreach (var existente in asignacionesCercanas)
        {
            if (periodo.SeCruzaCon(existente.Periodo))
            {
                return Result.Failure<PeriodoRango>(new Error(
                    "Turnos.CruceHorario",
                    "El empleado ya tiene un turno que se cruza con ese horario.",
                    ErrorType.Conflict));
            }

            if (existente.Periodo.Fin <= periodo.Inicio)
            {
                var horasDescanso = (decimal)(periodo.Inicio - existente.Periodo.Fin).TotalHours;
                if (horasDescanso < descansoMinHoras)
                {
                    return Result.Failure<PeriodoRango>(new Error(
                        "Turnos.DescansoMinimoInsuficiente",
                        $"No se cumple el descanso mínimo de {descansoMinHoras} horas entre el turno del {existente.Fecha:yyyy-MM-dd} y este turno.",
                        ErrorType.Conflict));
                }
            }
            else if (periodo.Fin <= existente.Periodo.Inicio)
            {
                var horasDescanso = (decimal)(existente.Periodo.Inicio - periodo.Fin).TotalHours;
                if (horasDescanso < descansoMinHoras)
                {
                    return Result.Failure<PeriodoRango>(new Error(
                        "Turnos.DescansoMinimoInsuficiente",
                        $"No se cumple el descanso mínimo de {descansoMinHoras} horas entre este turno y el turno del {existente.Fecha:yyyy-MM-dd}.",
                        ErrorType.Conflict));
                }
            }
        }

        // 5b. Verificar JORNADA_MAX_HORAS_DIA y JORNADA_MAX_HORAS_SEMANA
        int minutosNetosNuevoTurno = Math.Max(0, periodo.DuracionMinutos - turno.MinutosDescanso);

        int minutosNetosDiaExistentes = asignacionesCercanas
            .Where(a => a.Fecha == fecha)
            .Sum(a => Math.Max(0, a.Periodo.DuracionMinutos - a.Turno.MinutosDescanso));

        decimal horasTotalesDia = (minutosNetosDiaExistentes + minutosNetosNuevoTurno) / 60m;
        if (horasTotalesDia > jornadaMaxDiaHoras)
        {
            return Result.Failure<PeriodoRango>(new Error(
                "Turnos.ExcedeJornadaMaximaDia",
                $"La programación supera el máximo de {jornadaMaxDiaHoras} horas diarias permitido ({horasTotalesDia:0.##} h).",
                ErrorType.Conflict));
        }

        int minutosNetosSemanaExistentes = asignacionesCercanas
            .Where(a => a.Fecha >= inicioSemana && a.Fecha <= finSemana)
            .Sum(a => Math.Max(0, a.Periodo.DuracionMinutos - a.Turno.MinutosDescanso));

        decimal horasTotalesSemana = (minutosNetosSemanaExistentes + minutosNetosNuevoTurno) / 60m;
        if (horasTotalesSemana > jornadaMaxSemanaHoras)
        {
            return Result.Failure<PeriodoRango>(new Error(
                "Turnos.ExcedeJornadaMaximaSemana",
                $"La programación supera el máximo de {jornadaMaxSemanaHoras} horas semanales permitido ({horasTotalesSemana:0.##} h).",
                ErrorType.Conflict));
        }

        return Result.Success(periodo);
    }
}

// =====================================================================
// 6.1 MODIFICAR ASIGNACIÓN DE TURNO (CU-04, RG-04)
// =====================================================================

[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record ModificarAsignacionTurnoCommand(
    long IdAsignacion,
    int IdTurno,
    DateOnly Fecha,
    string? Observacion) : IRequest<Result>;

public sealed class ModificarAsignacionTurnoCommandValidator : AbstractValidator<ModificarAsignacionTurnoCommand>
{
    public ModificarAsignacionTurnoCommandValidator()
    {
        RuleFor(x => x.IdAsignacion).GreaterThan(0);
        RuleFor(x => x.IdTurno).GreaterThan(0);
        RuleFor(x => x.Fecha).NotEmpty();
        RuleFor(x => x.Observacion).MaximumLength(300).When(x => !string.IsNullOrWhiteSpace(x.Observacion));
    }
}

public sealed class ModificarAsignacionTurnoCommandHandler : IRequestHandler<ModificarAsignacionTurnoCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IParametroService _parametroService;
    private readonly IConfiguration _configuration;

    public ModificarAsignacionTurnoCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IParametroService parametroService,
        IConfiguration configuration)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _parametroService = parametroService;
        _configuration = configuration;
    }

    public async Task<Result> Handle(ModificarAsignacionTurnoCommand request, CancellationToken cancellationToken)
    {
        var asignacion = await _context.Asignaciones
            .FirstOrDefaultAsync(a => a.IdAsignacion == request.IdAsignacion, cancellationToken);

        if (asignacion is null)
        {
            return Result.Failure(new Error("Turnos.AsignacionNoEncontrada", "La asignación de turno no existe.", ErrorType.NotFound));
        }

        if (asignacion.CodEstadoAsignacion == EstadosAsignacion.Anulada)
        {
            return Result.Failure(new Error("Turnos.AsignacionAnulada", "No se puede modificar una asignación anulada.", ErrorType.Conflict));
        }

        bool tieneAsistencia = await _context.RegistrosAsistencia
            .AsNoTracking()
            .AnyAsync(r => r.IdAsignacion == request.IdAsignacion, cancellationToken);

        if (tieneAsistencia)
        {
            return Result.Failure(new Error(
                "Turnos.AsignacionConAsistencia",
                "No se puede modificar una asignación que ya cuenta con registro de asistencia.",
                ErrorType.Conflict));
        }

        var turno = await _context.Turnos
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdTurno == request.IdTurno && t.Activo, cancellationToken);

        if (turno is null)
        {
            return Result.Failure(new Error("Turnos.TurnoNoActivo", "El turno seleccionado no existe o fue desactivado.", ErrorType.NotFound));
        }

        var offset = TimeZoneHelper.GetOffsetForDate(_configuration, request.Fecha);
        bool cruzaMedianoche = turno.HoraFin <= turno.HoraInicio;
        var nuevoPeriodo = PeriodoRango.DesdeTurno(request.Fecha, turno.HoraInicio, turno.HoraFin, cruzaMedianoche, offset);

        // Validar cruces de horario con otros turnos del mismo empleado
        var otrosTurnos = await _context.Asignaciones
            .AsNoTracking()
            .Where(a => a.IdEmpleado == asignacion.IdEmpleado &&
                        a.IdAsignacion != asignacion.IdAsignacion &&
                        a.CodEstadoAsignacion != EstadosAsignacion.Anulada &&
                        a.Fecha >= request.Fecha.AddDays(-1) &&
                        a.Fecha <= request.Fecha.AddDays(1))
            .ToListAsync(cancellationToken);

        if (otrosTurnos.Any(o => nuevoPeriodo.SeCruzaCon(o.Periodo)))
        {
            return Result.Failure(new Error(
                "Turnos.CruceHorario",
                "El empleado ya tiene un turno que se cruza con ese horario.",
                ErrorType.Conflict));
        }

        asignacion.IdTurno = request.IdTurno;
        asignacion.Fecha = request.Fecha;
        asignacion.Periodo = nuevoPeriodo;
        asignacion.Observacion = string.IsNullOrWhiteSpace(request.Observacion) ? null : request.Observacion.Trim();

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

// =====================================================================
// 6.1 ANULAR Y PUBLICAR ASIGNACIONES DE TURNO
// =====================================================================

/// <summary>
/// Sección 6.1: Quitar un turno = cod_estado_asignacion = 'ANULADA'. No se borra.
/// Una asignación con registro de asistencia no se anula ni se cambia de empleado.
/// </summary>
[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record AnularAsignacionTurnoCommand(
    long IdAsignacion,
    string? Observacion = null) : IRequest<Result>;

public sealed class AnularAsignacionTurnoCommandHandler : IRequestHandler<AnularAsignacionTurnoCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public AnularAsignacionTurnoCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(AnularAsignacionTurnoCommand request, CancellationToken cancellationToken)
    {
        var asignacion = await _context.Asignaciones
            .FirstOrDefaultAsync(a => a.IdAsignacion == request.IdAsignacion, cancellationToken);

        if (asignacion is null)
        {
            return Result.Failure(new Error("Turnos.AsignacionNoEncontrada", "La asignación de turno no existe.", ErrorType.NotFound));
        }

        bool tieneAsistencia = await _context.RegistrosAsistencia
            .AsNoTracking()
            .AnyAsync(r => r.IdAsignacion == request.IdAsignacion, cancellationToken);

        if (tieneAsistencia)
        {
            return Result.Failure(new Error(
                "Turnos.AsignacionConAsistencia",
                "Una asignación con registro de asistencia no se puede anular.",
                ErrorType.Conflict));
        }

        asignacion.CodEstadoAsignacion = EstadosAsignacion.Anulada;
        if (!string.IsNullOrWhiteSpace(request.Observacion))
        {
            asignacion.Observacion = request.Observacion.Trim();
        }

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
/// Sección 6.1: Publicar programación = cod_estado_asignacion = 'CONFIRMADA'.
/// </summary>
[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record PublicarAsignacionesTurnoCommand(
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    int? IdArea = null,
    long? IdEmpleado = null) : IRequest<Result<int>>;

public sealed class PublicarAsignacionesTurnoCommandValidator : AbstractValidator<PublicarAsignacionesTurnoCommand>
{
    public PublicarAsignacionesTurnoCommandValidator()
    {
        RuleFor(x => x.FechaHasta)
            .GreaterThanOrEqualTo(x => x.FechaDesde)
            .WithMessage("La fecha final debe ser mayor o igual a la fecha inicial.");
    }
}

public sealed class PublicarAsignacionesTurnoCommandHandler
    : IRequestHandler<PublicarAsignacionesTurnoCommand, Result<int>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public PublicarAsignacionesTurnoCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<int>> Handle(PublicarAsignacionesTurnoCommand request, CancellationToken cancellationToken)
    {
        var query = _context.Asignaciones
            .Where(a => a.Fecha >= request.FechaDesde &&
                        a.Fecha <= request.FechaHasta &&
                        a.CodEstadoAsignacion == EstadosAsignacion.Programada);

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(a => a.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            query = query.Where(a => a.Empleado.IdArea == request.IdArea.Value);
        }

        var asignaciones = await query.ToListAsync(cancellationToken);
        if (asignaciones.Count == 0)
        {
            return Result.Success(0);
        }

        foreach (var asignacion in asignaciones)
        {
            asignacion.CodEstadoAsignacion = EstadosAsignacion.Confirmada;
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(asignaciones.Count);
        }
        catch (Exception ex)
        {
            return Result.Failure<int>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

// =====================================================================
// 6.1 PROGRAMAR DÍA DE DESCANSO (turnos.dia_descanso)
// =====================================================================

[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record ProgramarDiaDescansoCommand(
    long IdEmpleado,
    DateOnly Fecha) : IRequest<Result<long>>;

public sealed class ProgramarDiaDescansoCommandValidator : AbstractValidator<ProgramarDiaDescansoCommand>
{
    public ProgramarDiaDescansoCommandValidator()
    {
        RuleFor(x => x.IdEmpleado).GreaterThan(0);
        RuleFor(x => x.Fecha).NotEmpty();
    }
}

public sealed class ProgramarDiaDescansoCommandHandler : IRequestHandler<ProgramarDiaDescansoCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public ProgramarDiaDescansoCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<long>> Handle(ProgramarDiaDescansoCommand request, CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId!.Value;

        bool empleadoActivo = await _context.Empleados
            .AsNoTracking()
            .AnyAsync(e => e.IdEmpleado == request.IdEmpleado && e.CodEstadoEmpleado == EstadosEmpleado.Activo, cancellationToken);

        if (!empleadoActivo)
        {
            return Result.Failure<long>(new Error(
                "Turnos.EmpleadoNoActivo",
                "Solo se pueden programar días de descanso a empleados activos.",
                ErrorType.Failure));
        }

        // Sección 6.1: Día de descanso: INSERT en turnos.dia_descanso; valida antes que no haya turno ese día.
        bool tieneTurnoEseDia = await _context.Asignaciones
            .AsNoTracking()
            .AnyAsync(a => a.IdEmpleado == request.IdEmpleado &&
                           a.Fecha == request.Fecha &&
                           a.CodEstadoAsignacion != EstadosAsignacion.Anulada, cancellationToken);

        if (tieneTurnoEseDia)
        {
            return Result.Failure<long>(new Error(
                "Turnos.EmpleadoTieneTurnoEnDescanso",
                $"El empleado ya tiene un turno programado en la fecha {request.Fecha:yyyy-MM-dd}. Anule el turno antes de asignar descanso.",
                ErrorType.Conflict));
        }

        var descanso = new DiaDescanso
        {
            IdEmpleado = request.IdEmpleado,
            Fecha = request.Fecha,
            CreadoPor = adminId,
            CreadoEn = DateTimeOffset.UtcNow
        };

        try
        {
            await _context.DiasDescanso.AddAsync(descanso, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(descanso.IdDiaDescanso);
        }
        catch (Exception ex)
        {
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record ListarDiasDescansoQuery(
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    long? IdEmpleado = null,
    int? IdArea = null) : IRequest<Result<IReadOnlyList<DiaDescansoDto>>>;

public sealed class ListarDiasDescansoQueryHandler
    : IRequestHandler<ListarDiasDescansoQuery, Result<IReadOnlyList<DiaDescansoDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarDiasDescansoQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<DiaDescansoDto>>> Handle(
        ListarDiasDescansoQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.DiasDescanso
            .AsNoTracking()
            .Where(d => d.Fecha >= request.FechaDesde && d.Fecha <= request.FechaHasta);

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(d => d.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            query = query.Where(d => d.Empleado.IdArea == request.IdArea.Value);
        }

        var items = await query
            .OrderBy(d => d.Fecha)
            .ThenBy(d => d.Empleado.Apellidos)
            .Select(d => new DiaDescansoDto(
                d.IdDiaDescanso,
                d.IdEmpleado,
                d.Empleado.Nombres + " " + d.Empleado.Apellidos,
                d.Fecha,
                d.CreadoPor,
                d.CreadoEn))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<DiaDescansoDto>>(items);
    }
}

// =====================================================================
// LISTAR ASIGNACIONES DE TURNO (ADMINISTRADOR)
// =====================================================================

[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record ListarAsignacionesTurnoQuery(
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    long? IdEmpleado = null,
    int? IdArea = null,
    int? IdTurno = null,
    string? CodEstadoAsignacion = null,
    int PageNumber = 1,
    int PageSize = 50) : IRequest<Result<PagedResult<AsignacionTurnoDto>>>;

public sealed class ListarAsignacionesTurnoQueryHandler
    : IRequestHandler<ListarAsignacionesTurnoQuery, Result<PagedResult<AsignacionTurnoDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarAsignacionesTurnoQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<AsignacionTurnoDto>>> Handle(
        ListarAsignacionesTurnoQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Math.Max(request.PageNumber, 1);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        var query = _context.Asignaciones
            .AsNoTracking()
            .Where(a => a.Fecha >= request.FechaDesde && a.Fecha <= request.FechaHasta);

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(a => a.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            query = query.Where(a => a.Empleado.IdArea == request.IdArea.Value);
        }

        if (request.IdTurno.HasValue)
        {
            query = query.Where(a => a.IdTurno == request.IdTurno.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CodEstadoAsignacion))
        {
            var estado = request.CodEstadoAsignacion.Trim().ToUpperInvariant();
            query = query.Where(a => a.CodEstadoAsignacion == estado);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(a => a.Fecha)
            .ThenBy(a => a.Empleado.Apellidos)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.IdAsignacion,
                a.IdEmpleado,
                NombreEmpleado = a.Empleado.Nombres + " " + a.Empleado.Apellidos,
                a.Empleado.NumeroDocumento,
                a.Empleado.IdArea,
                NombreArea = a.Empleado.Area.Nombre,
                a.IdTurno,
                NombreTurno = a.Turno.Nombre,
                ColorTurno = a.Turno.Color,
                a.Fecha,
                a.Periodo,
                a.CodEstadoAsignacion,
                NombreEstadoAsignacion = a.EstadoAsignacion.Nombre,
                a.Observacion,
                a.CreadoPor,
                a.CreadoEn
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new AsignacionTurnoDto(
                r.IdAsignacion,
                r.IdEmpleado,
                r.NombreEmpleado,
                r.NumeroDocumento,
                r.IdArea,
                r.NombreArea,
                r.IdTurno,
                r.NombreTurno,
                r.ColorTurno,
                r.Fecha,
                r.Periodo.Inicio,
                r.Periodo.Fin,
                r.CodEstadoAsignacion,
                r.NombreEstadoAsignacion,
                r.Observacion,
                r.CreadoPor,
                r.CreadoEn))
            .ToList();

        return Result.Success(new PagedResult<AsignacionTurnoDto>(items, totalCount, pageNumber, pageSize));
    }
}

