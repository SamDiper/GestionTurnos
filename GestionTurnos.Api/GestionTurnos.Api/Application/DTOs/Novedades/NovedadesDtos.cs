namespace GestionTurnos.Api.Application.DTOs.Novedades;

/// <summary>
/// Saldo de vacaciones desde novedades.v_saldo_vacaciones (Sección 3.4 - RF-06).
/// </summary>
public sealed record SaldoVacacionesDto(
    long IdEmpleado,
    decimal DiasOtorgados,
    decimal DiasUtilizados,
    decimal DiasEnSolicitud,
    decimal DiasDisponibles,
    decimal DiasDisponiblesParaNuevaSolicitud);

public sealed record ResultadoSolicitudVacacionesDto(
    long IdSolicitudVacaciones,
    decimal DiasSolicitados,
    IReadOnlyList<DateOnly> FechasTurnosEnConflicto,
    string? AdvertenciaConflictos);

public sealed record SolicitudVacacionesDto(
    long IdSolicitudVacaciones,
    long IdEmpleado,
    string NombreEmpleado,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    decimal DiasSolicitados,
    string CodEstadoSolicitud,
    string NombreEstadoSolicitud,
    string? Observacion,
    long SolicitadoPor,
    DateTimeOffset SolicitadoEn,
    long? ResueltoPor,
    DateTimeOffset? ResueltoEn,
    string? MotivoRechazo);

public sealed record PeriodoVacacionalDto(
    long IdPeriodoVacacional,
    long IdEmpleado,
    string NombreEmpleado,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    decimal DiasOtorgados);

public sealed record NovedadDto(
    long IdNovedad,
    long IdEmpleado,
    string NombreEmpleado,
    string CodTipoNovedad,
    string NombreTipoNovedad,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    bool Remunerado,
    bool AfectaHoras,
    string CodEstadoSolicitud,
    string NombreEstadoSolicitud,
    string? Observaciones,
    string? UrlSoporte,
    long RegistradoPor,
    DateTimeOffset RegistradoEn,
    long? ResueltoPor,
    DateTimeOffset? ResueltoEn,
    string? MotivoRechazo);
