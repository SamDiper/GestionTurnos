using FluentAssertions;
using GestionTurnos.Api.Application.CQRS.Seguridad.Auth;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Auditoria;
using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.UnitTests.Helpers;
using NSubstitute;
using Xunit;

namespace GestionTurnos.UnitTests.Application.CQRS.Seguridad;

public class IniciarSesionCommandHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();

    [Fact]
    public async Task Handle_UsuarioNoExisteYSinSolicitud_RetornaCredencialesInvalidas()
    {
        using var context = TestDbContextFactory.Create();
        var handler = new IniciarSesionCommandHandler(
            context, _unitOfWork, _passwordHasher, _jwtTokenGenerator, _currentUserService);

        var command = new IniciarSesionCommand("inexistente@empresa.com", "Password123!");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.CredencialesInvalidas);
    }

    [Fact]
    public async Task Handle_UsuarioNoExisteYConSolicitudPendiente_RetornaRegistroPendiente()
    {
        using var context = TestDbContextFactory.Create();

        context.SolicitudesRegistro.Add(new SolicitudRegistro
        {
            IdSolicitudRegistro = 1,
            CodTipoDocumento = "CC",
            NumeroDocumento = "12345678",
            Nombres = "Juan",
            Apellidos = "Pérez",
            Correo = "juan.perez@empresa.com",
            HashContrasena = "hash_dummy_pass",
            CodEstadoSolicitud = EstadosSolicitud.Pendiente,
            SolicitadoEn = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var handler = new IniciarSesionCommandHandler(
            context, _unitOfWork, _passwordHasher, _jwtTokenGenerator, _currentUserService);

        var command = new IniciarSesionCommand("juan.perez@empresa.com", "Password123!");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.RegistroPendiente);
    }

    [Fact]
    public async Task Handle_UsuarioNoExisteYConSolicitudRechazada_RetornaRegistroRechazadoConMotivo()
    {
        using var context = TestDbContextFactory.Create();

        const string motivo = "Documentación incompleta";
        context.SolicitudesRegistro.Add(new SolicitudRegistro
        {
            IdSolicitudRegistro = 2,
            CodTipoDocumento = "CC",
            NumeroDocumento = "87654321",
            Nombres = "Ana",
            Apellidos = "Gómez",
            Correo = "ana.gomez@empresa.com",
            HashContrasena = "hash_dummy_pass_2",
            CodEstadoSolicitud = EstadosSolicitud.Rechazada,
            MotivoRechazo = motivo,
            SolicitadoEn = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var handler = new IniciarSesionCommandHandler(
            context, _unitOfWork, _passwordHasher, _jwtTokenGenerator, _currentUserService);

        var command = new IniciarSesionCommand("ana.gomez@empresa.com", "Password123!");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AuthErrors.RegistroRechazado(motivo).Code);
        result.Error.Message.Should().Contain(motivo);
    }

    [Fact]
    public async Task Handle_UsuarioExistePeroContrasenaIncorrecta_RetornaCredencialesInvalidas()
    {
        using var context = TestDbContextFactory.Create();

        var rol = new Rol { IdRol = 1, Nombre = "Empleado", Activo = true };
        context.Roles.Add(rol);
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 10,
            Correo = "test@empresa.com",
            HashContrasena = "hash_cifrado",
            IdRol = rol.IdRol,
            Rol = rol,
            CodEstadoUsuario = EstadosUsuario.Activo
        });
        await context.SaveChangesAsync();

        _passwordHasher.Verify("ClaveIncorrecta", "hash_cifrado").Returns(false);

        var handler = new IniciarSesionCommandHandler(
            context, _unitOfWork, _passwordHasher, _jwtTokenGenerator, _currentUserService);

        var command = new IniciarSesionCommand("test@empresa.com", "ClaveIncorrecta");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.CredencialesInvalidas);
    }

    [Fact]
    public async Task Handle_UsuarioExistePeroInactivo_RetornaUsuarioNoActivo()
    {
        using var context = TestDbContextFactory.Create();

        var rol = new Rol { IdRol = 2, Nombre = "Supervisor", Activo = true };
        context.Roles.Add(rol);
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 11,
            Correo = "inactivo@empresa.com",
            HashContrasena = "hash_valido",
            IdRol = rol.IdRol,
            Rol = rol,
            CodEstadoUsuario = EstadosUsuario.Inactivo
        });
        await context.SaveChangesAsync();

        _passwordHasher.Verify("PasswordCorrecta!", "hash_valido").Returns(true);

        var handler = new IniciarSesionCommandHandler(
            context, _unitOfWork, _passwordHasher, _jwtTokenGenerator, _currentUserService);

        var command = new IniciarSesionCommand("inactivo@empresa.com", "PasswordCorrecta!");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AuthErrors.UsuarioNoActivo(EstadosUsuario.Inactivo).Code);
    }

    [Fact]
    public async Task Handle_UsuarioExistePeroRolInactivo_RetornaForbidden()
    {
        using var context = TestDbContextFactory.Create();

        var rolInactivo = new Rol { IdRol = 3, Nombre = "RolDesactivado", Activo = false };
        context.Roles.Add(rolInactivo);
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 12,
            Correo = "rol.inactivo@empresa.com",
            HashContrasena = "hash_valido",
            IdRol = rolInactivo.IdRol,
            Rol = rolInactivo,
            CodEstadoUsuario = EstadosUsuario.Activo
        });
        await context.SaveChangesAsync();

        _passwordHasher.Verify("PasswordCorrecta!", "hash_valido").Returns(true);

        var handler = new IniciarSesionCommandHandler(
            context, _unitOfWork, _passwordHasher, _jwtTokenGenerator, _currentUserService);

        var command = new IniciarSesionCommand("rol.inactivo@empresa.com", "PasswordCorrecta!");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.Forbidden);
    }

    [Fact]
    public async Task Handle_CredencialesValidasYUsuarioActivo_GeneraTokenYRetornaExito()
    {
        using var context = TestDbContextFactory.Create();

        var rol = new Rol { IdRol = 4, Nombre = "Administrador", Activo = true };
        var permiso = new Permiso { IdPermiso = 100, Codigo = "turnos.ver", Descripcion = "Ver turnos" };
        var rolPermiso = new RolPermiso { IdRol = rol.IdRol, Rol = rol, IdPermiso = permiso.IdPermiso, Permiso = permiso };

        context.Roles.Add(rol);
        context.Permisos.Add(permiso);
        context.RolesPermisos.Add(rolPermiso);

        var usuario = new Usuario
        {
            IdUsuario = 20,
            Correo = "admin@empresa.com",
            HashContrasena = "hash_ok",
            IdRol = rol.IdRol,
            Rol = rol,
            CodEstadoUsuario = EstadosUsuario.Activo
        };
        context.Usuarios.Add(usuario);

        var empleado = new Empleado
        {
            IdEmpleado = 101,
            IdUsuario = usuario.IdUsuario,
            Nombres = "Pedro",
            Apellidos = "Ramírez",
            CodTipoDocumento = "CC",
            NumeroDocumento = "98765432",
            CorreoContacto = "admin@empresa.com",
            CodEstadoEmpleado = EstadosEmpleado.Activo,
            IdCargo = 1,
            IdArea = 1,
            FechaIngreso = new DateOnly(2025, 1, 1),
            CodTipoContrato = "INDEF"
        };
        context.Empleados.Add(empleado);
        await context.SaveChangesAsync();

        _passwordHasher.Verify("AdminPassword123!", "hash_ok").Returns(true);
        _currentUserService.IpAddress.Returns("192.168.1.50");

        var expira = DateTimeOffset.UtcNow.AddHours(8);
        _jwtTokenGenerator.GenerateToken(
            usuario.IdUsuario,
            usuario.Correo,
            rol.Nombre,
            empleado.IdEmpleado,
            "Pedro Ramírez",
            Arg.Any<IReadOnlyCollection<string>>())
            .Returns(("jwt_mock_token_abc123", expira));

        var handler = new IniciarSesionCommandHandler(
            context, _unitOfWork, _passwordHasher, _jwtTokenGenerator, _currentUserService);

        var command = new IniciarSesionCommand("admin@empresa.com", "AdminPassword123!");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("jwt_mock_token_abc123");
        result.Value.Correo.Should().Be("admin@empresa.com");
        result.Value.Rol.Should().Be("Administrador");
        result.Value.IdEmpleado.Should().Be(101);
        result.Value.NombreCompleto.Should().Be("Pedro Ramírez");
        result.Value.Permisos.Should().Contain("turnos.ver");

        await _unitOfWork.Received(1).BeginTransactionAsync(usuario.IdUsuario, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
    }
}
