import { Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'dating_app_client_logs';

@Injectable({
  providedIn: 'root'
})
export class LoggerService {
  constructor() {
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {}
  }

  clearLogs() {
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {}
  }
}

