import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthStore } from '../auth';
import { ApiClient, NotificationService } from '../services';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const store = inject(AuthStore);
  const router = inject(Router);
  const notificaciones = inject(NotificationService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        const esRutaLogin = req.url.includes('/auth/login');

        if (error.status === 401 && !esRutaLogin) {
          store.limpiarSesion();
          notificaciones.alerta('Tu sesión expiró. Ingresa nuevamente.');
          void router.navigate(['/ingresar']);
        } else if (error.status === 403) {
          const { general } = ApiClient.extraerMensajeError(error);
          notificaciones.error(general || 'No tienes permiso para realizar esta acción.');
        } else if (error.status >= 500) {
          notificaciones.error('El servidor encontró un problema. Intenta de nuevo en unos momentos.');
        }
      }
      return throwError(() => error);
    })
  );
};
