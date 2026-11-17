import { Routes } from '@angular/router';
import { AppShellComponent } from './layout';

/**
 * Enrutador general recopilatorio.
 * Delega de forma diferida (lazy loading) las rutas de Autenticación, Empleado (/mi) y Administrador (/admin).
 */
export const routes: Routes = [
  {
    path: 'ingresar',
    title: 'Ingresar · Gestión de Turnos',
    loadComponent: () =>
      import('../features/auth/login/login.page').then((m) => m.LoginPageComponent),
  },
  {
    path: 'registro',
    title: 'Solicitar Registro · Gestión de Turnos',
    loadComponent: () =>
      import('../features/auth/register/register.page').then((m) => m.RegisterPageComponent),
  },
  {
    path: '',
    component: AppShellComponent,
    children: [
      {
        path: '',
        redirectTo: 'mi',
        pathMatch: 'full',
      },
      {
        path: 'mi',
        loadChildren: () => import('../features/mi/mi.routes').then((m) => m.MI_ROUTES),
      },
      {
        path: 'admin',
        loadChildren: () => import('../features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
      },
      {
        path: 'sin-permiso',
        title: 'Acceso Restringido · Gestión de Turnos',
        loadComponent: () =>
          import('../features/sin-permiso/sin-permiso.page').then((m) => m.SinPermisoPageComponent),
      },
    ],
  },
  {
    path: '**',
    redirectTo: 'ingresar',
  },
];
