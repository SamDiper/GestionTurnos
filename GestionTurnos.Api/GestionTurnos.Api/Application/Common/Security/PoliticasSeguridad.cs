namespace GestionTurnos.Api.Application.Common.Security;

/// <summary>
/// Constantes tipadas que definen los nombres de las políticas de autorización del sistema de Sobretasa a la Gasolina.
/// Elimina el uso de cadenas mágicas en Controladores, Commands, Queries y Behaviors.
/// </summary>
public static class PoliticasSeguridad
{
    /// <summary>
    /// Política que exige facultades exclusivas de la Gobernación de Caquetá (Hacienda, Fiscalización, Tesorería).
    /// </summary>
    public const string Gobernacion = "PoliticaGobernacion";

    /// <summary>
    /// Política para Contribuyentes y Declarantes (Distribuidores Mayoristas, Minoristas y Estaciones de Servicio - EDS).
    /// </summary>
    public const string Contribuyente = "PoliticaContribuyente";

    /// <summary>
    /// Política para Municipios del Departamento del Caquetá (Sujetos Activos de Sobretasa Municipal).
    /// </summary>
    public const string Municipio = "PoliticaMunicipio";

    /// <summary>
    /// Política que agrupa a todos los usuarios externos (Contribuyentes y Municipios).
    /// </summary>
    public const string Externo = "PoliticaExterno";
}

/// <summary>
/// Constantes que definen los códigos de tipo de entidad institucional del ecosistema de Sobretasa.
/// </summary>
public static class TiposEntidadSeguridad
{
    public const string GOBERNACION = "GOBERNACION";
    public const string CONTRIBUYENTE = "CONTRIBUYENTE";
    public const string MUNICIPIO = "MUNICIPIO";
}
