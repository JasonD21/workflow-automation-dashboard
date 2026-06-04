import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { TriggerDefinition, ActionDefinition } from '../core/models';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private http = inject(HttpClient);
  private api = environment.apiBaseUrl;
  triggers() {
    return this.http.get<TriggerDefinition[]>(`${this.api}/catalog/triggers`);
  }
  actions() {
    return this.http.get<ActionDefinition[]>(`${this.api}/catalog/actions`);
  }
}
