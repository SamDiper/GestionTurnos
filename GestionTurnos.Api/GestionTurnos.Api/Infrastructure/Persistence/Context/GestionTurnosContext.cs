using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Infrastructure.Persistence.Context;

/// <summary>
/// DbContext principal de Gestión de Turnos. Se deja vacío a la espera de las entidades del nuevo dominio.
/// </summary>
public class GestionTurnosContext : DbContext
{
    public GestionTurnosContext(DbContextOptions<GestionTurnosContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GestionTurnosContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
