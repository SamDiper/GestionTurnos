import { Routes } from '@angular/router';

export const MI_ROUTES: Routes = [
  {
    path: '',
    title: 'Mi Inicio · Gestión de Turnos',
    loadComponent: () =>
      import('./inicio/mi-inicio.page').then((m) => m.MiInicioPageComponent),
  },
  {
    path: 'calendario',
    title: 'Mi Calendario · Gestión de Turnos',
    data: {
      titulo: 'Mi calendario de turnos',
      descripcion: 'Visualización por día, semana y mes de tus turnos, descansos y novedades.',
    },
    loadComponent: () =>
      import('./seccion/mi-seccion.page').then((m) => m.MiSeccionPageComponent),
  },
  {
    path: 'asistencia',
    title: 'Marcar Asistencia · Gestión de Turnos',
    data: {
      titulo: 'Marcaje e historial de asistencia',
      descripcion: 'Registra tu entrada y salida en tiempo real y consulta tus horas.',
      esAsistencia: true,
    },
    loadComponent: () =>
      import('./seccion/mi-seccion.page').then((m) => m.MiSeccionPageComponent),
  },
  {
    path: 'vacaciones',
    title: 'Mis Vacaciones · Gestión de Turnos',
    data: {
      titulo: 'Mis vacaciones y saldo disponible',
      descripcion: 'Consulta tus días causados y solicita nuevos períodos de vacaciones.',
    },
    loadComponent: () =>
      import('./seccion/mi-seccion.page').then((m) => m.MiSeccionPageComponent),
  },
  {
    path: 'novedades',
    title: 'Mis Novedades · Gestión de Turnos',
    data: {
      titulo: 'Mis permisos, incapacidades y novedades',
      descripcion: 'Registra solicitudes de permisos y consulta su estado de aprobación.',
    },
    loadComponent: () =>
      import('./seccion/mi-seccion.page').then((m) => m.MiSeccionPageComponent),
  },
  {
    path: 'perfil',
    title: 'Mi Perfil · Gestión de Turnos',
    data: {
      titulo: 'Mi perfil y datos de contacto',
      descripcion: 'Actualiza tu correo de contacto, teléfono, dirección y contraseña.',
    },
    loadComponent: () =>
      import('./seccion/mi-seccion.page').then((m) => m.MiSeccionPageComponent),
  },
];
