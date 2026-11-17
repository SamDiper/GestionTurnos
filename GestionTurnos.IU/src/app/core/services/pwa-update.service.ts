import { Injectable, inject, signal } from '@angular/core';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { filter } from 'rxjs';

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  readonly userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

@Injectable({ providedIn: 'root' })
export class PwaUpdateService {
  private readonly swUpdate = inject(SwUpdate, { optional: true });
  private eventoInstalacion: BeforeInstallPromptEvent | null = null;

  private readonly _actualizacionDisponible = signal(false);
  private readonly _instalable = signal(false);

  readonly actualizacionDisponible = this._actualizacionDisponible.asReadonly();
  readonly instalable = this._instalable.asReadonly();

  constructor() {
    if (this.swUpdate?.isEnabled) {
      this.swUpdate.versionUpdates
        .pipe(filter((evt): evt is VersionReadyEvent => evt.type === 'VERSION_READY'))
        .subscribe(() => this._actualizacionDisponible.set(true));
    }

    if (typeof window !== 'undefined') {
      window.addEventListener('beforeinstallprompt', (evento) => {
        evento.preventDefault();
        this.eventoInstalacion = evento as BeforeInstallPromptEvent;
        this._instalable.set(true);
      });
    }
  }

  async aplicarActualizacion(): Promise<void> {
    if (this.swUpdate?.isEnabled) {
      await this.swUpdate.activateUpdate();
      location.reload();
    }
  }

  async instalarPwa(): Promise<void> {
    if (!this.eventoInstalacion) return;
    await this.eventoInstalacion.prompt();
    const { outcome } = await this.eventoInstalacion.userChoice;
    if (outcome === 'accepted') {
      this._instalable.set(false);
      this.eventoInstalacion = null;
    }
  }

  posponerAvisoActualizacion(): void {
    this._actualizacionDisponible.set(false);
  }
}
