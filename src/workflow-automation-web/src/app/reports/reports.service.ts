import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import {
  ReportSchedule,
  SaveReportSchedule,
  GeneratedReportSummary,
  GeneratedReportDetail,
  Paged,
} from '../core/models';

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private http = inject(HttpClient);
  private api = environment.apiBaseUrl;

  listSchedules() {
    return this.http.get<ReportSchedule[]>(`${this.api}/report-schedules`);
  }

  createSchedule(r: SaveReportSchedule) {
    return this.http.post<ReportSchedule>(`${this.api}/report-schedules`, r);
  }

  updateSchedule(id: string, r: SaveReportSchedule) {
    return this.http.put<ReportSchedule>(`${this.api}/report-schedules/${id}`, r);
  }

  deleteSchedule(id: string) {
    return this.http.delete(`${this.api}/report-schedules/${id}`);
  }

  generate(id: string) {
    return this.http.post<GeneratedReportDetail>(`${this.api}/report-schedules/${id}/generate`, {});
  }

  listReports(page = 1, pageSize = 20) {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<Paged<GeneratedReportSummary>>(`${this.api}/reports`, { params });
  }
  getReport(id: string) {
    return this.http.get<GeneratedReportDetail>(`${this.api}/reports/${id}`);
  }
}
