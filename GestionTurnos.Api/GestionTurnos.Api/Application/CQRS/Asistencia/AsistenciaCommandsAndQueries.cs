using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Asistencia;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Asistencia;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestionTurnos.Api.Application.CQRS.Asistencia;

// =====================================================================
// 3.6 REGISTRAR ASISTENCIA DEL EMPLEADO (CU-02)
// =====================================================================

/// <summary>
/// Sección 3.6: Entrada.
/// - La hora siempre es now() del servidor; no acepta la hora del cliente.
/// - Busca el turno que corresponde con ventana previa de 60 minutos.
/// - No permite entrada si el empleado ya tiene un registro con hora_salida IS NULL.
/// </summary>
public sealed record RegistrarMiEntradaCommand(
    string CodFuenteMarcacion = FuentesMarcacionCodigos.Web) : IRequest<Result<long>>, IRequireAuthentication;

public sealed class RegistrarMiEntradaCommandValidator : AbstractValidator<RegistrarMiEntradaCommand>
{
    public RegistrarMiEntradaCommandValidator()
    {
        RuleFor(x => x.CodFuenteMarcacion)
            .NotEmpty()
            .Must(f => f.Equals(FuentesMarcacionCodigos.Web, StringComparison.OrdinalIgnoreCase) ||
                       f.Equals(FuentesMarcacionCodigos.Movil, StringComparison.OrdinalIgnoreCase))
            .WithMessage("La fuente de marcación del empleado debe ser 'WEB' o 'MOVIL'.");
    }
}

