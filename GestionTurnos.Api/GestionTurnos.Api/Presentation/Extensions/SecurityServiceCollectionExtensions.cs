using System.Text;
using GestionTurnos.Api.Application.Common.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace GestionTurnos.Api.Presentation.Extensions;

/// <summary>
/// Métodos de extensión para configurar la seguridad (Autenticación y Autorización) en el contenedor de dependencias.
/// </summary>
public static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var secretKey = jwtSettings["Key"] ?? "ClaveSeguraTemporalParaTokensJwt2026!Caqueta";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"] ?? "GobernacionCaquetaApi",
                ValidAudience = jwtSettings["Audience"] ?? "AngularApp",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        context.Response.Headers.Append("Token-Expired", "true");
                    }
                    return Task.CompletedTask;
                },
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        accessToken = context.Request.Query["token"];
                    }

                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(PoliticasSeguridad.Gobernacion, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("TipoEntidad", TiposEntidadSeguridad.GOBERNACION));

            options.AddPolicy(PoliticasSeguridad.Contribuyente, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("TipoEntidad", TiposEntidadSeguridad.CONTRIBUYENTE));

            options.AddPolicy(PoliticasSeguridad.Municipio, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("TipoEntidad", TiposEntidadSeguridad.MUNICIPIO));

            options.AddPolicy(PoliticasSeguridad.Externo, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireAssertion(ctx =>
                          ctx.User.HasClaim("TipoEntidad", TiposEntidadSeguridad.CONTRIBUYENTE) ||
                          ctx.User.HasClaim("TipoEntidad", TiposEntidadSeguridad.MUNICIPIO)));
        });

        return services;
    }
}

