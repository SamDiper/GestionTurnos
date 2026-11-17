namespace GestionTurnos.Api.Application.Common.Notificaciones;

/// <summary>
/// Configuración de las notificaciones informativas por correo (sección <c>Notificaciones</c> de appsettings).
/// No gobierna los códigos de verificación en dos pasos: esos son parte del inicio de sesión y siempre se envían.
/// </summary>
public sealed class NotificacionesSettings
{
    public const string SectionName = "Notificaciones";

    /// <summary>Interruptor general de las notificaciones informativas (registro y rotación de turnos).</summary>
    public bool Habilitado { get; set; } = true;

    /// <summary>Nombre del sistema mostrado en el encabezado y pie de los correos.</summary>
    public string NombreSistema { get; set; } = "Gestión de Turnos";

    /// <summary>URL pública del frontend (botón "Ingresar al sistema"). Vacío = el correo no muestra el botón.</summary>
    public string? UrlAplicacion { get; set; }

    /// <summary>Capacidad máxima de la cola en memoria. Si se llena, los nuevos correos se descartan y se registra un error.</summary>
    public int CapacidadCola { get; set; } = 1000;

    /// <summary>Reintentos ante errores transitorios del SMTP (con espera exponencial).</summary>
    public int MaximoReintentos { get; set; } = 3;

    /// <summary>Correos que se envían en paralelo como máximo.</summary>
    public int GradoParalelismo { get; set; } = 2;

    public NotificacionesEventosSettings Eventos { get; set; } = new();
}

/// <summary>
/// Activación individual de cada evento notificado.
/// </summary>
public sealed class NotificacionesEventosSettings
{
    public bool RegistroAprobado { get; set; } = true;
    public bool RegistroRechazado { get; set; } = true;
    public bool TurnosPublicados { get; set; } = true;
    public bool TurnoModificado { get; set; } = true;
    public bool TurnoAnulado { get; set; } = true;
}
