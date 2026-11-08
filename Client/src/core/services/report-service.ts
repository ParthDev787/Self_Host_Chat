import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ReportResponse {
  success: boolean;
  message: string;
  reportId?: string;
}

@Injectable({
  providedIn: 'root'
})
export class ReportService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl;

  isModalOpen = signal<boolean>(false);
  isOpen = this.isModalOpen;

  openModal() {
    this.isModalOpen.set(true);
  }

  closeModal() {
    this.isModalOpen.set(false);
  }

  submitReport(formData: FormData): Observable<ReportResponse> {
    return this.http.post<ReportResponse>(this.baseUrl + 'report', formData, {
      withCredentials: true
    });
  }
}

