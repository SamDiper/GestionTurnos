using System.Globalization;

namespace GestionTurnos.Api.Application.Notificaciones;

/// <summary>
/// Nombres de las plantillas embebidas en Infrastructure/Templates/Email/{nombre}.html.
/// </summary>
public static class PlantillasCorreo
{
    public const string CodigoVerificacion = "codigo_verificacion";
    public const string AvisoSeguridad = "aviso_seguridad";
    public const string RegistroAprobado = "registro_aprobado";
    public const string RegistroRechazado = "registro_rechazado";
    public const string TurnosPublicados = "turnos_publicados";
    public const string TurnoModificado = "turno_modificado";
    public const string TurnoAnulado = "turno_anulado";
}

// ---------------------------------------------------------------------
// Modelos de plantilla. Solo texto ya formateado: las plantillas no hacen
// cálculos ni dependen de la cultura del servidor.
// ---------------------------------------------------------------------

public sealed record CodigoVerificacionModelo(string Nombre, string Titulo, string Mensaje, string Codigo, int MinutosVigencia);

public sealed record AvisoSeguridadModelo(string Nombre, string Titulo, string Mensaje);

public sealed record RegistroAprobadoModelo(string Nombre, string Correo, string Area, string Cargo, string FechaIngreso);

public sealed record RegistroRechazadoModelo(string Nombre, string Motivo);

public sealed record TurnoCorreoItem(string Fecha, string Turno, string Horario);

public sealed record TurnosPublicadosModelo(string Nombre, string Desde, string Hasta, IReadOnlyList<TurnoCorreoItem> Turnos);

public sealed record TurnoModificadoModelo(string Nombre, TurnoCorreoItem Anterior, TurnoCorreoItem Nuevo);

public sealed record TurnoAnuladoModelo(string Nombre, TurnoCorreoItem Turno, string Motivo);

/// <summary>
/// Formatos de fecha y hora para correos (español de Colombia, independiente de la cultura del servidor).
/// </summary>
public static class FormatoCorreo
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

    public static string Fecha(DateOnly fecha) => fecha.ToString("dddd d 'de' MMMM 'de' yyyy", Cultura);

    public static string Horario(TimeOnly inicio, TimeOnly fin, bool cruzaMedianoche) =>
        $"{inicio:HH\\:mm} – {fin:HH\\:mm}{(cruzaMedianoche ? " (termina al día siguiente)" : string.Empty)}";

    public static TurnoCorreoItem Turno(DateOnly fecha, string nombreTurno, TimeOnly inicio, TimeOnly fin, bool cruzaMedianoche) =>
        new(Fecha(fecha), nombreTurno, Horario(inicio, fin, cruzaMedianoche));
}
