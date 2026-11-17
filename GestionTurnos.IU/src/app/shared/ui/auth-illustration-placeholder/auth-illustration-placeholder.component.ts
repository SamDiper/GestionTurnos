import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'ui-auth-illustration-placeholder',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="relative flex w-full max-w-md flex-col items-center justify-center p-6">
      <!-- Forma orgánica violeta/índigo inspirada en la referencia visual -->
      <div
        aria-hidden="true"
        class="relative flex aspect-square w-full max-w-[380px] items-center justify-center rounded-[58%_42%_48%_52%/44%_56%_44%_56%] bg-indigo-600/90 p-8 shadow-xl"
      >
        <!-- Contenedor con texto de referencia para pegar la imagen luego -->
        <div
          class="flex h-full w-full flex-col items-center justify-center rounded-3xl border-2 border-dashed border-white/80 bg-indigo-950/40 p-6 text-center text-white backdrop-blur-xs"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.75"
            class="mb-3 h-12 w-12 text-amber-300"
            aria-hidden="true"
          >
            <rect width="18" height="18" x="3" y="3" rx="2" ry="2" />
            <circle cx="9" cy="9" r="2" />
            <path d="m21 15-3.086-3.086a2 2 0 0 0-2.828 0L6 21" />
          </svg>
          <p class="text-sm font-bold tracking-wide text-white">
            {{ tituloReferencia() }}
          </p>
          <p class="mt-2 text-xs leading-relaxed text-indigo-100">
            {{ detalleReferencia() }}
          </p>
        </div>
      </div>
      <!-- Línea base decorativa de la ilustración -->
      <div aria-hidden="true" class="mt-4 flex items-center gap-3">
        <span class="h-0.5 w-8 bg-slate-800"></span>
        <span class="h-0.5 w-36 bg-slate-800"></span>
        <span class="h-0.5 w-8 bg-slate-800"></span>
      </div>
    </div>
  `,
})
export class AuthIllustrationPlaceholderComponent {
  readonly tituloReferencia = input('Espacio reservado para ilustración de referencia');
  readonly detalleReferencia = input(
    'Pegar aquí la imagen corporativa (ej. public/auth-hero.webp · 420×420 px)'
  );
}
