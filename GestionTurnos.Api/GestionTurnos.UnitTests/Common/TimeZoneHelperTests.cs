using FluentAssertions;
using GestionTurnos.Api.Application.Common;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GestionTurnos.UnitTests.Common;

public class TimeZoneHelperTests
{
    [Fact]
    public void ResolveEmpresaTimeZone_SinConfiguracion_RetornaFallbackValido()
    {
        var config = new ConfigurationBuilder().Build();

        var timeZone = TimeZoneHelper.ResolveEmpresaTimeZone(config);

        timeZone.Should().NotBeNull();
        timeZone.BaseUtcOffset.Should().Be(TimeSpan.FromHours(-5));
    }

    [Fact]
    public void ResolveEmpresaTimeZone_ConConfiguracionExplicita_RetornaZonaConfigurada()
    {
        var configData = new Dictionary<string, string?>
        {
            ["AppSettings:TimeZone"] = "UTC"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

        var timeZone = TimeZoneHelper.ResolveEmpresaTimeZone(config);

        timeZone.Should().NotBeNull();
        timeZone.BaseUtcOffset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void GetOffsetForDate_FechaEnColombia_RetornaMenosCincoHoras()
    {
        var config = new ConfigurationBuilder().Build();
        var fecha = new DateOnly(2026, 6, 15);

        var offset = TimeZoneHelper.GetOffsetForDate(config, fecha);

        offset.Should().Be(TimeSpan.FromHours(-5));
    }

    [Fact]
    public void GetCurrentDateInEmpresaTimeZone_RetornaFechaValida()
    {
        var config = new ConfigurationBuilder().Build();

        var date = TimeZoneHelper.GetCurrentDateInEmpresaTimeZone(config);

        date.Year.Should().BeGreaterThanOrEqualTo(2025);
    }
}
