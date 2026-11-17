import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, of, tap } from 'rxjs';
import {
  LoginRequest,
  LoginResponseDto,
  SolicitarRegistroRequest,
  TipoDocumentoDto,
} from '../models';
import { ApiClient } from '../services';
import { AuthStore } from './auth.store';

const TIPOS_DOC_RESPALDO: readonly TipoDocumentoDto[] = [
  { codTipoDocumento: 'CC', nombre: 'Cédula de Ciudadanía', activo: true },
  { codTipoDocumento: 'CE', nombre: 'Cédula de Extranjería', activo: true },
  { codTipoDocumento: 'PAS', nombre: 'Pasaporte', activo: true },
  { codTipoDocumento: 'PEP', nombre: 'Permiso Especial de Permanencia', activo: true },
];

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiClient);
  private readonly store = inject(AuthStore);
  private readonly router = inject(Router);

  iniciarSesion(credenciales: LoginRequest): Observable<LoginResponseDto> {
    return this.api
      .post<LoginResponseDto>('/auth/login', credenciales)
      .pipe(tap((respuesta) => this.store.establecerSesion(respuesta)));
  }

  solicitarRegistro(solicitud: SolicitarRegistroRequest): Observable<number> {
    return this.api.post<number>('/auth/solicitar-registro', solicitud);
  }

  listarTiposDocumento(): Observable<readonly TipoDocumentoDto[]> {
    return this.api
      .get<readonly TipoDocumentoDto[]>('/parametrizacion/tipos-documento', { soloActivos: true })
      .pipe(catchError(() => of(TIPOS_DOC_RESPALDO)));
  }

  cerrarSesion(): void {
    this.store.limpiarSesion();
    void this.router.navigate(['/ingresar']);
  }

  rutaInicialSegunPermisos(): string {
    return this.store.tieneAlgunPermisoAdmin() && !this.store.usuario()?.idEmpleado
      ? '/admin'
      : '/mi';
  }
}
