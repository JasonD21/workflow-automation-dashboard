import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { DashboardSummary } from '../core/models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private http = inject(HttpClient);
  getSummary() {
    return this.http.get<DashboardSummary>(`${environment.apiBaseUrl}/dashboard/summary`);
  }
}
