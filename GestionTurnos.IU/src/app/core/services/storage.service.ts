import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class StorageService {
  obtener(clave: string): string | null {
    try {
      return typeof sessionStorage !== 'undefined' ? sessionStorage.getItem(clave) : null;
    } catch {
      return null;
    }
  }

  guardar(clave: string, valor: string): void {
    try {
      if (typeof sessionStorage !== 'undefined') {
        sessionStorage.setItem(clave, valor);
      }
    } catch {
      // Ignorado si el navegador bloquea el almacenamiento de sesión
    }
  }

  eliminar(clave: string): void {
    try {
      if (typeof sessionStorage !== 'undefined') {
        sessionStorage.removeItem(clave);
      }
    } catch {
      // Ignorado
    }
  }
}
