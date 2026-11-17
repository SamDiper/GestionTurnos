import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { AuthSplitLayoutComponent } from '../../../app/shared/ui';
import { RegisterFormComponent } from './components/register-form.component';
import { RegisterPendingNoticeComponent } from './components/register-pending-notice.component';

/**
 * Página recopilatoria de Solicitud de Registro (/registro).
 * Ensambla el layout dividido de referencia (ilustración + tarjeta con patrones de puntos)
 * y alterna entre el formulario y el aviso de solicitud pendiente.
 */
@Component({
  selector: 'app-register-page',
  imports: [AuthSplitLayoutComponent, RegisterFormComponent, RegisterPendingNoticeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main id="contenido-principal" tabindex="-1" class="focus:outline-none">
      <ui-auth-split-layout
        titulo="Solicita tu cuenta"
        subtitulo="Registra tus datos para aprobación del administrador"
        tituloImagen="Espacio reservado para imagen de referencia (Registro)"
        detalleImagen="Pegar aquí la ilustración de bienvenida / registro cuando esté lista."
      >
        @if (idSolicitudRecibida(); as idSolicitud) {
          <app-register-pending-notice [idSolicitud]="idSolicitud" />
        } @else {
          <app-register-form (solicitudCreada)="idSolicitudRecibida.set($event)" />
        }
      </ui-auth-split-layout>
    </main>
  `,
})
export class RegisterPageComponent {
  protected readonly idSolicitudRecibida = signal<number | null>(null);
}
