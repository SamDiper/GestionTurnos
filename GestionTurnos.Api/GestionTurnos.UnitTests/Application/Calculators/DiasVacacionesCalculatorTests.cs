using FluentAssertions;
using GestionTurnos.Api.Application.Calculators;
using GestionTurnos.Api.Domain.Entities.Parametrizacion;
using GestionTurnos.UnitTests.Helpers;
using Xunit;

namespace GestionTurnos.UnitTests.Application.Calculators;

public class DiasVacacionesCalculatorTests
{
    [Fact]
    public async Task CalcularDiasSolicitadosAsync_FechaFinMenorQueFechaInicio_RetornaCero()
    {
        using var context = TestDbContextFactory.Create();
        var calculator = new DiasVacacionesCalculator(context);

        var inicio = new DateOnly(2026, 6, 15);
        var fin = new DateOnly(2026, 6, 10);

        var dias = await calculator.CalcularDiasSolicitadosAsync(inicio, fin);

        dias.Should().Be(0m);
    }

    [Fact]
    public async Task CalcularDiasSolicitadosAsync_SemanaCompletaSinFestivos_RetornaCincoDiasHabiles()
    {
        using var context = TestDbContextFactory.Create();
        var calculator = new DiasVacacionesCalculator(context);

        // Lunes 2026-06-01 a Domingo 2026-06-07 (7 días calendario, 5 hábiles)
        var inicio = new DateOnly(2026, 6, 1);
        var fin = new DateOnly(2026, 6, 7);

        var dias = await calculator.CalcularDiasSolicitadosAsync(inicio, fin);

        dias.Should().Be(5m);
    }

    [Fact]
    public async Task CalcularDiasSolicitadosAsync_ConFestivoEntreSemana_ExcluyeFestivo()
    {
        using var context = TestDbContextFactory.Create();

        // Agregamos un festivo un Miércoles
        var festivo = new DiaFestivo
        {
            Fecha = new DateOnly(2026, 6, 3), // Miércoles
            Nombre = "Día Festivo Nacional"
        };
        context.DiasFestivos.Add(festivo);
        await context.SaveChangesAsync();

        var calculator = new DiasVacacionesCalculator(context);

        // Lunes a Viernes (normalmente 5 hábiles, con 1 festivo deben ser 4)
        var inicio = new DateOnly(2026, 6, 1);
        var fin = new DateOnly(2026, 6, 5);

        var dias = await calculator.CalcularDiasSolicitadosAsync(inicio, fin);

        dias.Should().Be(4m);
    }

    [Fact]
    public async Task CalcularDiasSolicitadosAsync_SoloFinDeSemana_RetornaCero()
    {
        using var context = TestDbContextFactory.Create();
        var calculator = new DiasVacacionesCalculator(context);

        // Sábado 2026-06-06 a Domingo 2026-06-07
        var inicio = new DateOnly(2026, 6, 6);
        var fin = new DateOnly(2026, 6, 7);

        var dias = await calculator.CalcularDiasSolicitadosAsync(inicio, fin);

        dias.Should().Be(0m);
    }

    [Fact]
    public async Task CalcularDiasSolicitadosAsync_FestivoEnFinDeSemana_NoDescuentaDoble()
    {
        using var context = TestDbContextFactory.Create();

        // Festivo que cae en Sábado
        context.DiasFestivos.Add(new DiaFestivo
        {
            Fecha = new DateOnly(2026, 6, 6), // Sábado
            Nombre = "Festivo en Sábado"
        });
        await context.SaveChangesAsync();

        var calculator = new DiasVacacionesCalculator(context);

        // Lunes a Domingo: 5 días hábiles (el sábado ya no era hábil, no debe restarse dos veces)
        var inicio = new DateOnly(2026, 6, 1);
        var fin = new DateOnly(2026, 6, 7);

        var dias = await calculator.CalcularDiasSolicitadosAsync(inicio, fin);

        dias.Should().Be(5m);
    }
}
