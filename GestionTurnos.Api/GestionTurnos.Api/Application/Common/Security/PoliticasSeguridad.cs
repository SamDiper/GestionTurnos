using GestionTurnos.Api.Domain.Enums;

namespace GestionTurnos.Api.Application.Common.Security;

/// <summary>
/// Constantes tipadas para políticas y códigos de permisos del sistema de Gestión de Turnos y Asistencia (Sección 2).
/// La autorización se basa en permisos finos consultados desde seguridad.rol_permiso -> seguridad.permiso.codigo.
/// </summary>
public static class PoliticasSeguridad
{
    public const string Autenticado = "PoliticaAutenticado";

    // Códigos de permisos finos (RF-01, RG-08)
    public const string UsuariosGestionar = PermisosSistema.UsuariosGestionar;
    public const string EmpleadosGestionar = PermisosSistema.EmpleadosGestionar;
    public const string TurnosGestionar = PermisosSistema.TurnosGestionar;
    public const string TurnosProgramar = PermisosSistema.TurnosProgramar;
    public const string AsistenciaRegistrarManual = PermisosSistema.AsistenciaRegistrarManual;
    public const string VacacionesAprobar = PermisosSistema.VacacionesAprobar;
    public const string NovedadesAprobar = PermisosSistema.NovedadesAprobar;
    public const string RegistrosAprobar = PermisosSistema.RegistrosAprobar;
    public const string ParametrosEditar = PermisosSistema.ParametrosEditar;
    public const string AuditoriaConsultar = PermisosSistema.AuditoriaConsultar;
    public const string ReportesConsultar = PermisosSistema.ReportesConsultar;
}
