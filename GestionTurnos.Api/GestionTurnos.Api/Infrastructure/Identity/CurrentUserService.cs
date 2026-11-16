using System.Security.Claims;
using GestionTurnos.Api.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace GestionTurnos.Api.Infrastructure.Identity;

/// <summary>
/// Implementación de <see cref="ICurrentUserService"/> para extraer información
/// del usuario desde el HttpContext actual y sus claims de JWT.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var claim = user?.FindFirstValue("usuario_id") ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (long.TryParse(claim, out long parsedId))
            {
                return parsedId;
            }
            return null;
        }
    }

    public string? Email =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email)
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("email");

    public string? Nombre =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name)
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("nombre");

    public string? RolCodigo =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role)
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("role");

    public string? IpAddress
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            string? forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            return httpContext.Connection.RemoteIpAddress?.ToString();
        }
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public IReadOnlyList<string> Permisos =>
        _httpContextAccessor.HttpContext?.User?.FindAll("permiso")
            .Select(c => c.Value)
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

    public bool TienePermiso(string permisoCodigo)
    {
        if (string.IsNullOrWhiteSpace(permisoCodigo))
            return false;

        return Permisos.Contains(permisoCodigo, StringComparer.OrdinalIgnoreCase);
    }
}
