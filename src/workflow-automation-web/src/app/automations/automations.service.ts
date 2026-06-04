import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Automation, SaveAutomation, Run } from '../core/models';

@Injectable({ providedIn: 'root' })
export class AutomationsService {
  private http = inject(HttpClient);
  private api = environment.apiBaseUrl;

  list() {
    return this.http.get<Automation[]>(`${this.api}/automations`);
  }

  get(id: string) {
    return this.http.get<Automation>(`${this.api}/automations/${id}`);
  }

  create(req: SaveAutomation) {
    return this.http.post<Automation>(`${this.api}/automations`, req);
  }

  update(id: string, req: SaveAutomation) {
    return this.http.put<Automation>(`${this.api}/automations/${id}`, req);
  }

  setEnabled(id: string, enabled: boolean) {
    return this.http.patch(`${this.api}/automations/${id}/enabled`, { enabled });
  }

  delete(id: string) {
    return this.http.delete(`${this.api}/automations/${id}`);
  }

  test(id: string) {
    return this.http.post<Run>(`${this.api}/automations/${id}/test`, {});
  }
}
