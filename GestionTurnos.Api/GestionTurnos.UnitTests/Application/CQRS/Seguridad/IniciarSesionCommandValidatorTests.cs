using FluentAssertions;
using GestionTurnos.Api.Application.CQRS.Seguridad.Auth;
using Xunit;

namespace GestionTurnos.UnitTests.Application.CQRS.Seguridad;

public class IniciarSesionCommandValidatorTests
{
    private readonly IniciarSesionCommandValidator _validator = new();

    [Fact]
    public void Validador_ConDatosValidos_PasaSinErrores()
    {
        var command = new IniciarSesionCommand("usuario@empresa.com", "PasswordSegura123!");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void Validador_ConCorreoVacio_FallaConErrorObligatorio(string? correo)
    {
        var command = new IniciarSesionCommand(correo!, "Password123!");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Correo) &&
                                            e.ErrorMessage.Contains("El correo electrónico es obligatorio"));
    }

    [Theory]
    [InlineData("correo-sin-arroba")]
    [InlineData("usuario@")]
    [InlineData("@dominio.com")]
    [InlineData("invalido@@algo.com")]
    public void Validador_ConCorreoInvalido_FallaConErrorFormato(string correoInvalido)
    {
        var command = new IniciarSesionCommand(correoInvalido, "Password123!");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Correo) &&
                                            e.ErrorMessage.Contains("formato del correo electrónico no es válido"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void Validador_ConContrasenaVacia_FallaConErrorObligatorio(string? contrasena)
    {
        var command = new IniciarSesionCommand("admin@empresa.com", contrasena!);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Contrasena) &&
                                            e.ErrorMessage.Contains("La contraseña es obligatoria"));
    }

    [Fact]
    public void Validador_ConCorreoExcedeLongitudMaxima_FallaValidacion()
    {
        var correoLargo = new string('a', 145) + "@dominio.com"; // > 150 chars
        var command = new IniciarSesionCommand(correoLargo, "Password123!");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Correo));
    }
}
