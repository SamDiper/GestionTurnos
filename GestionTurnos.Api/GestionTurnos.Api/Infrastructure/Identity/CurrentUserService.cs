using System.Security.Claims;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace GestionTurnos.Api.Infrastructure.Identity;

/// <summary>
/// Implementación de <see cref="ICurrentUserService"/> para extraer información
/// del usuario desde el HttpContext actual y sus claims de JWT en el ecosistema de Sobretasa.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("usuario_id");
            if (int.TryParse(claim, out int parsedId))
            {
                return parsedId;
            }
            return null;
        }
    }

    public string? Email =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);

    public string? Nombre =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);

    public int? DepartamentoId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("departamento_id");
            if (int.TryParse(claim, out int parsedId))
            {
                return parsedId;
            }
            return null;
        }
    }

    public int? MunicipioId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("municipio_id");
            if (int.TryParse(claim, out int parsedId))
            {
                return parsedId;
            }
            return null;
        }
    }

    public int? ContribuyenteId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("contribuyente_id");
            if (int.TryParse(claim, out int parsedId))
            {
                return parsedId;
            }
            return null;
        }
    }

    public string? TipoEntidadCodigo =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue("TipoEntidad");

    public string? RolCodigo =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);

    public bool EsGobernacion => 
        TipoEntidadCodigo == TiposEntidadSeguridad.GOBERNACION || (!ContribuyenteId.HasValue && !MunicipioId.HasValue);

    public bool EsContribuyente => 
        TipoEntidadCodigo == TiposEntidadSeguridad.CONTRIBUYENTE || ContribuyenteId.HasValue;

    public bool EsMunicipio => 
        TipoEntidadCodigo == TiposEntidadSeguridad.MUNICIPIO || MunicipioId.HasValue;

    public bool EsExterno => EsContribuyente || EsMunicipio;

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
