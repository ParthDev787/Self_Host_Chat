import { Component, inject, signal, computed, OnInit, OnDestroy, ViewChild, ElementRef, HostListener } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccountService } from '../../core/services/account-service';
import { ToastService } from '../../core/services/toast-service';
import { ConnectionService } from '../../core/services/connection-service';
import { CryptoService } from '../../core/services/crypto-service';
import { Contact, ChatMessage, ConnectionRequest } from '../../types/user';

function getAvatarColor(username: string): string {
  const colors = ['#6366f1', '#ec4899', '#10b981', '#f59e0b', '#3b82f6', '#8b5cf6', '#14b8a6', '#f43f5e'];
  let hash = 0;
  for (let i = 0; i < (username || '').length; i++) {
    hash = username.charCodeAt(i) + ((hash << 5) - hash);
  }
  return colors[Math.abs(hash) % colors.length];
}

export function extractTargetUsername(raw: string): string {
  if (!raw) return '';
  let str = raw.trim();

  // If query string contains ?join= or &join=
  if (str.includes('join=')) {
    const match = str.match(/[?&]join=([^&#/?\s]+)/i);
    if (match && match[1]) return decodeURIComponent(match[1]).trim();
  }
  // If query string contains ?user= or &user=
  if (str.includes('user=')) {
    const match = str.match(/[?&]user=([^&#/?\s]+)/i);
    if (match && match[1]) return decodeURIComponent(match[1]).trim();
  }
  // If URL path contains /connect/username
  if (str.includes('/connect/')) {
    const match = str.match(/\/connect\/([^&#/?\s]+)/i);
    if (match && match[1]) return decodeURIComponent(match[1]).trim();
  }

  // If URL object parsing works
  try {
    const testUrl = str.startsWith('http://') || str.startsWith('https://') ? str : `http://${str}`;
    const url = new URL(testUrl);
    const j = url.searchParams.get('join') || url.searchParams.get('user');
    if (j) return j.trim();
  } catch {}

  // Strip leading/trailing slashes or queries
  str = str.replace(/^[/#?]+/, '').replace(/[/#?]+$/, '');
  return str.trim();
}

function formatDisplayTime(dateStr: string): string {
  if (!dateStr) return 'Just now';
  try {
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return dateStr;
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  } catch {
    return dateStr;
  }
}

@Component({
  selector: 'app-home',
  imports: [FormsModule],
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class Home implements OnInit, OnDestroy {
  protected accountService = inject(AccountService);
  private toastService = inject(ToastService);
  private connectionService = inject(ConnectionService);
  protected cryptoService = inject(CryptoService);

  // ── Auth state ────────────────────────────
  isLoginMode = signal(true);
  loginModel = { username: '', password: '' };
  registerModel = { username: '', password: '' };

  // ── Dashboard state ───────────────────────
  selectedContact = signal<Contact | null>(null);
  showProfileMenu = signal(false);
  showEditProfile = signal(false);
  showConnectPopup = signal(false);
  selectedMessageForInspection = signal<ChatMessage | null>(null);
  connectTab = signal<'share' | 'join'>('share');
  joinInput = signal('');
  joinMessage = signal('');
  showNotifications = signal(false);
  messageText = signal('');
  searchText = signal('');
  connectionLink = signal('');

  // ── Theme State in Profile Menu ───────────
  showThemeMenu = signal(false);
  currentTheme = signal<string>(typeof localStorage !== 'undefined' ? (localStorage.getItem('theme') || 'dark') : 'dark');
  availableThemes = [
    'dark', 'synthwave', 'dracula', 'night', 'luxury', 'forest', 'dim', 'coffee', 'black', 'sunset',
    'light', 'nord', 'emerald', 'corporate', 'retro', 'cyberpunk', 'valentine', 'aqua', 'cupcake', 'winter', 'autumn', 'business'
  ];

  toggleThemeMenu(event?: MouseEvent) {
    if (event) {
      event.stopPropagation();
    }
    this.showThemeMenu.update(v => !v);
  }

  changeTheme(theme: string) {
    this.currentTheme.set(theme);
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem('theme', theme);
    }
    if (typeof document !== 'undefined') {
      document.documentElement.setAttribute('data-theme', theme);
    }
  }

  // ── Smart Scroll to Unread State ──────────
  firstUnreadMessageId = signal<string | null>(null);
  unreadCountInChat = signal<number>(0);
  showScrollToBottomBtn = signal<boolean>(false);
  private currentChatContact: string | null = null;
  private hasScrolledForCurrentChat = false;

  @ViewChild('messagesContainer') private messagesContainer?: ElementRef<HTMLDivElement>;
  @ViewChild('messagesEnd') private messagesEnd?: ElementRef<HTMLDivElement>;

  scrollToBottom(smooth: boolean = true) {
    setTimeout(() => {
      try {
        if (this.messagesEnd?.nativeElement) {
          this.messagesEnd.nativeElement.scrollIntoView({
            behavior: smooth ? 'smooth' : 'auto',
            block: 'end'
          });
        } else if (this.messagesContainer?.nativeElement) {
          const el = this.messagesContainer.nativeElement;
          el.scrollTop = el.scrollHeight;
        }
      } catch {}
    }, 60);
  }

  scrollToFirstUnread() {
    setTimeout(() => {
      try {
        const dividerEl = document.getElementById('unread-divider');
        const firstMsgEl = this.firstUnreadMessageId() 
          ? document.getElementById('msg-' + this.firstUnreadMessageId()) 
          : null;
        const targetEl = dividerEl || firstMsgEl;

        if (targetEl && this.messagesContainer?.nativeElement) {
          const container = this.messagesContainer.nativeElement;
          const containerRect = container.getBoundingClientRect();
          const targetRect = targetEl.getBoundingClientRect();
          const relativeTop = targetRect.top - containerRect.top + container.scrollTop;

          // 28px of top padding so unread messages and divider sit comfortably with breathing room below header
          const scrollTarget = Math.max(0, relativeTop - 28);

          container.scrollTo({
            top: scrollTarget,
            behavior: 'smooth'
          });
        }
      } catch (err) {
        console.warn('Scroll to unread error:', err);
      }
    }, 90);
  }

  onMessagesScroll() {
    if (!this.messagesContainer?.nativeElement) return;
    const el = this.messagesContainer.nativeElement;
    const distanceFromBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    this.showScrollToBottomBtn.set(distanceFromBottom > 160);
  }

  // ── Live Data ─────────────────────────────
  contacts = signal<Contact[]>([]);
  notifications = signal<ConnectionRequest[]>([]);

  private intervalId: any = null;
  private lastKnownPendingCount = 0;
  private isInitialized = false;

  isLoggedIn = computed(() => {
    const u = this.accountService.currentUser();
    return !!(u && typeof u.username === 'string' && u.username.trim().length > 0 && !!u.token);
  });

  unreadNotifCount = computed(() =>
    this.notifications().filter(n => n.status === 'pending' || (n.status === 'rejected' && n.isOutgoingRejection)).length
  );

  filteredContacts = computed(() => {
    const q = this.searchText().toLowerCase();
    if (!q) return this.contacts();
    return this.contacts().filter(c => c.username.toLowerCase().includes(q));
  });

  ngOnInit() {
    if (typeof localStorage !== 'undefined' && typeof document !== 'undefined') {
      const savedTheme = localStorage.getItem('theme') || 'dark';
      this.currentTheme.set(savedTheme);
      document.documentElement.setAttribute('data-theme', savedTheme);
    }

    const user = this.accountService.currentUser();
    if (user?.username) {
      this.cryptoService.initializeIdentity(user.username);
    }

    this.checkUrlJoinParam();
    this.loadLiveData();

    // Heartbeat every 1.5 seconds to refresh requests, contacts and active chat from the API
    this.intervalId = setInterval(() => {
      this.loadLiveData();
    }, 1500);
  }

  ngOnDestroy() {
    if (this.intervalId) {
      clearInterval(this.intervalId);
    }
  }

  private checkUrlJoinParam() {
    const params = new URLSearchParams(window.location.search);
    const joinParam = params.get('join') || params.get('user');
    if (joinParam) {
      if (this.isLoggedIn()) {
        const current = this.accountService.currentUser()!;
        const target = joinParam.trim();
        if (target.toLowerCase() !== current.username.toLowerCase()) {
          this.joinInput.set(target);
          this.connectTab.set('join');
          this.showConnectPopup.set(true);
        }
        const cleanUrl = window.location.origin + window.location.pathname;
        window.history.replaceState({}, document.title, cleanUrl);
      }
    }
  }

  // ── Live Data Store Methods ───────────────
  loadLiveData() {
    if (!this.isLoggedIn()) {
      this.contacts.set([]);
      this.notifications.set([]);
      this.lastKnownPendingCount = 0;
      return;
    }

    const current = this.accountService.currentUser()!;
    const currentUsername = current.username.toLowerCase();

    // 1. Fetch live connection requests from Backend API
    this.connectionService.getRequests(currentUsername).subscribe({
      next: (reqs: any[]) => {
        const myNotifications: ConnectionRequest[] = (reqs || [])
          .filter(r => {
            const s = ((r.status || r.Status || 'pending') as string).toLowerCase();
            return s !== 'accepted';
          })
          .map(r => {
          const fromUser = r.fromUsername || r.FromUsername || 'User';
          const toUser = r.toUsername || r.ToUsername || current.username;
          const status = ((r.status || r.Status || 'pending') as string).toLowerCase() as 'pending' | 'accepted' | 'rejected';
          const rawTime = r.timestamp || r.Timestamp;
          const isOutgoingRejection = fromUser.toLowerCase() === currentUsername && status === 'rejected';
          const displayName = isOutgoingRejection ? toUser : fromUser;

          return {
            id: r.id || r.Id || Date.now().toString(),
            fromUsername: fromUser,
            toUsername: toUser,
            message: isOutgoingRejection ? 'Your join request was rejected' : (r.message || r.Message || ''),
            avatarLetter: displayName.charAt(0).toUpperCase(),
            avatarColor: getAvatarColor(displayName),
            timestamp: formatDisplayTime(rawTime),
            displayTime: formatDisplayTime(rawTime),
            status: status,
            isOutgoingRejection: isOutgoingRejection,
            displayName: displayName,
          };
        });

        this.notifications.set(myNotifications);

        const unreadItems = myNotifications.filter(n => n.status === 'pending' || (n.status === 'rejected' && n.isOutgoingRejection));
        if (this.isInitialized && unreadItems.length > this.lastKnownPendingCount) {
          const latest = unreadItems[0];
          if (latest.isOutgoingRejection) {
            this.toastService.warning(`⚠️ ${latest.displayName} rejected your connection request`);
          } else {
            this.toastService.info(`🔔 New connection request from ${latest.fromUsername}!`);
          }
        }
        this.lastKnownPendingCount = unreadItems.length;
        this.isInitialized = true;
      },
      error: () => {
        // Fallback or silent catch if server momentarily busy
      }
    });

    // 2. Fetch live connected contacts from Backend API
    this.connectionService.getContacts(currentUsername).subscribe({
      next: async (contactData: any[]) => {
        const active = this.selectedContact();
        const contactList: Contact[] = [];

        for (const c of (contactData || [])) {
          let previewText = c.lastMessage || 'Connected! Say hi 👋';
          const obj = c.lastMessageObj;

          if (obj) {
            const unlocked = await this.cryptoService.unlockMessage(
              obj.id || obj.Id,
              obj.sender || obj.Sender || '',
              obj.recipient || obj.Recipient || '',
              obj.ciphertext || obj.Ciphertext,
              obj.iv || obj.Iv,
              obj.mac || obj.Mac,
              obj.ephemeralPublicKey || obj.EphemeralPublicKey,
              obj.sequence ?? obj.Sequence ?? 0,
              obj.text || obj.Text
            );
            if (unlocked && unlocked.text) {
              previewText = unlocked.text;
            }
          }

          const isCurrentlyActive = !!(active && active.username.toLowerCase() === c.username.toLowerCase());
          const contactStatus = ((c.status || 'offline') as 'online' | 'offline');
          const lastSeenText = c.lastSeen || (contactStatus === 'online' ? 'Online' : 'Offline');

          contactList.push({
            id: c.username,
            username: c.username,
            avatarLetter: c.username.charAt(0).toUpperCase(),
            avatarColor: getAvatarColor(c.username),
            status: contactStatus,
            lastSeen: lastSeenText,
            lastSeenTime: c.lastSeenTime,
            lastMessage: previewText,
            lastMessageTime: c.lastMessageTime || 'now',
            unreadCount: isCurrentlyActive ? 0 : (c.unreadCount || 0),
            lastMessageIsMine: c.lastMessageIsMine ?? false,
            lastMessageIsRead: c.lastMessageIsRead ?? false,
            lastMessageIsDelivered: c.lastMessageIsDelivered ?? false,
            messages: [],
          });
        }

        this.contacts.set(contactList);

        // Update selected contact with live presence & last seen status
        if (active) {
          const fresh = contactList.find(c => c.username.toLowerCase() === active.username.toLowerCase());
          if (fresh) {
            this.selectedContact.update(curr => curr ? {
              ...curr,
              status: fresh.status,
              lastSeen: fresh.lastSeen,
              lastSeenTime: fresh.lastSeenTime,
            } : null);
          }
          this.loadChatMessages(currentUsername, active.username);
        }
      },
      error: () => {}
    });
  }

  loadChatMessages(currentUsername: string, contactUsername: string) {
    this.connectionService.getMessages(currentUsername, contactUsername).subscribe({
      next: async (msgs: any[]) => {
        const formattedMsgs: ChatMessage[] = [];

        for (const m of (msgs || [])) {
          const sender = m.sender || m.Sender || '';
          const recipient = m.recipient || m.Recipient || '';
          const rawText = m.text || m.Text || '';
          const ciphertext = m.ciphertext || m.Ciphertext;
          const iv = m.iv || m.Iv;
          const mac = m.mac || m.Mac;
          const ephemeralPublicKey = m.ephemeralPublicKey || m.EphemeralPublicKey;
          const sequence = m.sequence ?? m.Sequence ?? 0;
          const isRead = m.isRead ?? m.IsRead ?? false;
          const isDelivered = m.isDelivered ?? m.IsDelivered ?? true;
          const readAt = m.readAt ?? m.ReadAt;

          // Unlock message via Signal Protocol (HMAC verify + AES-256 decrypt)
          const unlocked = await this.cryptoService.unlockMessage(
            m.id || m.Id,
            sender,
            recipient,
            ciphertext,
            iv,
            mac,
            ephemeralPublicKey,
            sequence,
            rawText
          );

          formattedMsgs.push({
            id: m.id || m.Id,
            senderUsername: sender,
            recipientUsername: recipient,
            text: unlocked.text,
            ciphertext: ciphertext,
            iv: iv,
            mac: mac,
            ephemeralPublicKey: ephemeralPublicKey,
            sequence: sequence,
            isEncrypted: unlocked.isEncrypted,
            isDecrypted: true,
            verificationStatus: unlocked.verificationStatus,
            isRead: isRead,
            isDelivered: isDelivered,
            readAt: readAt,
            timestamp: formatDisplayTime(m.timestamp || m.Timestamp),
            isMine: (sender || '').toLowerCase() === currentUsername.toLowerCase(),
          });
        }

        const active = this.selectedContact();
        if (active && active.username.toLowerCase() === contactUsername.toLowerCase()) {
          const prevCount = active.messages?.length || 0;
          this.selectedContact.set({
            ...active,
            messages: formattedMsgs,
          });

          // Smart scroll logic:
          if (!this.hasScrolledForCurrentChat) {
            // Find incoming unread messages
            const unreads = formattedMsgs.filter(m => !m.isMine && !m.isRead);

            if (unreads.length > 0) {
              // Position view at the first unread message with top padding
              const firstUnread = unreads[0];
              this.firstUnreadMessageId.set(firstUnread.id);
              this.unreadCountInChat.set(unreads.length);
              this.scrollToFirstUnread();

              // Mark messages as read on backend and clear contact unread badge
              this.connectionService.markMessagesAsRead(currentUsername, contactUsername).subscribe({
                next: () => {
                  active.unreadCount = 0;
                  this.contacts.update(list => list.map(c =>
                    c.username.toLowerCase() === contactUsername.toLowerCase() ? { ...c, unreadCount: 0 } : c
                  ));
                },
                error: () => {}
              });
            } else {
              // No unread messages: smooth scroll directly to bottom
              this.firstUnreadMessageId.set(null);
              this.unreadCountInChat.set(0);
              this.scrollToBottom(false);
            }
            this.hasScrolledForCurrentChat = true;
          } else {
            // Subsequent heartbeat updates while chat is open:
            const incomingUnreads = formattedMsgs.filter(m => !m.isMine && !m.isRead);
            if (incomingUnreads.length > 0) {
              this.connectionService.markMessagesAsRead(currentUsername, contactUsername).subscribe({
                next: () => {
                  for (const u of incomingUnreads) {
                    u.isRead = true;
                  }
                  active.unreadCount = 0;
                  this.contacts.update(list => list.map(c =>
                    c.username.toLowerCase() === contactUsername.toLowerCase() ? { ...c, unreadCount: 0 } : c
                  ));
                },
                error: () => {}
              });
            }

            if (formattedMsgs.length !== prevCount) {
              // If there are no unread messages currently being reviewed, scroll to new message
              if (!this.firstUnreadMessageId()) {
                this.scrollToBottom(true);
              }
            }
          }
        }
      },
      error: () => {}
    });
  }

  // ── Auth Methods ──────────────────────────
  login() {
    if (!this.loginModel.username || !this.loginModel.password) {
      this.toastService.warning('Please enter both username and password');
      return;
    }
    const uname = this.loginModel.username.trim().toLowerCase();
    this.accountService.login(this.loginModel).subscribe({
      next: () => {
        this.cryptoService.initializeIdentity(uname);
        this.toastService.success(`Welcome back, ${uname}!`);
        this.loginModel = { username: '', password: '' };
        this.loadLiveData();
        this.checkUrlJoinParam();
      },
    });
  }

  register() {
    if (!this.registerModel.username || !this.registerModel.password) {
      this.toastService.warning('Please enter both username and password');
      return;
    }
    const uname = this.registerModel.username.trim().toLowerCase();
    this.accountService.register(this.registerModel).subscribe({
      next: () => {
        this.cryptoService.initializeIdentity(uname);
        this.toastService.success(`Welcome ${uname}! Account created.`);
        this.registerModel = { username: '', password: '' };
        this.loadLiveData();
        this.checkUrlJoinParam();
      },
    });
  }

  @HostListener('window:beforeunload')
  onBeforeUnload() {
    const current = this.accountService.currentUser();
    if (current?.username) {
      try {
        const payload = JSON.stringify({ username: current.username });
        navigator.sendBeacon('/api/connections/presence/offline', new Blob([payload], { type: 'application/json' }));
      } catch {}
    }
  }

  logout() {
    const current = this.accountService.currentUser();
    if (current?.username) {
      this.connectionService.setOffline(current.username).subscribe({
        next: () => {},
        error: () => {}
      });
    }
    this.accountService.logout();
    this.selectedContact.set(null);
    this.closeDropdowns();
    this.toastService.info('You have logged out');
  }

  // ── Contact / Chat Methods ────────────────
  selectContact(contact: Contact) {
    this.currentChatContact = contact.username.toLowerCase();
    this.hasScrolledForCurrentChat = false;
    this.unreadCountInChat.set(contact.unreadCount || 0);
    this.firstUnreadMessageId.set(null);
    contact.unreadCount = 0;
    this.selectedContact.set(contact);

    // Immediately clear unread badge in contacts list for this open contact
    this.contacts.update(list => list.map(c =>
      c.username.toLowerCase() === contact.username.toLowerCase() ? { ...c, unreadCount: 0 } : c
    ));

    const current = this.accountService.currentUser();
    if (current) {
      this.connectionService.markMessagesAsRead(current.username, contact.username).subscribe({
        next: () => {},
        error: () => {}
      });
      this.cryptoService.getPeerPublicKey(contact.username);
      this.loadChatMessages(current.username, contact.username);
    }
  }

  async sendMessage() {
    const text = this.messageText().trim();
    const contact = this.selectedContact();
    const current = this.accountService.currentUser();
    if (!text || !contact || !current) return;

    try {
      // Step 1–5: Signal Protocol (X3DH, Double Ratchet, AES-256, HMAC-SHA256)
      let cryptoPayload: any = null;
      try {
        cryptoPayload = await this.cryptoService.lockMessage(contact.username, text);
      } catch (err: any) {
        console.warn('E2EE key session unavailable, falling back:', err);
      }

      this.connectionService.sendMessage(
        current.username, 
        contact.username, 
        text, 
        cryptoPayload
      ).subscribe({
        next: () => {
          this.messageText.set('');
          this.firstUnreadMessageId.set(null);
          this.unreadCountInChat.set(0);
          this.scrollToBottom(true);
          this.contacts.update(list => list.map(c => {
            if (c.username.toLowerCase() === contact.username.toLowerCase()) {
              return {
                ...c,
                lastMessage: text,
                lastMessageIsMine: true,
                lastMessageIsDelivered: true,
                lastMessageIsRead: false,
                lastMessageTime: 'now'
              };
            }
            return c;
          }));
          this.loadChatMessages(current.username, contact.username);
          this.loadLiveData();
        },
        error: (err) => {
          this.toastService.error(err.error?.error || 'Failed to send message');
        }
      });
    } catch (err: any) {
      this.toastService.error(err.message || 'Encryption error');
    }
  }

  // ── Dropdown Toggles ──────────────────────
  toggleProfileMenu() {
    this.showProfileMenu.update(v => {
      if (v) {
        this.showThemeMenu.set(false);
      }
      return !v;
    });
    this.showNotifications.set(false);
  }

  toggleNotifications() {
    this.showNotifications.update(v => !v);
    this.showProfileMenu.set(false);
    this.showThemeMenu.set(false);
  }

  closeDropdowns() {
    this.showProfileMenu.set(false);
    this.showThemeMenu.set(false);
    this.showNotifications.set(false);
  }

  // ── Profile ───────────────────────────────
  openEditProfile() {
    this.showEditProfile.set(true);
    this.showProfileMenu.set(false);
  }


  // ── Connect People ────────────────────────
  openConnectPopup() {
    const user = this.accountService.currentUser()?.username || 'user';
    this.connectionLink.set(`${window.location.origin}/?join=${encodeURIComponent(user)}`);
    this.connectTab.set('share');
    this.joinInput.set('');
    this.joinMessage.set('');
    this.showConnectPopup.set(true);
  }

  copyLink() {
    navigator.clipboard.writeText(this.connectionLink());
    this.toastService.success('Connection link copied to clipboard!');
  }

  sendJoinRequest() {
    const raw = this.joinInput().trim();
    const current = this.accountService.currentUser();
    if (!raw || !current) {
      this.toastService.warning('Please enter a connection link or username');
      return;
    }

    const target = extractTargetUsername(raw);
    if (!target) {
      this.toastService.warning('Invalid username or link');
      return;
    }

    const currentUsername = current.username.toLowerCase();
    const targetLower = target.toLowerCase();

    if (targetLower === currentUsername) {
      this.toastService.warning('You cannot connect with yourself!');
      return;
    }

    // Send to Backend API
    this.connectionService.sendRequest(current.username, target, this.joinMessage().trim()).subscribe({
      next: () => {
        this.toastService.success(`Connection request sent to ${target}! 🚀`);
        this.showConnectPopup.set(false);
        this.joinInput.set('');
        this.joinMessage.set('');
        this.loadLiveData();
      },
      error: (err) => {
        const errorMsg = err.error?.error || err.error || 'Failed to send request';
        this.toastService.warning(errorMsg);
      }
    });
  }

  // ── Notification Actions ──────────────────
  acceptRequest(req: ConnectionRequest) {
    this.connectionService.acceptRequest(req.id).subscribe({
      next: () => {
        this.notifications.update(list => list.filter(n => n.id !== req.id));
        this.toastService.success(`You are now connected with ${req.fromUsername}! 🎉`);
        this.loadLiveData();
      },
      error: (err) => {
        this.toastService.error(err.error?.error || 'Failed to accept request');
      }
    });
  }

  rejectRequest(req: ConnectionRequest) {
    this.connectionService.rejectRequest(req.id).subscribe({
      next: () => {
        this.toastService.info(`Rejected request from ${req.fromUsername}`);
        this.loadLiveData();
      },
      error: (err) => {
        this.toastService.error(err.error?.error || 'Failed to reject request');
      }
    });
  }

  dismissNotification(req: ConnectionRequest) {
    this.connectionService.dismissRequest(req.id).subscribe({
      next: () => {
        this.notifications.update(list => list.filter(n => n.id !== req.id));
        this.toastService.info('Notification dismissed');
        this.loadLiveData();
      },
      error: () => {
        this.notifications.update(list => list.filter(n => n.id !== req.id));
        this.loadLiveData();
      }
    });
  }

  // ── Helpers ───────────────────────────────
  getInitial(): string {
    return this.accountService.currentUser()?.username?.charAt(0)?.toUpperCase() || '?';
  }
}
