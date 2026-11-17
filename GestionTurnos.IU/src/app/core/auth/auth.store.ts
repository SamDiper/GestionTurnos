import { Injectable, computed, inject, signal } from '@angular/core';
import { LoginResponseDto, PERMISOS, UsuarioSesion } from '../models';
import { StorageService } from '../services';

const CLAVE_TOKEN = 'gt_access_token';
const CLAVE_USUARIO = 'gt_usuario_sesion';

@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly storage = inject(StorageService);

  private readonly _token = signal<string | null>(this.storage.obtener(CLAVE_TOKEN));
  private readonly _usuario = signal<UsuarioSesion | null>(this.leerUsuarioInicial());

  readonly token = this._token.asReadonly();
  readonly usuario = this._usuario.asReadonly();
  readonly autenticado = computed(() => Boolean(this._token() && this._usuario()));
  readonly permisos = computed(() => new Set<string>(this._usuario()?.permisos ?? []));
  readonly tieneAlgunPermisoAdmin = computed(() =>
    Object.values(PERMISOS).some((codigo) => this.permisos().has(codigo))
  );
  readonly esAdmin = computed(() => {
    const rol = this._usuario()?.rol?.toLowerCase() ?? '';
    return rol === 'administrador' || rol === 'admin' || this.tieneAlgunPermisoAdmin();
  });

  puede(codigoPermiso: string): boolean {
    return this.permisos().has(codigoPermiso);
  }

  establecerSesion(respuesta: LoginResponseDto): void {
    const sesion: UsuarioSesion = {
      idUsuario: respuesta.idUsuario,
      correo: respuesta.correo,
      rol: respuesta.rol,
      idEmpleado: respuesta.idEmpleado,
      nombreCompleto: respuesta.nombreCompleto || respuesta.correo,
      permisos: respuesta.permisos ?? [],
      expiraEn: respuesta.expiraEn,
    };
    this._token.set(respuesta.accessToken);
    this._usuario.set(sesion);
    this.storage.guardar(CLAVE_TOKEN, respuesta.accessToken);
    this.storage.guardar(CLAVE_USUARIO, JSON.stringify(sesion));
  }

  limpiarSesion(): void {
    this._token.set(null);
    this._usuario.set(null);
    this.storage.eliminar(CLAVE_TOKEN);
    this.storage.eliminar(CLAVE_USUARIO);
  }

  private leerUsuarioInicial(): UsuarioSesion | null {
    const crudo = this.storage.obtener(CLAVE_USUARIO);
    if (!crudo) return null;
    try {
      return JSON.parse(crudo) as UsuarioSesion;
    } catch {
      return null;
    }
  }
}
