import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Connection, AuthorizeUrl } from '../core/models';

@Injectable({ providedIn: 'root' })
export class ConnectionsService {
  private http = inject(HttpClient);
  private api = environment.apiBaseUrl;

  list() {
    return this.http.get<Connection[]>(`${this.api}/connections`);
  }
  authorize(provider: string) {
    return this.http.get<AuthorizeUrl>(`${this.api}/connections/${provider}/authorize`);
  }
  disconnect(id: string) {
    return this.http.delete(`${this.api}/connections/${id}`);
  }
}
