namespace GestionTurnos.Api.Application.DTOs.Asistencia;

public sealed record RegistroAsistenciaDto(
    long IdRegistro,
    long IdEmpleado,
    string NombreEmpleado,
    long? IdAsignacion,
    DateTimeOffset HoraEntrada,
    DateTimeOffset? HoraSalida,
    string CodFuenteMarcacion,
    string NombreFuenteMarcacion,
    string? Observacion,
    long? RegistradoPor,
    DateTimeOffset CreadoEn);

public sealed record HorasPorTurnoEmpleadoDto(
    long IdAsignacion,
    DateOnly Fecha,
    int IdTurno,
    string NombreTurno,
    int MinutosProgramados,
    int MinutosTrabajados,
    int DiferenciaMinutos,
    DateTimeOffset? HoraEntrada,
    DateTimeOffset? HoraSalida,
    int? MinutosTarde,
    bool SinAsistencia);

public sealed record ConsolidadoHorasEmpleadoDto(
    DateOnly Periodo,
    int MinutosProgramados,
    int MinutosTrabajados,
    int Diferencia,
    int TurnosSinAsistencia);

public sealed record HorasPorCategoriaDto(
    string CodCategoriaHora,
    string NombreCategoria,
    decimal FactorPago,
    int TotalMinutos,
    decimal TotalHoras);

