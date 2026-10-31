namespace GestionTurnos.Api.Application.Interfaces;

/// <summary>
/// Proporciona acceso al contexto del usuario actualmente autenticado en la solicitud HTTP.
/// Adapta los claims del token JWT al modelo de negocio de Sobretasa a la Gasolina de Caquetá.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// ID del usuario autenticado (extraído del token JWT).
    /// </summary>
    int? UserId { get; }

    /// <summary>
    /// Correo electrónico del usuario.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Nombre completo del usuario.
    /// </summary>
    string? Nombre { get; }

    /// <summary>
    /// ID del departamento al que pertenece el usuario.
    /// </summary>
    int? DepartamentoId { get; }

    /// <summary>
    /// ID del municipio asignado (para usuarios de alcaldías municipales).
    /// </summary>
    int? MunicipioId { get; }

    /// <summary>
    /// ID del Contribuyente asignado (Distribuidor Mayorista, Minorista, EDS). Nulo para gobernación central.
    /// </summary>
    int? ContribuyenteId { get; }

    /// <summary>
    /// Código del Tipo de Entidad (GOBERNACION, CONTRIBUYENTE, MUNICIPIO).
    /// </summary>
    string? TipoEntidadCodigo { get; }

    /// <summary>
    /// Código o nombre del Rol del usuario autenticado.
    /// </summary>
    string? RolCodigo { get; }

    /// <summary>
    /// Indica si el usuario pertenece a la Gobernación de Caquetá.
    /// </summary>
    bool EsGobernacion { get; }

    /// <summary>
    /// Indica si el usuario pertenece a un Contribuyente o EDS.
    /// </summary>
    bool EsContribuyente { get; }

    /// <summary>
    /// Indica si el usuario pertenece a una Alcaldía o Tesorería Municipal.
    /// </summary>
    bool EsMunicipio { get; }

    /// <summary>
    /// Indica si el usuario es un actor externo (Contribuyente o Municipio).
    /// </summary>
    bool EsExterno { get; }

    /// <summary>
    /// Dirección IP del cliente que realiza la solicitud HTTP.
    /// </summary>
    string? IpAddress { get; }

    /// <summary>
    /// Indica si la solicitud actual pertenece a un usuario autenticado.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Lista de códigos de permisos activos asignados al usuario.
    /// </summary>
    IReadOnlyList<string> Permisos { get; }

    /// <summary>
    /// Determina si el usuario autenticado cuenta con un permiso específico.
    /// </summary>
    bool TienePermiso(string permisoCodigo);
}
