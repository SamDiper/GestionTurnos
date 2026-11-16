using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Auditoria;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Reportes;

// =====================================================================
// 5. REPORTES (RF-10 - permiso: reportes.consultar)
// Filtros por empleado, área, cargo, turno, fecha y estado.
// Solo lectura; siempre acotado por rango de fechas.
// =====================================================================

[RequirePermission(PermisosSistema.ReportesConsultar)]
public sealed record ReporteCumplimientoTurnosQuery(
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    long? IdEmpleado = null,
    int? IdArea = null,
    int? IdCargo = null,
    int? IdTurno = null,
    bool? SoloSinAsistencia = null,
    bool? SoloConRetardo = null,
    int PageNumber = 1,
    int PageSize = 50) : IRequest<Result<PagedResult<ReporteCumplimientoTurnoDto>>>;

public sealed class ReporteCumplimientoTurnosQueryValidator : AbstractValidator<ReporteCumplimientoTurnosQuery>
{
    public ReporteCumplimientoTurnosQueryValidator()
    {
        RuleFor(x => x.FechaDesde).NotEmpty();
        RuleFor(x => x.FechaHasta)
            .NotEmpty()
            .GreaterThanOrEqualTo(x => x.FechaDesde)
            .WithMessage("La fecha final debe ser mayor o igual a la fecha inicial.");
    }
}

public sealed class ReporteCumplimientoTurnosQueryHandler
    : IRequestHandler<ReporteCumplimientoTurnosQuery, Result<PagedResult<ReporteCumplimientoTurnoDto>>>
{
    private readonly GestionTurnosContext _context;

    public ReporteCumplimientoTurnosQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<ReporteCumplimientoTurnoDto>>> Handle(
        ReporteCumplimientoTurnosQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Math.Max(request.PageNumber, 1);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        var query = from v in _context.VistaHorasPorTurno.AsNoTracking()
                    join emp in _context.Empleados.AsNoTracking() on v.IdEmpleado equals emp.IdEmpleado
                    join area in _context.Areas.AsNoTracking() on emp.IdArea equals area.IdArea
                    join cargo in _context.Cargos.AsNoTracking() on emp.IdCargo equals cargo.IdCargo
                    join turno in _context.Turnos.AsNoTracking() on v.IdTurno equals turno.IdTurno
                    where v.Fecha >= request.FechaDesde && v.Fecha <= request.FechaHasta
                    select new
                    {
                        v.IdAsignacion,
                        v.Fecha,
                        v.IdEmpleado,
                        emp.NumeroDocumento,
                        NombreEmpleado = emp.Nombres + " " + emp.Apellidos,
                        emp.IdArea,
                        NombreArea = area.Nombre,
                        emp.IdCargo,
                        NombreCargo = cargo.Nombre,
                        v.IdTurno,
                        NombreTurno = turno.Nombre,
                        v.MinutosProgramados,
                        v.MinutosTrabajados,
                        v.HoraEntrada,
                        v.HoraSalida,
                        v.MinutosTarde,
                        v.SinAsistencia
                    };

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(x => x.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            query = query.Where(x => x.IdArea == request.IdArea.Value);
        }

        if (request.IdCargo.HasValue)
        {
            query = query.Where(x => x.IdCargo == request.IdCargo.Value);
        }

        if (request.IdTurno.HasValue)
        {
            query = query.Where(x => x.IdTurno == request.IdTurno.Value);
        }

        if (request.SoloSinAsistencia == true)
        {
            query = query.Where(x => x.SinAsistencia);
        }

        if (request.SoloConRetardo == true)
        {
            query = query.Where(x => x.MinutosTarde != null && x.MinutosTarde > 0);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.Fecha)
            .ThenBy(x => x.NombreEmpleado)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ReporteCumplimientoTurnoDto(
                x.IdAsignacion,
                x.Fecha,
                x.IdEmpleado,
                x.NumeroDocumento,
                x.NombreEmpleado,
                x.IdArea,
                x.NombreArea,
                x.IdCargo,
                x.NombreCargo,
                x.IdTurno,
                x.NombreTurno,
                x.MinutosProgramados,
                x.MinutosTrabajados,
                x.MinutosTrabajados - x.MinutosProgramados,
                x.HoraEntrada,
                x.HoraSalida,
                x.MinutosTarde,
                x.SinAsistencia))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<ReporteCumplimientoTurnoDto>(items, totalCount, pageNumber, pageSize));
    }
}

