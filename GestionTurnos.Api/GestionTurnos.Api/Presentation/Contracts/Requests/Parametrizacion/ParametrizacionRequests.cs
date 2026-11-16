namespace GestionTurnos.Api.Presentation.Contracts.Requests.Parametrizacion;

public sealed record GuardarDiaFestivoRequest(
    DateOnly Fecha,
    string Nombre);

public sealed record CambiarValorParametroRequest(
    string Valor,
    DateOnly VigenteDesde);

