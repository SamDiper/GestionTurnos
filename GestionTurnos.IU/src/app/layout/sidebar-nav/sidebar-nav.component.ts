import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthStore, SiPuedeDirective } from '../../core';
import { UiIconComponent } from '../../shared/ui';
import { ITEMS_MENU_ADMIN, ITEMS_MENU_EMPLEADO } from '../navigation-config.constant';

@Component({
  selector: 'app-sidebar-nav',
  imports: [RouterLink, RouterLinkActive, SiPuedeDirective, UiIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside
      aria-label="Menú lateral del sistema"
      class="hidden min-h-[calc(100dvh-var(--spacing-barra))] w-18 shrink-0 flex-col border-r border-slate-200 bg-white py-5 md:flex xl:w-64"
    >
      @if (!store.esAdmin()) {
        <nav aria-label="Secciones del empleado" class="px-2.5 xl:px-4">
          <p class="mb-2 hidden px-3 text-xs font-bold tracking-wider text-slate-500 uppercase xl:block">
            Mi Espacio
          </p>
          <ul class="space-y-1">
            @for (item of itemsEmpleado; track item.id) {
              <li>
                <a
                  [routerLink]="item.ruta"
                  [routerLinkActiveOptions]="{ exact: item.ruta === '/mi' }"
                  routerLinkActive="bg-[#f0fdfa] text-[#1D768A] font-bold border-[#1D768A]"
                  [attr.aria-label]="item.etiqueta"
                  class="flex min-h-11 items-center justify-center gap-3 rounded-xl border border-transparent px-3 py-2.5 text-sm font-medium text-slate-700 transition hover:bg-slate-100 hover:text-[#113459] xl:justify-start"
                >
                  <ui-icon [nombre]="item.icono" />
                  <span class="hidden xl:inline">{{ item.etiqueta }}</span>
                </a>
              </li>
            }
          </ul>
        </nav>
      }

      @if (store.esAdmin()) {
        <nav aria-label="Secciones de administración" class="px-2.5 xl:px-4">
          <p class="mb-2 hidden px-3 text-xs font-bold tracking-wider text-slate-500 uppercase xl:block">
            Administración
          </p>
          <ul class="space-y-1">
            @for (item of itemsAdmin; track item.id) {
              <li *siPuede="item.permiso">
                <a
                  [routerLink]="item.ruta"
                  [routerLinkActiveOptions]="{ exact: item.ruta === '/admin' }"
                  routerLinkActive="bg-[#f0fdfa] text-[#1D768A] font-bold border-[#1D768A]"
                  [attr.aria-label]="item.etiqueta"
                  class="flex min-h-11 items-center justify-center gap-3 rounded-xl border border-transparent px-3 py-2.5 text-sm font-medium text-slate-700 transition hover:bg-slate-100 hover:text-[#113459] xl:justify-start"
                >
                  <ui-icon [nombre]="item.icono" />
                  <span class="hidden xl:inline">{{ item.etiqueta }}</span>
                </a>
              </li>
            }
          </ul>
        </nav>
      }
    </aside>
  `,
})
export class SidebarNavComponent {
  protected readonly store = inject(AuthStore);
  protected readonly itemsEmpleado = ITEMS_MENU_EMPLEADO;
  protected readonly itemsAdmin = ITEMS_MENU_ADMIN;
}