[RequirePermission(PermisosSistema.ReportesConsultar)]
public sealed record ReporteConsolidadoPorEmpleadoQuery(
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    int? IdArea = null,
    int? IdCargo = null,
    long? IdEmpleado = null) : IRequest<Result<IReadOnlyList<ReporteConsolidadoEmpleadoDto>>>;

public sealed class ReporteConsolidadoPorEmpleadoQueryValidator
    : AbstractValidator<ReporteConsolidadoPorEmpleadoQuery>
{
    public ReporteConsolidadoPorEmpleadoQueryValidator()
    {
        RuleFor(x => x.FechaDesde).NotEmpty();
        RuleFor(x => x.FechaHasta).NotEmpty().GreaterThanOrEqualTo(x => x.FechaDesde);
    }
}

public sealed class ReporteConsolidadoPorEmpleadoQueryHandler
    : IRequestHandler<ReporteConsolidadoPorEmpleadoQuery, Result<IReadOnlyList<ReporteConsolidadoEmpleadoDto>>>
{
    private readonly GestionTurnosContext _context;

    public ReporteConsolidadoPorEmpleadoQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<ReporteConsolidadoEmpleadoDto>>> Handle(
        ReporteConsolidadoPorEmpleadoQuery request,
        CancellationToken cancellationToken)
    {
        var query = from v in _context.VistaHorasPorTurno.AsNoTracking()
                    join emp in _context.Empleados.AsNoTracking() on v.IdEmpleado equals emp.IdEmpleado
                    join area in _context.Areas.AsNoTracking() on emp.IdArea equals area.IdArea
                    join cargo in _context.Cargos.AsNoTracking() on emp.IdCargo equals cargo.IdCargo
                    where v.Fecha >= request.FechaDesde && v.Fecha <= request.FechaHasta
                    select new
                    {
                        v.IdEmpleado,
                        emp.NumeroDocumento,
                        NombreEmpleado = emp.Nombres + " " + emp.Apellidos,
                        emp.IdArea,
                        NombreArea = area.Nombre,
                        emp.IdCargo,
                        NombreCargo = cargo.Nombre,
                        v.MinutosProgramados,
                        v.MinutosTrabajados,
                        MinutosTarde = v.MinutosTarde ?? 0,
                        v.SinAsistencia
                    };

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(x => x.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            query = query.Where(x => x.IdArea == request.IdArea.Value);
        }

        if (request.IdCargo.HasValue)
        {
            query = query.Where(x => x.IdCargo == request.IdCargo.Value);
        }

        var filas = await query.ToListAsync(cancellationToken);

        var consolidado = filas
            .GroupBy(x => new
            {
                x.IdEmpleado,
                x.NumeroDocumento,
                x.NombreEmpleado,
                x.NombreArea,
                x.NombreCargo
            })
            .OrderBy(g => g.Key.NombreEmpleado)
            .Select(g =>
            {
                int programados = g.Sum(x => x.MinutosProgramados);
                int trabajados = g.Sum(x => x.MinutosTrabajados);
                return new ReporteConsolidadoEmpleadoDto(
                    g.Key.IdEmpleado,
                    g.Key.NumeroDocumento,
                    g.Key.NombreEmpleado,
                    g.Key.NombreArea,
                    g.Key.NombreCargo,
                    g.Count(),
                    g.Count(x => x.SinAsistencia),
                    programados,
                    trabajados,
                    trabajados - programados,
                    g.Sum(x => x.MinutosTarde));
            })
            .ToList();

        return Result.Success<IReadOnlyList<ReporteConsolidadoEmpleadoDto>>(consolidado);
    }
}