public sealed class RegistrarMiEntradaCommandHandler : IRequestHandler<RegistrarMiEntradaCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmpleadoSesionService _empleadoSesionService;
    private readonly IConfiguration _configuration;

    public RegistrarMiEntradaCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IEmpleadoSesionService empleadoSesionService,
        IConfiguration configuration)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _empleadoSesionService = empleadoSesionService;
        _configuration = configuration;
    }

    public async Task<Result<long>> Handle(RegistrarMiEntradaCommand request, CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<long>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        // No permitir una entrada si el empleado ya tiene un registro con hora_salida IS NULL (Sección 3.6)
        bool tieneRegistroAbierto = await _context.RegistrosAsistencia
            .AsNoTracking()
            .AnyAsync(r => r.IdEmpleado == idEmpleado && r.HoraSalida == null, cancellationToken);

        if (tieneRegistroAbierto)
        {
            return Result.Failure<long>(new Error(
                "Asistencia.RegistroEnCurso",
                "Ya tienes un registro de asistencia en curso sin hora de salida.",
                ErrorType.Conflict));
        }

        // La hora siempre es now() del servidor; no aceptar la hora del cliente
        var ahoraServidor = DateTimeOffset.UtcNow;
        var fechaHoy = DateOnly.FromDateTime(ahoraServidor.UtcDateTime);

        // Buscar el turno que corresponde en ventana [lower(periodo) - 60 min, upper(periodo))
        // y que aún no tenga registro de asistencia
        var candidatos = await _context.Asignaciones
            .AsNoTracking()
            .Where(a => a.IdEmpleado == idEmpleado &&
                        a.CodEstadoAsignacion != EstadosAsignacion.Anulada &&
                        a.Fecha >= fechaHoy.AddDays(-1) &&
                        a.Fecha <= fechaHoy.AddDays(1) &&
                        !_context.RegistrosAsistencia.Any(r => r.IdAsignacion == a.IdAsignacion))
            .ToListAsync(cancellationToken);

        var turnoCorrespondiente = candidatos
            .Where(a => ahoraServidor >= a.Periodo.Inicio.AddMinutes(-60) &&
                        ahoraServidor < a.Periodo.Fin)
            .OrderBy(a => a.Periodo.Inicio)
            .FirstOrDefault();

        bool permitirSinTurno = _configuration.GetValue<bool>("AppSettings:PermitirAsistenciaSinTurno", false);

        if (turnoCorrespondiente is null && !permitirSinTurno)
        {
            return Result.Failure<long>(new Error(
                "Asistencia.SinTurnoProgramado",
                "No tienes un turno programado vigente para registrar entrada en este horario.",
                ErrorType.Conflict));
        }

        var nuevoRegistro = new RegistroAsistencia
        {
            IdEmpleado = idEmpleado,
            IdAsignacion = turnoCorrespondiente?.IdAsignacion,
            HoraEntrada = ahoraServidor,
            CodFuenteMarcacion = request.CodFuenteMarcacion.Trim().ToUpperInvariant(),
            CreadoEn = ahoraServidor
        };

        try
        {
            await _context.RegistrosAsistencia.AddAsync(nuevoRegistro, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(nuevoRegistro.IdRegistro);
        }
        catch (Exception ex)
        {
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

/// <summary>
/// Sección 3.6: Salida.
/// Actualiza hora_salida = now() donde id_empleado = $1 AND hora_salida IS NULL,
/// y en la misma transacción ejecuta la liquidación de horas (Sección 6.3).
/// </summary>
public sealed record RegistrarMiSalidaCommand : IRequest<Result<long>>, IRequireAuthentication;

public sealed class RegistrarMiSalidaCommandHandler : IRequestHandler<RegistrarMiSalidaCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmpleadoSesionService _empleadoSesionService;
    private readonly ILiquidacionHorasCalculator _liquidacionCalculator;

    public RegistrarMiSalidaCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IEmpleadoSesionService empleadoSesionService,
        ILiquidacionHorasCalculator liquidacionCalculator)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _empleadoSesionService = empleadoSesionService;
        _liquidacionCalculator = liquidacionCalculator;
    }

    public async Task<Result<long>> Handle(RegistrarMiSalidaCommand request, CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<long>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var registroAbierto = await _context.RegistrosAsistencia
                .FirstOrDefaultAsync(r => r.IdEmpleado == idEmpleado && r.HoraSalida == null, cancellationToken);

            if (registroAbierto is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<long>(new Error(
                    "Asistencia.SinEntradaAbierta",
                    "No tienes un registro de entrada pendiente por cerrar.",
                    ErrorType.NotFound));
            }

            registroAbierto.HoraSalida = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // En la misma transacción ejecuta la liquidación de horas (Sección 6.3)
            await _liquidacionCalculator.LiquidarRegistroAsync(registroAbierto.IdRegistro, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result.Success(registroAbierto.IdRegistro);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

// =====================================================================
// 3.6 REGISTRO O CORRECCIÓN MANUAL (ADMINISTRADOR - permiso: asistencia.registrar_manual)
// =====================================================================

/// <summary>
/// Sección 3.6: Registro o corrección manual por el administrador.
/// cod_fuente_marcacion = 'ADMIN', registrado_por = admin y observacion obligatoria. Reliquida después.
/// </summary>
[RequirePermission(PermisosSistema.AsistenciaRegistrarManual)]
public sealed record RegistrarOCorregirAsistenciaManualCommand(
    long? IdRegistro,
    long IdEmpleado,
    long? IdAsignacion,
    DateTimeOffset HoraEntrada,
    DateTimeOffset? HoraSalida,
    string Observacion) : IRequest<Result<long>>;

public sealed class RegistrarOCorregirAsistenciaManualCommandValidator
    : AbstractValidator<RegistrarOCorregirAsistenciaManualCommand>
{
    public RegistrarOCorregirAsistenciaManualCommandValidator()
    {
        RuleFor(x => x.IdEmpleado).GreaterThan(0);
        RuleFor(x => x.HoraEntrada).NotEmpty();
        RuleFor(x => x.HoraSalida)
            .GreaterThan(x => x.HoraEntrada)
            .WithMessage("La hora de salida debe ser posterior a la hora de entrada.")
            .When(x => x.HoraSalida.HasValue);

        RuleFor(x => x.Observacion)
            .NotEmpty().WithMessage("La observación es obligatoria para el registro o corrección manual de asistencia.")
            .MaximumLength(300);
    }
}

public sealed class RegistrarOCorregirAsistenciaManualCommandHandler
    : IRequestHandler<RegistrarOCorregirAsistenciaManualCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILiquidacionHorasCalculator _liquidacionCalculator;

    public RegistrarOCorregirAsistenciaManualCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILiquidacionHorasCalculator liquidacionCalculator)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _liquidacionCalculator = liquidacionCalculator;
    }

    public async Task<Result<long>> Handle(
        RegistrarOCorregirAsistenciaManualCommand request,
        CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId!.Value;

        if (request.IdAsignacion.HasValue)
        {
            bool asignacionDeEmpleado = await _context.Asignaciones
                .AsNoTracking()
                .AnyAsync(a => a.IdAsignacion == request.IdAsignacion.Value &&
                               a.IdEmpleado == request.IdEmpleado &&
                               a.CodEstadoAsignacion != EstadosAsignacion.Anulada, cancellationToken);

            if (!asignacionDeEmpleado)
            {
                return Result.Failure<long>(new Error(
                    "Asistencia.AsignacionInvalida",
                    "La asignación seleccionada no pertenece al empleado o se encuentra anulada.",
                    ErrorType.Failure));
            }
        }

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            RegistroAsistencia registro;
            if (request.IdRegistro.HasValue && request.IdRegistro.Value > 0)
            {
                var existente = await _context.RegistrosAsistencia
                    .FirstOrDefaultAsync(r => r.IdRegistro == request.IdRegistro.Value, cancellationToken);

                if (existente is null)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<long>(new Error("Asistencia.RegistroNoEncontrado", "El registro de asistencia no existe.", ErrorType.NotFound));
                }

                existente.IdEmpleado = request.IdEmpleado;
                existente.IdAsignacion = request.IdAsignacion;
                existente.HoraEntrada = request.HoraEntrada;
                existente.HoraSalida = request.HoraSalida;
                existente.CodFuenteMarcacion = FuentesMarcacionCodigos.Admin;
                existente.Observacion = request.Observacion.Trim();
                existente.RegistradoPor = adminId;
                registro = existente;
            }
            else
            {
                registro = new RegistroAsistencia
                {
                    IdEmpleado = request.IdEmpleado,
                    IdAsignacion = request.IdAsignacion,
                    HoraEntrada = request.HoraEntrada,
                    HoraSalida = request.HoraSalida,
                    CodFuenteMarcacion = FuentesMarcacionCodigos.Admin,
                    Observacion = request.Observacion.Trim(),
                    RegistradoPor = adminId,
                    CreadoEn = DateTimeOffset.UtcNow
                };
                await _context.RegistrosAsistencia.AddAsync(registro, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Reliquidar horas después (Sección 3.6 y 6.3)
            if (registro.HoraSalida.HasValue)
            {
                await _liquidacionCalculator.LiquidarRegistroAsync(registro.IdRegistro, cancellationToken);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success(registro.IdRegistro);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

// =====================================================================
// 3.4 CONSULTAS DE ASISTENCIA Y HORAS DEL EMPLEADO (RF-05, CU-05)
// =====================================================================

public sealed record ObtenerMisHorasPorTurnoQuery(
    DateOnly Desde,
    DateOnly Hasta) : IRequest<Result<IReadOnlyList<HorasPorTurnoEmpleadoDto>>>, IRequireAuthentication;

public sealed class ObtenerMisHorasPorTurnoQueryHandler
    : IRequestHandler<ObtenerMisHorasPorTurnoQuery, Result<IReadOnlyList<HorasPorTurnoEmpleadoDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public ObtenerMisHorasPorTurnoQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<IReadOnlyList<HorasPorTurnoEmpleadoDto>>> Handle(
        ObtenerMisHorasPorTurnoQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<HorasPorTurnoEmpleadoDto>>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var items = await (from v in _context.VistaHorasPorTurno.AsNoTracking()
                           join t in _context.Turnos.AsNoTracking() on v.IdTurno equals t.IdTurno
                           where v.IdEmpleado == idEmpleado &&
                                 v.Fecha >= request.Desde &&
                                 v.Fecha <= request.Hasta
                           orderby v.Fecha descending
                           select new HorasPorTurnoEmpleadoDto(
                               v.IdAsignacion,
                               v.Fecha,
                               v.IdTurno,
                               t.Nombre,
                               v.MinutosProgramados,
                               v.MinutosTrabajados,
                               v.MinutosTrabajados - v.MinutosProgramados,
                               v.HoraEntrada,
                               v.HoraSalida,
                               v.MinutosTarde,
                               v.SinAsistencia))
                          .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<HorasPorTurnoEmpleadoDto>>(items);
    }
}

/// <summary>
/// Sección 3.4: Consolidado de asistencia y horas con $grano = 'day', 'week', 'month' o 'year'
/// (validado contra esa lista blanca estricta).
/// </summary>
public sealed record ObtenerMiConsolidadoHorasQuery(
    DateOnly Desde,
    DateOnly Hasta,
    string Grano = "week") : IRequest<Result<IReadOnlyList<ConsolidadoHorasEmpleadoDto>>>, IRequireAuthentication;

public sealed class ObtenerMiConsolidadoHorasQueryValidator : AbstractValidator<ObtenerMiConsolidadoHorasQuery>
{
    private static readonly HashSet<string> GranosPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "day", "week", "month", "year"
    };

    public ObtenerMiConsolidadoHorasQueryValidator()
    {
        RuleFor(x => x.Hasta).GreaterThanOrEqualTo(x => x.Desde);
        RuleFor(x => x.Grano)
            .NotEmpty()
            .Must(g => GranosPermitidos.Contains(g.Trim()))
            .WithMessage("El grano de agrupación debe ser 'day', 'week', 'month' o 'year'.");
    }
}

public sealed class ObtenerMiConsolidadoHorasQueryHandler
    : IRequestHandler<ObtenerMiConsolidadoHorasQuery, Result<IReadOnlyList<ConsolidadoHorasEmpleadoDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public ObtenerMiConsolidadoHorasQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<IReadOnlyList<ConsolidadoHorasEmpleadoDto>>> Handle(
        ObtenerMiConsolidadoHorasQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ConsolidadoHorasEmpleadoDto>>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;
        var grano = request.Grano.Trim().ToLowerInvariant();

        var filas = await _context.VistaHorasPorTurno
            .AsNoTracking()
            .Where(v => v.IdEmpleado == idEmpleado &&
                        v.Fecha >= request.Desde &&
                        v.Fecha <= request.Hasta)
            .ToListAsync(cancellationToken);

        var consolidado = filas
            .GroupBy(v => TruncarFechaPorGrano(v.Fecha, grano))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                int programados = g.Sum(x => x.MinutosProgramados);
                int trabajados = g.Sum(x => x.MinutosTrabajados);
                int sinAsistencia = g.Count(x => x.SinAsistencia);
                return new ConsolidadoHorasEmpleadoDto(
                    Periodo: g.Key,
                    MinutosProgramados: programados,
                    MinutosTrabajados: trabajados,
                    Diferencia: trabajados - programados,
                    TurnosSinAsistencia: sinAsistencia);
            })
            .ToList();

        return Result.Success<IReadOnlyList<ConsolidadoHorasEmpleadoDto>>(consolidado);
    }

    private static DateOnly TruncarFechaPorGrano(DateOnly fecha, string grano)
    {
        return grano switch
        {
            "day" => fecha,
            "week" => fecha.AddDays(-(((int)fecha.DayOfWeek + 6) % 7)), // Lunes de la semana (equivalente a date_trunc('week', fecha))
            "month" => new DateOnly(fecha.Year, fecha.Month, 1),
            "year" => new DateOnly(fecha.Year, 1, 1),
            _ => fecha
        };
    }
}

