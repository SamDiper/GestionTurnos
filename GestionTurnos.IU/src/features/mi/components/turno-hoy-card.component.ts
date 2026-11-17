import { ChangeDetectionStrategy, Component } from '@angular/core';
import { UiBadgeComponent } from '../../../app/shared/ui';

@Component({
  selector: 'app-turno-hoy-card',
  imports: [UiBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article
      aria-labelledby="titulo-turno-hoy"
      class="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs"
    >
      <div class="flex items-center justify-between gap-2">
        <h2 id="titulo-turno-hoy" class="text-base font-bold text-slate-900">
          Mi turno programado hoy
        </h2>
        <ui-badge estado="CONFIRMADA" />
      </div>

      <p class="mt-2 text-2xl font-extrabold tracking-tight text-marca-700">
        06:00 – 14:00
      </p>
      <p class="mt-1 text-xs font-medium text-slate-600">
        Turno Mañana · Zona horaria oficial: America/Bogota
      </p>

      <!-- Tarjeta visual de referencia (inspirada en el bloque multimedia del diseño PWA) -->
      <div
        class="mt-4 flex h-36 w-full flex-col items-center justify-center rounded-xl border border-dashed border-slate-300 bg-slate-100 p-4 text-center"
      >
        <svg
          xmlns="http://www.w3.org/2000/svg"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.75"
          class="mb-2 h-8 w-8 text-slate-600"
          aria-hidden="true"
        >
          <rect width="18" height="18" x="3" y="3" rx="2" ry="2" />
          <circle cx="9" cy="9" r="2" />
          <path d="m21 15-3.086-3.086a2 2 0 0 0-2.828 0L6 21" />
        </svg>
        <p class="text-xs font-semibold text-slate-700">
          Espacio de referencia visual / banner de sede o turno
        </p>
      </div>
    </article>
  `,
})
export class TurnoHoyCardComponent {}
