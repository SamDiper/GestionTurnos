namespace GestionTurnos.Api.Application.DTOs.Auditoria;

/// <summary>
/// Registro de la bitácora de auditoría de negocio (Sección 5 - RF-08, RG-09).
/// </summary>
public sealed record BitacoraNegocioDto(
    long IdBitacora,
    DateTimeOffset FechaHora,
    long? IdUsuario,
    string? CorreoUsuario,
    string CodTipoAccion,
    string NombreTipoAccion,
    string Esquema,
    string Tabla,
    string IdRegistro,
    long? IdEmpleado,
    string? NombreEmpleado,
    string? ValorAnterior,
    string? ValorNuevo,
    string? Descripcion,
    string? IpOrigen);

/// <summary>
/// Reporte operativo de cumplimiento de turnos y asistencia (Sección 5 - RF-10).
/// </summary>
public sealed record ReporteCumplimientoTurnoDto(
    long IdAsignacion,
    DateOnly Fecha,
    long IdEmpleado,
    string NumeroDocumento,
    string NombreEmpleado,
    int IdArea,
    string NombreArea,
    int IdCargo,
    string NombreCargo,
    int IdTurno,
    string NombreTurno,
    int MinutosProgramados,
    int MinutosTrabajados,
    int DiferenciaMinutos,
    DateTimeOffset? HoraEntrada,
    DateTimeOffset? HoraSalida,
    int? MinutosTarde,
    bool SinAsistencia);

/// <summary>
/// Reporte consolidado de horas por empleado (Sección 5 - RF-10).
/// </summary>
public sealed record ReporteConsolidadoEmpleadoDto(
    long IdEmpleado,
    string NumeroDocumento,
    string NombreEmpleado,
    string NombreArea,
    string NombreCargo,
    int TotalTurnosProgramados,
    int TotalTurnosSinAsistencia,
    int TotalMinutosProgramados,
    int TotalMinutosTrabajados,
    int DiferenciaMinutos,
    int TotalMinutosTarde);

