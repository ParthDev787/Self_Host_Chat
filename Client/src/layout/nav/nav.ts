import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AccountService } from '../../core/services/account-service';
import { ToastService } from '../../core/services/toast-service';

@Component({
  selector: 'app-nav',
  imports: [RouterLink, RouterLinkActive, FormsModule],
  templateUrl: './nav.html',
  styleUrl: './nav.css'
})
export class Nav {
  protected accountService = inject(AccountService);
  private toastService = inject(ToastService);

  model = {
    username: '',
    password: ''
  };

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