/// <summary>
/// Sección 3.4: Horas por categoría (ordinaria, extra, recargos):
/// suma asistencia.detalle_horas unida a asistencia.registro y parametrizacion.categoria_hora.
/// </summary>
public sealed record ObtenerMisHorasPorCategoriaQuery(
    DateTimeOffset Desde,
    DateTimeOffset Hasta) : IRequest<Result<IReadOnlyList<HorasPorCategoriaDto>>>, IRequireAuthentication;

public sealed class ObtenerMisHorasPorCategoriaQueryHandler
    : IRequestHandler<ObtenerMisHorasPorCategoriaQuery, Result<IReadOnlyList<HorasPorCategoriaDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public ObtenerMisHorasPorCategoriaQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<IReadOnlyList<HorasPorCategoriaDto>>> Handle(
        ObtenerMisHorasPorCategoriaQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<HorasPorCategoriaDto>>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var agrupado = await (from dh in _context.DetallesHoras.AsNoTracking()
                              join r in _context.RegistrosAsistencia.AsNoTracking() on dh.IdRegistro equals r.IdRegistro
                              join ch in _context.CategoriasHora.AsNoTracking() on dh.CodCategoriaHora equals ch.CodCategoriaHora
                              where r.IdEmpleado == idEmpleado &&
                                    r.HoraEntrada >= request.Desde &&
                                    r.HoraEntrada <= request.Hasta
                              group dh by new { ch.CodCategoriaHora, ch.Nombre, ch.FactorPago } into g
                              select new
                              {
                                  g.Key.CodCategoriaHora,
                                  g.Key.Nombre,
                                  g.Key.FactorPago,
                                  TotalMinutos = g.Sum(x => x.Minutos)
                              })
                             .ToListAsync(cancellationToken);

        var resultado = agrupado
            .OrderBy(x => x.CodCategoriaHora)
            .Select(x => new HorasPorCategoriaDto(
                x.CodCategoriaHora,
                x.Nombre,
                x.FactorPago,
                x.TotalMinutos,
                Math.Round(x.TotalMinutos / 60m, 2)))
            .ToList();

        return Result.Success<IReadOnlyList<HorasPorCategoriaDto>>(resultado);
    }
}

