import { Component, inject, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccountService } from '../../../core/services/account-service';
import { ToastService } from '../../../core/services/toast-service';

@Component({
  selector: 'app-register',
  imports: [FormsModule],
  templateUrl: './register.html',
  styleUrl: './register.css'
})
export class Register {
  private accountService = inject(AccountService);
  private toastService = inject(ToastService);

  cancelRegister = output<boolean>();

  model = {
    username: '',
    password: ''
  };

  register() {
    if (!this.model.username || !this.model.password) {
      this.toastService.warning('Please enter both username and password');
      return;
    }

    this.accountService.register(this.model).subscribe({
      next: () => {
        this.toastService.success(`Welcome ${this.model.username}! Account created.`);
        this.cancel();
      }
    });
  }

  cancel() {
    this.cancelRegister.emit(false);
  }
}
