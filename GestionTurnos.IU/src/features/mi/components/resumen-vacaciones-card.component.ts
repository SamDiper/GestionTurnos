import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UiBadgeComponent } from '../../../app/shared/ui';

@Component({
  selector: 'app-resumen-vacaciones-card',
  imports: [RouterLink, UiBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article
      aria-labelledby="titulo-vacaciones"
      class="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs"
    >
      <div class="flex items-center justify-between gap-2">
        <h2 id="titulo-vacaciones" class="text-base font-bold text-slate-900">
          Saldo de vacaciones y solicitudes
        </h2>
        <ui-badge estado="ACTIVO" />
      </div>

      <dl class="mt-4 grid grid-cols-2 gap-4">
        <div class="rounded-xl bg-slate-50 p-3.5">
          <dt class="text-xs font-medium text-slate-600">Días disponibles</dt>
          <dd class="mt-1 text-2xl font-extrabold text-slate-900">15</dd>
        </div>
        <div class="rounded-xl bg-slate-50 p-3.5">
          <dt class="text-xs font-medium text-slate-600">Solicitudes pendientes</dt>
          <dd class="mt-1 text-2xl font-extrabold text-slate-900">0</dd>
        </div>
      </dl>

      <div class="mt-4 flex flex-wrap gap-3">
        <a
          routerLink="/mi/vacaciones"
          class="inline-flex min-h-11 items-center rounded-lg bg-marca-50 px-4 py-2 text-xs font-bold text-marca-700 hover:bg-marca-100"
        >
          Solicitar vacaciones
        </a>
        <a
          routerLink="/mi/novedades"
          class="inline-flex min-h-11 items-center rounded-lg border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-800 hover:bg-slate-50"
        >
          Ver novedades
        </a>
      </div>
    </article>
  `,
})
export class ResumenVacacionesCardComponent {}
