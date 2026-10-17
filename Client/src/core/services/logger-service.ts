import { Injectable, signal } from '@angular/core';

export interface ClientLogEntry {
  id: string;
  correlationId?: string; // Correlation ID linking to backend
  timestamp: string;      // ISO string
  displayTime: string;    // Formatted date & time
  method: string;         // GET, POST, etc.
  url: string;            // Request URL
  status: number;         // HTTP status code
  statusText?: string;    // OK, Bad Request, etc.
  durationMs: number;     // Elapsed time in ms
  message?: string;       // Optional details/error
  timeMs: number;         // Epoch timestamp for 7-day purge
}

const STORAGE_KEY = 'dating_app_client_logs';
const SEVEN_DAYS_MS = 7 * 24 * 60 * 60 * 1000; // 7 days in milliseconds

@Injectable({
  providedIn: 'root'
})
export class LoggerService {
  logs = signal<ClientLogEntry[]>([]);

  constructor() {
    this.loadLogs();
  }

  /**
   * Log an HTTP request with automatic 7-day retention cleanup and correlation ID
   */
  logRequest(
    method: string,
    url: string,
    status: number,
    durationMs: number,
    statusText = '',
    message = '',
    correlationId = ''
  ) {
    const now = new Date();
    const entry: ClientLogEntry = {
      id: `${Date.now()}_${Math.random().toString(36).substring(2, 7)}`,
      correlationId: correlationId || '',
      timestamp: now.toISOString(),
      displayTime: now.toLocaleString(),
      method: (method || 'GET').toUpperCase(),
      url,
      status,
      statusText: statusText || (status >= 200 && status < 300 ? 'OK' : 'Error'),
      durationMs: Math.round(durationMs),
      message,
      timeMs: now.getTime(),
    };

    let allLogs = this.readFromStorage();

    // Add new log entry at the beginning
    allLogs.unshift(entry);

    // Enforce 7-Day Retention: prune anything older than 7 days
    const cutoff = Date.now() - SEVEN_DAYS_MS;
    allLogs = allLogs.filter(log => log.timeMs >= cutoff);

    // Limit maximum log count to prevent localStorage exhaustion
    if (allLogs.length > 500) {
      allLogs = allLogs.slice(0, 500);
    }

    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(allLogs));
    } catch {
      allLogs = allLogs.slice(0, 100);
      try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(allLogs));
      } catch {}
    }

    this.logs.set(allLogs);
  }

  loadLogs(): ClientLogEntry[] {
    let allLogs = this.readFromStorage();
    const cutoff = Date.now() - SEVEN_DAYS_MS;
    allLogs = allLogs.filter(log => log.timeMs >= cutoff);
    this.logs.set(allLogs);
    return allLogs;
  }

  private readFromStorage(): ClientLogEntry[] {
    try {
      const data = localStorage.getItem(STORAGE_KEY);
      if (!data) return [];
      const parsed = JSON.parse(data);
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }

  /**
   * Generates and downloads a .log text file with correlation IDs matching backend logs
   */
  downloadLogFile() {
    const currentLogs = this.loadLogs();
    const header = [
      '=================================================================',
      '  CLIENT HTTP REQUEST LOGS (LAST 7 DAYS) WITH CORRELATION IDS',
      `  Generated At: ${new Date().toISOString()} (${new Date().toLocaleString()})`,
      `  Total Entries: ${currentLogs.length}`,
      '=================================================================\n\n',
    ].join('\n');

    const lines = currentLogs.map(l => {
      const cid = l.correlationId ? `[${l.correlationId}]` : '[none]';
      const statusStr = l.status ? `${l.status} ${l.statusText || ''}`.trim() : 'PENDING';
      let line = `${l.timestamp.replace('T', ' ').slice(0, 19)} [INF] ${cid} ${l.method} ${l.url} -> ${statusStr} (${l.durationMs}ms)`;
      if (l.message) {
        line += ` | ${l.message}`;
      }
      return line;
    });

    const fileContent = header + lines.join('\n');
    const blob = new Blob([fileContent], { type: 'text/plain;charset=utf-8' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `frontend-${new Date().toISOString().slice(0, 10).replace(/-/g, '')}.log`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    window.URL.revokeObjectURL(url);
  }

  clearLogs() {
    localStorage.removeItem(STORAGE_KEY);
    this.logs.set([]);
  }
}
