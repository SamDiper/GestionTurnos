using FluentAssertions;
using GestionTurnos.Api.Application.CQRS.Seguridad.Registro;
using Xunit;

namespace GestionTurnos.UnitTests.Application.CQRS.Seguridad;

public class SolicitarRegistroCommandValidatorTests
{
    private readonly SolicitarRegistroCommandValidator _validator = new();

    private static SolicitarRegistroCommand CrearComandoValido() => new(
        CodTipoDocumento: "CC",
        NumeroDocumento: "1020304050",
        Nombres: "Carlos Andrés",
        Apellidos: "Pérez Gómez",
        Correo: "carlos.perez@empresa.com",
        Telefono: "3001234567",
        Contrasena: "ClaveSegura2026*"
    );

    [Fact]
    public void Validador_ConDatosCompletosYValidos_PasaSinErrores()
    {
        var command = CrearComandoValido();

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abcdefg")] // 7 caracteres
    public void Validador_ConContrasenaMenorDe8Caracteres_FallaConErrorLongitud(string contrasenaCorta)
    {
        var command = CrearComandoValido() with { Contrasena = contrasenaCorta };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Contrasena) &&
                                            e.ErrorMessage.Contains("al menos 8 caracteres"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void Validador_ConNombresVacios_FallaConErrorObligatorio(string? nombres)
    {
        var command = CrearComandoValido() with { Nombres = nombres! };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Nombres) &&
                                            e.ErrorMessage.Contains("Los nombres son obligatorios"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void Validador_ConApellidosVacios_FallaConErrorObligatorio(string? apellidos)
    {
        var command = CrearComandoValido() with { Apellidos = apellidos! };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Apellidos) &&
                                            e.ErrorMessage.Contains("Los apellidos son obligatorios"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null!)]
    public void Validador_ConTipoDocumentoVacio_FallaConErrorObligatorio(string? tipoDoc)
    {
        var command = CrearComandoValido() with { CodTipoDocumento = tipoDoc! };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.CodTipoDocumento));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null!)]
    public void Validador_ConNumeroDocumentoVacio_FallaConErrorObligatorio(string? numDoc)
    {
        var command = CrearComandoValido() with { NumeroDocumento = numDoc! };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.NumeroDocumento));
    }
}
