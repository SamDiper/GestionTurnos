using GestionTurnos.Api.Domain.Shared;

namespace GestionTurnos.Api.Domain.Errors.Seguridad;

/// <summary>
/// Errores transversales de autenticación y autorización usados por el pipeline de MediatR.
/// </summary>
public static class AuthErrors
{
    public static readonly Error Unauthorized = new(
        "Auth.Unauthorized", "El usuario no está autenticado.", ErrorType.Unauthorized);

    public static readonly Error Forbidden = new(
        "Auth.Forbidden", "No tiene permisos para realizar esta acción.", ErrorType.Forbidden);
}
