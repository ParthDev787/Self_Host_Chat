import { Component, ElementRef, HostListener, ViewChild, computed, effect, inject, signal, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ReportService } from '../../core/services/report-service';
import { AccountService } from '../../core/services/account-service';
import { ToastService } from '../../core/services/toast-service';

export interface ImageAttachment {
  file: File;
  previewUrl: string;
  sizeKb: number;
}

export interface VideoAttachment {
  file: File;
  name: string;
  sizeMb: number;
}

@Component({
  selector: 'app-report-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './report-modal.html',
  styleUrl: './report-modal.css'
})
export class ReportModal {
  private reportService = inject(ReportService);
  protected accountService = inject(AccountService);
  private toastService = inject(ToastService);
  private router = inject(Router);

  @ViewChild('imageFileInput') imageFileInput?: ElementRef<HTMLInputElement>;
  @ViewChild('videoFileInput') videoFileInput?: ElementRef<HTMLInputElement>;

  isOpen = computed(() => this.reportService.isModalOpen());
  isSubmitting = signal<boolean>(false);

  category = signal<string>('Bug');
  description = signal<string>('');
  customUsername = signal<string>('');

  selectedImages = signal<ImageAttachment[]>([]);
  selectedVideo = signal<VideoAttachment | null>(null);

  imageError = signal<string | null>(null);
  videoError = signal<string | null>(null);

  readonly MAX_IMAGES = 3;
  readonly MAX_IMAGE_SIZE_BYTES = 200 * 1024; // 200 KB
  readonly MAX_VIDEO_SIZE_BYTES = 25 * 1024 * 1024; // 25 MB

  categories = [
    { id: 'Bug', label: '🐞 Bug / Issue', desc: 'Something is broken or behaves unexpectedly' },
    { id: 'Suggestion', label: '💡 Feature Suggestion', desc: 'An idea or feature you would love to see' },
    { id: 'Chat Issue', label: '💬 Chat / Realtime Issue', desc: 'Messaging, presence, or connection issue' },
    { id: 'Security', label: '🔒 Security / E2EE', desc: 'Encryption, privacy, or account security' },
    { id: 'Other', label: '❓ Other / General Feedback', desc: 'General question, inquiry, or comment' }
  ];

  private returnUrl: string = '/';

  constructor() {
    effect(() => {
      const open = this.reportService.isModalOpen();
      if (open) {
        untracked(() => {
          this.resetForm();
          const currentUser = this.accountService.currentUser();
          this.customUsername.set(currentUser?.username || '');
          this.returnUrl = this.router.url.includes('not-found') ? '/' : this.router.url;
        });
      }
    }, { allowSignalWrites: true });
  }

  open() {
    this.reportService.openModal();
  }

  close() {
    if (this.isSubmitting()) return;
    this.cleanupObjectUrls();
    this.reportService.closeModal();

    // If the view was redirected to /not-found or /server-error, safely restore the previous page
    if (this.router.url.includes('not-found') || this.router.url.includes('server-error')) {
      this.router.navigateByUrl(this.returnUrl || '/');
    }
  }

  @HostListener('window:keydown.escape')
  onEscapePress() {
    if (this.isOpen() && !this.isSubmitting()) {
      this.close();
    }
  }

  onImagesSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;

    this.imageError.set(null);
    const files = Array.from(input.files);
    const currentList = [...this.selectedImages()];

    for (const file of files) {
      if (currentList.length >= this.MAX_IMAGES) {
        this.imageError.set(`Maximum of ${this.MAX_IMAGES} images allowed.`);
        break;
      }

      if (!file.type.startsWith('image/')) {
        this.imageError.set(`'${file.name}' is not an image file.`);
        continue;
      }

      if (file.size > this.MAX_IMAGE_SIZE_BYTES) {
        const sizeKb = (file.size / 1024).toFixed(1);
        this.imageError.set(
          `⚠️ '${file.name}' is ${sizeKb} KB. Each image must be 200 KB or smaller. Please compress or resize it.`
        );
        continue;
      }

      const previewUrl = URL.createObjectURL(file);
      currentList.push({
        file,
        previewUrl,
        sizeKb: Math.round(file.size / 1024)
      });
    }

    this.selectedImages.set(currentList);
    input.value = '';
  }

  removeImage(index: number) {
    this.imageError.set(null);
    const list = [...this.selectedImages()];
    if (list[index]) {
      URL.revokeObjectURL(list[index].previewUrl);
      list.splice(index, 1);
      this.selectedImages.set(list);
    }
  }

  onVideoSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;

    this.videoError.set(null);
    const file = input.files[0];

    if (!file.type.startsWith('video/')) {
      this.videoError.set(`'${file.name}' is not a valid video file.`);
      input.value = '';
      return;
    }

    if (file.size > this.MAX_VIDEO_SIZE_BYTES) {
      const sizeMb = (file.size / (1024 * 1024)).toFixed(1);
      this.videoError.set(`⚠️ '${file.name}' is ${sizeMb} MB. Maximum allowed video size is 25 MB.`);
      input.value = '';
      return;
    }

    this.selectedVideo.set({
      file,
      name: file.name,
      sizeMb: parseFloat((file.size / (1024 * 1024)).toFixed(2))
    });

    input.value = '';
  }

  removeVideo() {
    this.videoError.set(null);
    this.selectedVideo.set(null);
  }

  submitReport() {
    const user = this.customUsername().trim() || this.accountService.currentUser()?.username || 'Guest';
    const desc = this.description().trim();

    if (!desc) {
      this.toastService.warning('Please enter a description for your issue or suggestion.');
      return;
    }

    if (desc.length < 5) {
      this.toastService.warning('Please provide at least 5 characters in your description.');
      return;
    }

    this.isSubmitting.set(true);

    const formData = new FormData();
    formData.append('username', user);
    formData.append('category', this.category());
    formData.append('description', desc);

    // Append images
    for (const item of this.selectedImages()) {
      formData.append('images', item.file, item.file.name);
    }

    // Append video if any
    const video = this.selectedVideo();
    if (video) {
      formData.append('video', video.file, video.file.name);
    }

    this.reportService.submitReport(formData).subscribe({
      next: (res) => {
        this.isSubmitting.set(false);
        this.toastService.success(res.message || 'Report submitted successfully! Thank you for your feedback.');
        this.close();
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const errMsg = err.error?.message || err.error?.error || 'Failed to submit report. Please try again.';
        this.toastService.error(errMsg);
      }
    });
  }

  private resetForm() {
    this.cleanupObjectUrls();
    this.category.set('Bug');
    this.description.set('');
    this.selectedImages.set([]);
    this.selectedVideo.set(null);
    this.imageError.set(null);
    this.videoError.set(null);
    this.isSubmitting.set(false);
  }

  private cleanupObjectUrls() {
    for (const item of this.selectedImages()) {
      try {
        URL.revokeObjectURL(item.previewUrl);
      } catch {}
    }
  }
}

