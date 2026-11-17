import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ApiClient } from '../../../app/core';
import { HorasPorTurno } from '../../../app/shared/data/modelos';
import { MiApi } from '../../../app/shared/data/apis';
import { UiSkeletonComponent } from '../../../app/shared/ui';
import { formatearFecha, formatearHora, hoyIso, minutosAHoras, sumarDias } from '../../../app/shared/utils/formato';
import { MarcajeRapidoCardComponent } from '../components/marcaje-rapido-card.component';

/** Mi asistencia (/mi/asistencia): marcaje y horas trabajadas contra programadas por turno. */
@Component({
  selector: 'app-mi-asistencia-page',
  imports: [MarcajeRapidoCardComponent, UiSkeletonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-asistencia" class="space-y-6">
      <header>
        <h1 id="titulo-asistencia" class="text-2xl font-extrabold tracking-tight text-slate-900">Mi asistencia</h1>
        <p class="mt-1 text-sm text-slate-600">Marca tu entrada y salida y revisa tus horas de los últimos días.</p>
      </header>

      <div class="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <app-marcaje-rapido-card />
        <dl class="grid grid-cols-3 gap-3 lg:col-span-2">
          <div class="tarjeta"><dt class="text-xs font-semibold text-slate-600">Programadas</dt><dd class="mt-1 text-lg font-extrabold">{{ minutosAHoras(totales().programados) }}</dd></div>
          <div class="tarjeta"><dt class="text-xs font-semibold text-slate-600">Trabajadas</dt><dd class="mt-1 text-lg font-extrabold">{{ minutosAHoras(totales().trabajados) }}</dd></div>
          <div class="tarjeta"><dt class="text-xs font-semibold text-slate-600">Llegadas tarde</dt><dd class="mt-1 text-lg font-extrabold">{{ totales().tardes }}</dd></div>
        </dl>
      </div>

      <article class="tarjeta" aria-labelledby="titulo-horas">
        <div class="mb-3 flex flex-wrap items-center justify-between gap-3">
          <h2 id="titulo-horas" class="text-base font-bold text-slate-900">Horas por turno</h2>
          <select aria-label="Período" class="filtro-select" (change)="cambiarDias(+$any($event.target).value)">
            <option value="7">Últimos 7 días</option>
            <option value="14" selected>Últimos 14 días</option>
            <option value="30">Últimos 30 días</option>
          </select>
        </div>
        @if (cargando()) {
          <ui-skeleton />
        } @else if (error()) {
          <p class="aviso-alerta">{{ error() }}</p>
        } @else {
          <div class="tabla-contenedor">
            <table class="tabla">
              <thead>
                <tr>
                  <th scope="col">Fecha</th><th scope="col">Turno</th><th scope="col">Entrada</th><th scope="col">Salida</th>
                  <th scope="col">Trabajado</th><th scope="col">Diferencia</th>
                </tr>
              </thead>
              <tbody>
                @for (h of horas(); track h.idAsignacion) {
                  <tr>
                    <td class="whitespace-nowrap font-semibold">{{ formatearFecha(h.fecha) }}</td>
                    <td>{{ h.nombreTurno }}</td>
                    <td>
                      {{ formatearHora(h.horaEntrada) }}
                      @if (h.minutosTarde) { <span class="block text-xs font-semibold text-alerta">{{ h.minutosTarde }} min tarde</span> }
                    </td>
                    <td>{{ formatearHora(h.horaSalida) }}</td>
                    <td>{{ minutosAHoras(h.minutosTrabajados) }}</td>
                    <td class="font-semibold" [class.text-peligro]="h.diferenciaMinutos < 0" [class.text-exito]="h.diferenciaMinutos >= 0">
                      {{ minutosAHoras(h.diferenciaMinutos) }}
                    </td>
                  </tr>
                } @empty {
                  <tr><td colspan="6" class="py-6 text-center text-slate-600">No hay turnos en el período.</td></tr>
                }
              </tbody>
            </table>
          </div>
        }
      </article>
    </section>
  `,
})
export class MiAsistenciaPageComponent implements OnInit {
  private readonly api = inject(MiApi);
  protected readonly formatearFecha = formatearFecha;
  protected readonly formatearHora = formatearHora;
  protected readonly minutosAHoras = minutosAHoras;

  private readonly dias = signal(14);
  protected readonly horas = signal<readonly HorasPorTurno[]>([]);
  protected readonly cargando = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly totales = computed(() =>
    this.horas().reduce(
      (t, h) => ({
        programados: t.programados + h.minutosProgramados,
        trabajados: t.trabajados + h.minutosTrabajados,
        tardes: t.tardes + (h.minutosTarde ? 1 : 0),
      }),
      { programados: 0, trabajados: 0, tardes: 0 }
    )
  );

  ngOnInit(): void {
    this.cargar();
  }

  cambiarDias(d: number): void {
    this.dias.set(d);
    this.cargar();
  }

  private cargar(): void {
    const hoy = hoyIso();
    this.cargando.set(true);
    this.api.horasPorTurno(sumarDias(hoy, -this.dias()), hoy).subscribe({
      next: (l) => {
        this.horas.set([...l].sort((a, b) => b.fecha.localeCompare(a.fecha)));
        this.cargando.set(false);
      },
      error: (err: unknown) => {
        this.cargando.set(false);
        this.error.set(ApiClient.extraerMensajeError(err).general);
      },
    });
  }
}
