using FluentAssertions;
using GestionTurnos.Api.Application.Common;
using GestionTurnos.Api.Domain.Enums;
using Xunit;

namespace GestionTurnos.UnitTests.Common;

public class ConversorParametrosTests
{
    [Theory]
    [InlineData("123", true, 123)]
    [InlineData("-45", true, -45)]
    [InlineData("0", true, 0)]
    [InlineData("abc", false, 0)]
    [InlineData("12.34", false, 0)]
    public void TryEntero_ValoresEsperados_RetornaResultadoCorrecto(string entrada, bool esperadoExito, int esperadoValor)
    {
        var exito = ConversorParametros.TryEntero(entrada, out var resultado);

        exito.Should().Be(esperadoExito);
        if (esperadoExito)
        {
            resultado.Should().Be(esperadoValor);
        }
    }

    [Theory]
    [InlineData("123.45", true, 123.45)]
    [InlineData("-0.5", true, -0.5)]
    [InlineData("100", true, 100)]
    [InlineData("invalido", false, 0)]
    public void TryDecimal_ValoresEsperados_RetornaResultadoCorrecto(string entrada, bool esperadoExito, double esperadoValor)
    {
        var exito = ConversorParametros.TryDecimal(entrada, out var resultado);

        exito.Should().Be(esperadoExito);
        if (esperadoExito)
        {
            resultado.Should().Be((decimal)esperadoValor);
        }
    }

    [Theory]
    [InlineData("true", true, true)]
    [InlineData("True", true, true)]
    [InlineData("TRUE", true, true)]
    [InlineData("false", true, false)]
    [InlineData("False", true, false)]
    [InlineData("1", true, true)]
    [InlineData("0", true, false)]
    [InlineData("2", false, false)]
    [InlineData("si", false, false)]
    public void TryBooleano_ValoresEsperados_RetornaResultadoCorrecto(string entrada, bool esperadoExito, bool esperadoValor)
    {
        var exito = ConversorParametros.TryBooleano(entrada, out var resultado);

        exito.Should().Be(esperadoExito);
        if (esperadoExito)
        {
            resultado.Should().Be(esperadoValor);
        }
    }

    [Theory]
    [InlineData("08:00", true, 8, 0)]
    [InlineData("23:59:59", true, 23, 59)]
    [InlineData("7:30", true, 7, 30)]
    [InlineData("25:00", false, 0, 0)]
    [InlineData("08-00", false, 0, 0)]
    public void TryHora_ValoresEsperados_RetornaResultadoCorrecto(string entrada, bool esperadoExito, int horas, int minutos)
    {
        var exito = ConversorParametros.TryHora(entrada, out var resultado);

        exito.Should().Be(esperadoExito);
        if (esperadoExito)
        {
            resultado.Hour.Should().Be(horas);
            resultado.Minute.Should().Be(minutos);
        }
    }

    [Theory]
    [InlineData(TiposDatoParametro.Entero, "42", true)]
    [InlineData(TiposDatoParametro.Entero, "invalido", false)]
    [InlineData(TiposDatoParametro.Decimal, "19.99", true)]
    [InlineData(TiposDatoParametro.Decimal, "no_decimal", false)]
    [InlineData(TiposDatoParametro.Booleano, "1", true)]
    [InlineData(TiposDatoParametro.Booleano, "quizas", false)]
    [InlineData(TiposDatoParametro.Hora, "14:00", true)]
    [InlineData(TiposDatoParametro.Hora, "99:99", false)]
    [InlineData(TiposDatoParametro.Texto, "Hola Mundo", true)]
    [InlineData(TiposDatoParametro.Texto, "   ", false)]
    [InlineData("DESCONOCIDO", "123", false)]
    public void EsValido_TiposYValores_RetornaValidacionEsperada(string tipoDato, string valor, bool esperadoValido)
    {
        var valido = ConversorParametros.EsValido(tipoDato, valor);

        valido.Should().Be(esperadoValido);
    }
}
