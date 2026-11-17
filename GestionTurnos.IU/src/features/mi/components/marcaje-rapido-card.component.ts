import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  ApiClient,
  ConexionService,
  NotificationService,
  RelojServidorService,
} from '../../../app/core';
import { UiButtonComponent } from '../../../app/shared/ui';

@Component({
  selector: 'app-marcaje-rapido-card',
  imports: [UiButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article
      aria-labelledby="titulo-marcaje"
      class="flex flex-col justify-between rounded-2xl border border-slate-200 bg-white p-6 shadow-xs"
    >
      <div>
        <h2 id="titulo-marcaje" class="text-base font-bold text-slate-900">
          Marcaje de asistencia
        </h2>
        <p class="mt-1 text-xs text-slate-600">
          Hora oficial del servidor:
          <strong class="font-mono text-slate-900">{{ reloj.horaBogota() }}</strong>
        </p>

        @if (!conexion.enLinea()) {
          <div
            role="alert"
            class="mt-3 rounded-xl border border-amber-300 bg-amber-50 p-3 text-xs font-medium text-amber-950"
          >
            Necesitas conexión para marcar. Si no puedes, pídele a tu supervisor que registre tu
            marcaje.
          </div>
        }
      </div>

      <div class="mt-5 grid grid-cols-1 gap-3 sm:grid-cols-2">
        <ui-button
          variante="primario"
          [anchoCompleto]="true"
          [deshabilitado]="!conexion.enLinea()"
          [cargando]="marcandoEntrada()"
          (click)="marcar('mi-entrada')"
        >
          Marcar entrada
        </ui-button>

        <ui-button
          variante="secundario"
          [anchoCompleto]="true"
          [deshabilitado]="!conexion.enLinea()"
          [cargando]="marcandoSalida()"
          (click)="marcar('mi-salida')"
        >
          Marcar salida
        </ui-button>
      </div>
    </article>
  `,
})
export class MarcajeRapidoCardComponent {
  protected readonly conexion = inject(ConexionService);
  protected readonly reloj = inject(RelojServidorService);
  private readonly api = inject(ApiClient);
  private readonly toast = inject(NotificationService);

  readonly marcandoEntrada = signal(false);
  readonly marcandoSalida = signal(false);

  marcar(tipo: 'mi-entrada' | 'mi-salida'): void {
    if (!this.conexion.enLinea()) return;
    const estadoCarga = tipo === 'mi-entrada' ? this.marcandoEntrada : this.marcandoSalida;
    estadoCarga.set(true);

    this.api.post(`/asistencia/${tipo}`, { observacion: null }).subscribe({
      next: () => {
        estadoCarga.set(false);
        this.toast.exito(
          tipo === 'mi-entrada' ? 'Entrada registrada con éxito.' : 'Salida registrada con éxito.'
        );
      },
      error: (err: unknown) => {
        estadoCarga.set(false);
        this.toast.error(ApiClient.extraerMensajeError(err).general);
      },
    });
  }
}
