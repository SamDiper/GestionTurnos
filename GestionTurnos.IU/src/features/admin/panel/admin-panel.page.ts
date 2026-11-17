import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import {
  AdminCountersGridComponent,
  ContadorAdminItem,
} from '../components/admin-counters-grid.component';

/**
 * Página recopilatoria del Panel Administrativo (/admin).
 * Ensambla la cuadrícula de los 6 indicadores clave de operación.
 */
@Component({
  selector: 'app-admin-panel-page',
  imports: [AdminCountersGridComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-panel-admin" class="space-y-6">
      <header>
        <h1 id="titulo-panel-admin" class="text-2xl font-extrabold tracking-tight text-slate-900">
          Panel de Administración
        </h1>
        <p class="mt-1 text-sm text-slate-600">
          Indicadores en tiempo real de registros, turnos, asistencia y solicitudes pendientes.
        </p>
      </header>

      <app-admin-counters-grid [contadores]="contadores()" />
    </section>
  `,
})
export class AdminPanelPageComponent {
  protected readonly contadores = signal<readonly ContadorAdminItem[]>([
    { etiqueta: 'Solicitudes de registro', valor: 0, ruta: '/admin/registros' },
    { etiqueta: 'Vacaciones pendientes', valor: 0, ruta: '/admin/empleados' },
    { etiqueta: 'Novedades pendientes', valor: 0, ruta: '/admin/empleados' },
    { etiqueta: 'Empleados activos', valor: 1, ruta: '/admin/empleados' },
    { etiqueta: 'Turnos de hoy', valor: 0, ruta: '/admin/programacion' },
    { etiqueta: 'Turnos sin asistencia', valor: 0, ruta: '/admin/asistencia' },
  ]);
}
