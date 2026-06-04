import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Paged, Run } from '../core/models';

@Injectable({ providedIn: 'root' })
export class RunsService {
  private http = inject(HttpClient);

  list(status: string | null, page: number, pageSize: number) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    return this.http.get<Paged<Run>>(`${environment.apiBaseUrl}/runs`, { params });
  }
}
