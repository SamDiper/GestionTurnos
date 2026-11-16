using System.ComponentModel.DataAnnotations;

namespace GestionTurnos.Api.Infrastructure.Persistence;

/// <summary>
/// Parámetros del proveedor EF Core SQL Server, enlazados desde la sección <c>Database</c> de appsettings.
/// </summary>
public class DatabaseSettings
{
    public const string SectionName = "Database";

    /// <summary>Nombre de la cadena de conexión dentro de <c>ConnectionStrings</c>.</summary>
    [Required]
    public string ConnectionStringName { get; set; } = "DefaultConnection";

    /// <summary>Tiempo máximo (segundos) de espera por comando SQL.</summary>
    [Range(1, 3600)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>Habilita la estrategia de reintentos ante fallos transitorios de SQL Server.</summary>
    public bool EnableRetryOnFailure { get; set; } = true;

    /// <summary>Número máximo de reintentos ante fallos transitorios.</summary>
    [Range(0, 20)]
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>Retardo máximo (segundos) entre reintentos.</summary>
    [Range(1, 300)]
    public int MaxRetryDelaySeconds { get; set; } = 30;

    /// <summary>Incluye valores de parámetros en los logs de EF Core. Solo para desarrollo.</summary>
    public bool EnableSensitiveDataLogging { get; set; }

    /// <summary>Errores detallados de materialización en EF Core. Solo para desarrollo.</summary>
    public bool EnableDetailedErrors { get; set; }
}
