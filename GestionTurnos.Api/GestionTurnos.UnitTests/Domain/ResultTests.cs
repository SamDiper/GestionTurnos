using FluentAssertions;
using GestionTurnos.Api.Domain.Shared;
using Xunit;

namespace GestionTurnos.UnitTests.Domain;

public class ResultTests
{
    [Fact]
    public void Success_CreaResultadoExitosoSinError()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_CreaResultadoFallidoConErrorEspecifico()
    {
        var error = new Error("Codigo.Test", "Mensaje de prueba", ErrorType.Validation);

        var result = Result.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void SuccessGeneric_RetornaValorEsperado()
    {
        const string valor = "Turno Asignado";

        var result = Result.Success(valor);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(valor);
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void FailureGeneric_AccederAValue_LanzaInvalidOperationException()
    {
        var error = new Error("Error.Prueba", "Fallo deliberado", ErrorType.Failure);
        var result = Result.Failure<string>(error);

        result.IsFailure.Should().BeTrue();
        var act = () => _ = result.Value;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No se puede acceder al valor de un resultado fallido*");
    }

    [Fact]
    public void Create_ConValorNull_RetornaFailureConErrorNullValue()
    {
        string? nulo = null;

        var result = Result.Create(nulo);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NullValue);
    }

    [Fact]
    public void Create_ConValorNoNulo_RetornaSuccess()
    {
        const int numero = 42;

        var result = Result.Create<int?>(numero);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(numero);
    }
}