// =====================================================================
// 5. CONSULTA ADMINISTRATIVA DE ASISTENCIA (permiso: asistencia.consultar)
// =====================================================================

[RequirePermission(PermisosSistema.AsistenciaConsultar)]
public sealed record ListarRegistrosAsistenciaAdminQuery(
    DateTimeOffset Desde,
    DateTimeOffset Hasta,
    long? IdEmpleado = null,
    int? IdArea = null,
    string? CodFuenteMarcacion = null,
    bool? SoloAbiertos = null,
    int PageNumber = 1,
    int PageSize = 50) : IRequest<Result<PagedResult<RegistroAsistenciaDto>>>;

public sealed class ListarRegistrosAsistenciaAdminQueryHandler
    : IRequestHandler<ListarRegistrosAsistenciaAdminQuery, Result<PagedResult<RegistroAsistenciaDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarRegistrosAsistenciaAdminQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<RegistroAsistenciaDto>>> Handle(
        ListarRegistrosAsistenciaAdminQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Math.Max(request.PageNumber, 1);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        var query = _context.RegistrosAsistencia
            .AsNoTracking()
            .Where(r => r.HoraEntrada >= request.Desde && r.HoraEntrada <= request.Hasta);

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(r => r.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            query = query.Where(r => r.Empleado.IdArea == request.IdArea.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CodFuenteMarcacion))
        {
            var fuente = request.CodFuenteMarcacion.Trim().ToUpperInvariant();
            query = query.Where(r => r.CodFuenteMarcacion == fuente);
        }

        if (request.SoloAbiertos == true)
        {
            query = query.Where(r => r.HoraSalida == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.HoraEntrada)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RegistroAsistenciaDto(
                r.IdRegistro,
                r.IdEmpleado,
                r.Empleado.Nombres + " " + r.Empleado.Apellidos,
                r.IdAsignacion,
                r.HoraEntrada,
                r.HoraSalida,
                r.CodFuenteMarcacion,
                r.FuenteMarcacion.Nombre,
                r.Observacion,
                r.RegistradoPor,
                r.CreadoEn))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<RegistroAsistenciaDto>(items, totalCount, pageNumber, pageSize));
    }
}

