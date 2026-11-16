using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Auditoria;
using GestionTurnos.Api.Domain.Entities.Auditoria;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Auditoria;

// =====================================================================
// 5. CONSULTA DE AUDITORÍA (auditoria.bitacora_negocio - RF-08, RG-09)
// Solo consulta, por empleado, fecha o tipo de acción, paginada y ordenada por fecha_hora DESC.
// =====================================================================

[RequirePermission(PermisosSistema.AuditoriaConsultar)]
public sealed record ConsultarBitacoraQuery(
    long? IdEmpleado = null,
    long? IdUsuario = null,
    string? CodTipoAccion = null,
    string? Esquema = null,
    string? Tabla = null,
    DateTimeOffset? FechaDesde = null,
    DateTimeOffset? FechaHasta = null,
    int PageNumber = 1,
    int PageSize = 25) : IRequest<Result<PagedResult<BitacoraNegocioDto>>>;

public sealed class ConsultarBitacoraQueryHandler
    : IRequestHandler<ConsultarBitacoraQuery, Result<PagedResult<BitacoraNegocioDto>>>
{
    private readonly GestionTurnosContext _context;

    public ConsultarBitacoraQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<BitacoraNegocioDto>>> Handle(
        ConsultarBitacoraQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Math.Max(request.PageNumber, 1);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        IQueryable<BitacoraNegocio> query = _context.BitacoraNegocio.AsNoTracking();

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(b => b.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdUsuario.HasValue)
        {
            query = query.Where(b => b.IdUsuario == request.IdUsuario.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CodTipoAccion))
        {
            var accion = request.CodTipoAccion.Trim().ToUpperInvariant();
            query = query.Where(b => b.CodTipoAccion == accion);
        }

        if (!string.IsNullOrWhiteSpace(request.Esquema))
        {
            var esquema = request.Esquema.Trim().ToLowerInvariant();
            query = query.Where(b => b.Esquema.ToLower() == esquema);
        }

        if (!string.IsNullOrWhiteSpace(request.Tabla))
        {
            var tabla = request.Tabla.Trim().ToLowerInvariant();
            query = query.Where(b => b.Tabla.ToLower() == tabla);
        }

        if (request.FechaDesde.HasValue)
        {
            query = query.Where(b => b.FechaHora >= request.FechaDesde.Value);
        }

        if (request.FechaHasta.HasValue)
        {
            query = query.Where(b => b.FechaHora <= request.FechaHasta.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(b => b.FechaHora)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BitacoraNegocioDto(
                b.IdBitacora,
                b.FechaHora,
                b.IdUsuario,
                b.Usuario != null ? b.Usuario.Correo : null,
                b.CodTipoAccion,
                b.TipoAccion.Nombre,
                b.Esquema,
                b.Tabla,
                b.IdRegistro,
                b.IdEmpleado,
                b.Empleado != null ? b.Empleado.Nombres + " " + b.Empleado.Apellidos : null,
                b.ValorAnterior,
                b.ValorNuevo,
                b.Descripcion,
                b.IpOrigen))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<BitacoraNegocioDto>(items, totalCount, pageNumber, pageSize));
    }
}

