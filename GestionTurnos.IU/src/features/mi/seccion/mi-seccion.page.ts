import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { UiEmptyStateComponent } from '../../../app/shared/ui';
import { MarcajeRapidoCardComponent } from '../components/marcaje-rapido-card.component';

/**
 * Página recopilatoria para las subrutas del empleado (/mi/calendario, /mi/asistencia, etc.).
 */
@Component({
  selector: 'app-mi-seccion-page',
  imports: [RouterLink, UiEmptyStateComponent, MarcajeRapidoCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-seccion-empleado" class="space-y-6">
      <header>
        <h1 id="titulo-seccion-empleado" class="text-2xl font-extrabold tracking-tight text-slate-900">
          {{ titulo }}
        </h1>
        <p class="mt-1 text-sm text-slate-600">{{ descripcion }}</p>
      </header>

      @if (esAsistencia) {
        <app-marcaje-rapido-card />
      }

      <ui-empty-state [titulo]="titulo" [descripcion]="descripcion">
        <a
          routerLink="/mi"
          class="inline-flex min-h-11 items-center rounded-lg bg-marca-600 px-4 py-2 text-xs font-bold text-white hover:bg-marca-700"
        >
          Volver al resumen de hoy
        </a>
      </ui-empty-state>
    </section>
  `,
})
export class MiSeccionPageComponent {
  private readonly route = inject(ActivatedRoute);
  protected readonly titulo = (this.route.snapshot.data['titulo'] as string) || 'Mi sección';
  protected readonly descripcion =
    (this.route.snapshot.data['descripcion'] as string) ||
    'Módulo preparado para consulta conectada al endpoint del empleado.';
  protected readonly esAsistencia = Boolean(this.route.snapshot.data['esAsistencia']);
}
