namespace GestionTurnos.Api.Presentation.Contracts.Requests.Novedades;

public sealed record SolicitarMisVacacionesRequest(
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string? Observacion);

public sealed record RegistrarPeriodoVacacionalRequest(
    long IdEmpleado,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    decimal? DiasOtorgados);

public sealed record ResolverSolicitudRequest(
    bool Aprobar,
    string? MotivoRechazo = null,
    bool ConfirmarAnulacionTurnos = false);

public sealed record SolicitarMiNovedadRequest(
    string CodTipoNovedad,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    string? Observaciones,
    string? UrlSoporte);

public sealed record RegistrarNovedadAdminRequest(
    long IdEmpleado,
    string CodTipoNovedad,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    bool? Remunerado,
    bool? AfectaHoras,
    string? Observaciones,
    string? UrlSoporte);

