using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Turnos;
using GestionTurnos.Api.Domain.Entities.Turnos;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Turnos;

// =====================================================================
// CATÁLOGO DE TURNOS (Sección 5 - RF-03, RG-05)
// Regla Sección 1: No escribir turnos.turno.cruza_medianoche ni duracion_minutos.
// =====================================================================

public sealed record ListarTurnosQuery(bool SoloActivos = true)
    : IRequest<Result<IReadOnlyList<TurnoDto>>>;

public sealed class ListarTurnosQueryHandler
    : IRequestHandler<ListarTurnosQuery, Result<IReadOnlyList<TurnoDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarTurnosQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<TurnoDto>>> Handle(
        ListarTurnosQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Turnos.AsNoTracking();
        if (request.SoloActivos)
        {
            query = query.Where(t => t.Activo);
        }

        var turnos = await query
            .OrderBy(t => t.HoraInicio)
            .ThenBy(t => t.Nombre)
            .Select(t => new TurnoDto(
                t.IdTurno,
                t.Nombre,
                t.HoraInicio,
                t.HoraFin,
                t.MinutosDescanso,
                t.CruzaMedianoche,
                t.DuracionMinutos,
                t.Color,
                t.Activo))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<TurnoDto>>(turnos);
    }
}

[RequirePermission(PermisosSistema.TurnosConfigurar)]
public sealed record GuardarTurnoCommand(
    int? IdTurno,
    string Nombre,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int MinutosDescanso,
    string? Color,
    bool Activo = true) : IRequest<Result<int>>;

public sealed class GuardarTurnoCommandValidator : AbstractValidator<GuardarTurnoCommand>
{
    public GuardarTurnoCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(60);
        RuleFor(x => x.MinutosDescanso).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Color)
            .Matches("^#[0-9A-Fa-f]{6}$")
            .WithMessage("El color debe estar en formato hexadecimal (ej. #F5B041).")
            .When(x => !string.IsNullOrWhiteSpace(x.Color));
    }
}

public sealed class GuardarTurnoCommandHandler : IRequestHandler<GuardarTurnoCommand, Result<int>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public GuardarTurnoCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<int>> Handle(GuardarTurnoCommand request, CancellationToken cancellationToken)
    {
        Turno turno;
        if (request.IdTurno.HasValue && request.IdTurno.Value > 0)
        {
            var existente = await _context.Turnos
                .FirstOrDefaultAsync(t => t.IdTurno == request.IdTurno.Value, cancellationToken);

            if (existente is null)
            {
                return Result.Failure<int>(new Error("Turnos.TurnoNoEncontrado", "El turno solicitado no existe.", ErrorType.NotFound));
            }

            // Editar un turno no altera las asignaciones ya creadas (guardan su propio periodo)
            existente.Nombre = request.Nombre.Trim();
            existente.HoraInicio = request.HoraInicio;
            existente.HoraFin = request.HoraFin;
            existente.MinutosDescanso = request.MinutosDescanso;
            existente.Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim();
            existente.Activo = request.Activo;
            turno = existente;
        }
        else
        {
            turno = new Turno
            {
                Nombre = request.Nombre.Trim(),
                HoraInicio = request.HoraInicio,
                HoraFin = request.HoraFin,
                MinutosDescanso = request.MinutosDescanso,
                Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim(),
                Activo = request.Activo
            };
            await _context.Turnos.AddAsync(turno, cancellationToken);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(turno.IdTurno);
        }
        catch (Exception ex)
        {
            return Result.Failure<int>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

