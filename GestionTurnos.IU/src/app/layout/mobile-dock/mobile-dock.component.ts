import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { UiIconComponent } from '../../shared/ui';
import { ITEMS_DOCK_MOVIL } from '../navigation-config.constant';

/**
 * Barra de navegación inferior flotante tipo cápsula (PWA Mobile Dock),
 * adaptada a la paleta del logo (#113459 fondo, #31BBA1 botón central, #1D768A activos).
 */
@Component({
  selector: 'app-mobile-dock',
  imports: [RouterLink, RouterLinkActive, UiIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav
      aria-label="Navegación principal móvil"
      class="safe-bottom-dock pointer-events-none fixed inset-x-0 bottom-0 z-40 flex justify-center px-4 pt-2 md:hidden"
    >
      <ul
        class="pointer-events-auto flex w-full max-w-md items-center justify-around rounded-full border border-[#1D768A]/40 bg-[#113459]/95 px-3 py-2 shadow-[0_12px_32px_rgba(17,52,89,0.5)] backdrop-blur-md"
      >
        @for (item of itemsDock; track item.id) {
          <li>
            @if (item.destacadoCentral) {
              <a
                [routerLink]="item.ruta"
                [attr.aria-label]="item.etiqueta"
                class="flex h-12 w-12 items-center justify-center rounded-full bg-[#31BBA1] text-[#081728] shadow-lg shadow-[#31BBA1]/40 transition hover:bg-[#5eead4] focus-visible:outline-white"
              >
                <ui-icon [nombre]="item.icono" />
                <span class="sr-only">{{ item.etiqueta }}</span>
              </a>
            } @else {
              <a
                [routerLink]="item.ruta"
                [routerLinkActiveOptions]="{ exact: item.ruta === '/mi' }"
                routerLinkActive="text-[#31BBA1] bg-[#0c2540]"
                [attr.aria-label]="item.etiqueta"
                class="flex min-h-11 min-w-11 flex-col items-center justify-center rounded-full px-3 py-2 text-slate-300 transition hover:bg-[#0c2540]/70 hover:text-white focus-visible:outline-white"
              >
                <ui-icon [nombre]="item.icono" />
                <span class="sr-only">{{ item.etiqueta }}</span>
              </a>
            }
          </li>
        }
      </ul>
    </nav>
  `,
})
export class MobileDockComponent {
  protected readonly itemsDock = ITEMS_DOCK_MOVIL;
}
