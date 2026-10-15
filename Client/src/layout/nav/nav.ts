import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { themes } from '../theme';
import { AccountService } from '../../core/services/account-service';
import { ToastService } from '../../core/services/toast-service';

@Component({
  selector: 'app-nav',
  imports: [RouterLink, RouterLinkActive, FormsModule],
  templateUrl: './nav.html',
  styleUrl: './nav.css'
})
export class Nav implements OnInit {
  protected accountService = inject(AccountService);
  private toastService = inject(ToastService);

  protected selectedTheme = signal<string>(localStorage.getItem('theme') || 'light');
  protected themes = themes;

  model = {
    username: '',
    password: ''
  };

  ngOnInit(): void {
    document.documentElement.setAttribute('data-theme', this.selectedTheme());
  }

  handleSelectTheme(theme: string) {
    this.selectedTheme.set(theme);
    localStorage.setItem('theme', theme);
    document.documentElement.setAttribute('data-theme', theme);
    const elem = document.activeElement as HTMLElement;
    if (elem) elem.blur();
  }

  login() {
    if (!this.model.username || !this.model.password) {
      this.toastService.warning('Please enter both username and password');
      return;
    }

    this.accountService.login(this.model).subscribe({
      next: () => {
        this.toastService.success(`Welcome back, ${this.model.username}!`);
        this.model = { username: '', password: '' };
      }
    });
  }

  logout() {
    this.accountService.logout();
    this.toastService.info('You have logged out');
  }
}
