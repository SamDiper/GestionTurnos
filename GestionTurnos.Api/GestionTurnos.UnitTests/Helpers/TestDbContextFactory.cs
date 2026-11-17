using System;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.UnitTests.Helpers;

public static class TestDbContextFactory
{
    public static GestionTurnosContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<GestionTurnosContext>()
            .UseInMemoryDatabase(databaseName: databaseName ?? Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        var context = new GestionTurnosContext(options);
        return context;
    }
}
