import { Routes } from '@angular/router';

export const MI_ROUTES: Routes = [
  {
    path: '',
    title: 'Mi Inicio · TKeeper',
    loadComponent: () =>
      import('./inicio/mi-inicio.page').then((m) => m.MiInicioPageComponent),
  },
  {
    path: 'calendario',
    title: 'Mi Calendario · TKeeper',
    loadComponent: () =>
      import('./calendario/mi-calendario.page').then((m) => m.MiCalendarioPageComponent),
  },
  {
    path: 'asistencia',
    title: 'Marcar Asistencia · TKeeper',
    loadComponent: () =>
      import('./asistencia/mi-asistencia.page').then((m) => m.MiAsistenciaPageComponent),
  },
  {
    path: 'vacaciones',
    title: 'Mis Vacaciones · TKeeper',
    loadComponent: () =>
      import('./vacaciones/mis-vacaciones.page').then((m) => m.MisVacacionesPageComponent),
  },
  {
    path: 'novedades',
    title: 'Mis Novedades · TKeeper',
    loadComponent: () =>
      import('./novedades/mis-novedades.page').then((m) => m.MisNovedadesPageComponent),
  },
  {
    path: 'cambios-turno',
    title: 'Cambios de Turno · TKeeper',
    loadComponent: () =>
      import('./cambios-turno/mis-cambios-turno.page').then((m) => m.MisCambiosTurnoPageComponent),
  },
  {
    path: 'perfil',
    title: 'Mi Perfil · TKeeper',
    loadComponent: () =>
      import('./perfil/mi-perfil.page').then((m) => m.MiPerfilPageComponent),
  },
];
