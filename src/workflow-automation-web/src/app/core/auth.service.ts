import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, finalize, firstValueFrom, map, of, shareReplay, switchMap, tap } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { AuthResponse, UserDto } from './models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private api = environment.apiBaseUrl;

  readonly accessToken = signal<string | null>(null);
  readonly user = signal<UserDto | null>(null);
  readonly isAuthenticated = computed(() => this.accessToken() !== null);
  readonly isDemo = computed(() => this.user()?.isDemo ?? false);

  private refreshInFlight$: Observable<string> | null = null;

  login(email: string, password: string) {
    return this.http
      .post<AuthResponse>(`${this.api}/auth/login`, { email, password }, { withCredentials: true })
      .pipe(
        tap((r) => this.accessToken.set(r.accessToken)),
        switchMap(() => this.loadUser()),
      );
  }

  register(email: string, password: string, displayName: string) {
    return this.http
      .post<AuthResponse>(
        `${this.api}/auth/register`,
        { email, password, displayName },
        { withCredentials: true },
      )
      .pipe(
        tap((r) => this.accessToken.set(r.accessToken)),
        switchMap(() => this.loadUser()),
      );
  }

  refresh(): Observable<string> {
    if (this.refreshInFlight$) return this.refreshInFlight$; // dedupe concurrent 401s
    this.refreshInFlight$ = this.http
      .post<AuthResponse>(`${this.api}/auth/refresh`, {}, { withCredentials: true })
      .pipe(
        tap((r) => this.accessToken.set(r.accessToken)),
        map((r) => r.accessToken),
        finalize(() => (this.refreshInFlight$ = null)),
        shareReplay(1),
      );
    return this.refreshInFlight$;
  }

  loadUser() {
    return this.http.get<UserDto>(`${this.api}/auth/me`).pipe(tap((u) => this.user.set(u)));
  }

  logout() {
    return this.http
      .post(`${this.api}/auth/logout`, {}, { withCredentials: true })
      .pipe(finalize(() => this.clearSession()));
  }

  clearSession() {
    this.accessToken.set(null);
    this.user.set(null);
  }

  // Called once at startup: restore the session from the refresh cookie if present.
  bootstrap(): Promise<unknown> {
    return firstValueFrom(
      this.refresh().pipe(
        switchMap(() => this.loadUser()),
        catchError(() => of(null)),
      ),
    );
  }
}
