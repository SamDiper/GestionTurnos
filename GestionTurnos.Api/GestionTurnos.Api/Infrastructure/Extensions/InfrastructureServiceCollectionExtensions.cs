using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Infrastructure.Identity;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using GestionTurnos.Api.Infrastructure.Persistence.Interceptors;
using GestionTurnos.Api.Infrastructure.Persistence.Repositories;
using GestionTurnos.Api.Infrastructure.Security;
using GestionTurnos.Api.Infrastructure.Services;
using GestionTurnos.Shared.FTP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestionTurnos.Api.Infrastructure.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var dbSettings = configuration
            .GetSection(DatabaseSettings.SectionName)
            .Get<DatabaseSettings>() ?? new DatabaseSettings();

        var connectionString = configuration.GetConnectionString(dbSettings.ConnectionStringName)
                               ?? configuration.GetConnectionString("DefaultConnection");

        services.AddOptions<DatabaseSettings>()
            .Bind(configuration.GetSection(DatabaseSettings.SectionName))
            .ValidateDataAnnotations();

        services.AddSingleton<SessionAndTimeZoneInterceptor>();

        services.AddDbContext<GestionTurnosContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<SessionAndTimeZoneInterceptor>();
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(dbSettings.CommandTimeoutSeconds);
                if (dbSettings.EnableRetryOnFailure)
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: dbSettings.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(dbSettings.MaxRetryDelaySeconds),
                        errorNumbersToAdd: null);
                }
            })
            .AddInterceptors(interceptor);

            if (dbSettings.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }

            if (dbSettings.EnableDetailedErrors)
            {
                options.EnableDetailedErrors();
            }
        });

        // Contexto aislado para persistencia de DataProtection en SQL Server
        services.AddDbContext<DataProtectionKeyContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(dbSettings.CommandTimeoutSeconds);
            });
        });

        // Repositorios e Inyección de Dependencias
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Seguridad
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        // Contexto e Identidad (incluye resolución obligatoria de id_empleado activo desde BD)
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IEmpleadoSesionService, EmpleadoSesionService>();

        // Parametrización y Reglas de Negocio
        services.AddScoped<IParametroService, ParametroService>();

        // Generación de Documentos PDF y Renderizado de Plantillas
        services.AddSingleton(typeof(WkHtmlToPdfDotNet.Contracts.IConverter), new WkHtmlToPdfDotNet.SynchronizedConverter(new WkHtmlToPdfDotNet.PdfTools()));
        services.AddScoped<ITemplateRenderer, ScribanTemplateRenderer>();
        services.AddScoped<IPdfGenerator, DinkToPdfGenerator>();

        // Servicios Compartidos (GestionTurnos.Shared)
        services.AddSharedFtp(configuration);

        return services;
    }
}
