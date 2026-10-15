import { Component, inject, signal } from '@angular/core';
import { AppError } from '../../../types/erro';
import { Router } from '@angular/router';

@Component({
  selector: 'app-server-error',
  imports: [],
  templateUrl: './server-error.html',
  styleUrl: './server-error.css',
})
export class ServerError {
  protected error = signal<AppError | null>(null);
  private router = inject(Router);
  protected showDetails = false;

  constructor() {
    const navigation = this.router.getCurrentNavigation();
    this.error.set(navigation?.extras.state?.['error'] ?? history.state?.['error'] ?? null);
  }

  detailsToggle() {
    this.showDetails = !this.showDetails;
  }
}
