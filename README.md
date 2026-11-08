# 🔐 Self-Host Chat

A **private, self-hosted messaging app** you can run entirely on your own machine or server — no third-party cloud, no data leaving your control.

Every message is **end-to-end encrypted** using the Signal Protocol (the same cryptography WhatsApp uses), so even the server never sees your plaintext messages.

---

## What Does It Do?

| Feature | Description |
|---|---|
| 🔐 **Register / Login** | Create an account with a username and password. Your session is kept alive with secure refresh tokens. |
| 🔗 **Connect with People** | Share your personal invite link or enter someone else's link/username to send them a connection request. |
| ✅ **Accept / Reject Requests** | Incoming connection requests appear as notifications — accept to become contacts, reject to decline. |
| 💬 **Private Chat** | Once connected, you can send and receive private messages in real-time (via polling every 4 seconds). |
| 🔒 **End-to-End Encryption** | Messages are encrypted on your device before sending, and decrypted only on the recipient's device. The server stores only ciphertext. |
| 🟢 **Online Presence** | See if your contacts are currently online or when they were last seen. |
| 📬 **Unread Badges & Read Receipts** | See how many unread messages you have per contact; messages show ✓✓ when read. |
| 🎨 **21 Themes** | Pick from 21 visual themes (dark, synthwave, dracula, cyberpunk, and more) — saved to your browser. |
| 🐛 **Issue & Feedback Reporting** | WhatsApp/Facebook style floating popup to report bugs, send suggestions, and attach up to 3 images (max 200 KB each) + 1 video, delivered directly to your email inbox. |

---

## Tech Stack

### Backend — `API/`
Built with **ASP.NET Core 9** (C#), running as a REST API.

| Layer | What it does |
|---|---|
| `Controllers/` | HTTP endpoints — handles requests from the browser |
| `Entities/` | Database models (User, Message, ConnectionRequest, etc.) |
| `DTOs/` | Shapes the data sent to/from the client |
| `Services/` | Business logic (account management, connections, tokens) |
| `Repositories/` | Database queries (EF Core) |
| `Interface/` | Service contracts (dependency injection) |
| `Middleware/` | Global error handling |
| `Extensions/` | App configuration helpers |
| `Helpers/` | Utility classes |

### Frontend — `Client/`
Built with **Angular 21**, styled with **Tailwind CSS v4** + **DaisyUI v5**.

| Folder | What it does |
|---|---|
| `core/services/` | API calls and crypto logic (account, connection, crypto, toast) |
| `features/home/` | The main chat dashboard (contacts, messages, notifications) |
| `features/register/` | The auth page (login + register form) |
| `layout/nav/` | Top navbar with theme switcher |
| `shared/error/` | 404 and 500 error pages |
| `types/` | TypeScript type definitions |

---

## Project Structure

```
Real_DatingApp/
│
├── API/                          # ASP.NET Core 9 Web API
│   ├── Controllers/              # AccountController, ConnectionsController
│   ├── Entities/                 # AppUser, ChatMessageEntity, ConnectionRequest, etc.
│   ├── DTOs/                     # Request & response data shapes
│   ├── Data/                     # EF Core DataContext & Migrations
│   ├── Services/                 # AccountService, ConnectionService, TokenService
│   ├── Repositories/             # UserRepository, MessageRepository, ConnectionRepository
│   ├── Interface/                # IAccountService, IConnectionService, etc.
│   ├── Middleware/               # Exception middleware
│   ├── Extensions/               # Service registration helpers
│   └── Helpers/                  # Utility classes
│
└── Client/                       # Angular 21 Frontend
    └── src/
        ├── core/
        │   ├── services/         # account-service, connection-service, crypto-service, toast-service
        │   └── interceptor/      # HTTP error interceptor
        ├── features/
        │   ├── home/             # Main chat UI (contacts list + chat window)
        │   └── register/         # Login & Register form
        ├── layout/nav/           # Navbar + theme switcher
        ├── shared/error/         # 404 Not Found & 500 Server Error pages
        └── types/                # TypeScript interfaces (User, Contact, ChatMessage, etc.)
```

---

## How to Run It

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)

### 1. Start the Backend (API)

```powershell
cd API
dotnet build
dotnet run
```

> API will be available at `https://localhost:5001`

### 2. Start the Frontend (Client)

```powershell
cd Client
npm install
npm start
```

> App will open at `http://localhost:4200`

---

## How the App Works — Step by Step

```
1. Open the app → You see a Login / Register screen
2. Create an account → Your encryption keys are generated in-browser
3. Go to "Connect" → Copy your personal link and share it with a friend
4. Friend opens the link → They send you a connection request
5. You accept the request → They appear in your Contacts list
6. Click on a contact → The chat window opens
7. Type a message → It is encrypted on your device, sent to the server
8. Friend opens the chat → Their device decrypts and shows the message
```

No message is ever readable by the server.

---

## Security & Encryption

This app implements the **Signal Protocol** for end-to-end encryption:

| Step | What Happens |
|---|---|
| 🔑 **Key Exchange** | ECDH P-256 (X3DH) — your device and your contact's device agree on a shared secret without sending it over the wire |
| 🔄 **Key Derivation** | Double Ratchet per message with HKDF-SHA256 — each message uses a fresh key |
| 🔒 **Encryption** | AES-256-CBC with a random 16-byte IV per message |
| ✅ **Integrity** | HMAC-SHA256 — the receiver verifies the message was not tampered with |

For the full technical breakdown with sequence diagrams and math specs, see:

📄 **[SECURITY_AND_ENCRYPTION.md](SECURITY_AND_ENCRYPTION.md)**

---

## API Endpoints (Quick Reference)

| Method | Endpoint | What it does |
|---|---|---|
| `POST` | `/api/account/register` | Create a new account |
| `POST` | `/api/account/login` | Log in |
| `POST` | `/api/account/refresh` | Refresh your session token |
| `POST` | `/api/account/logout` | Log out |
| `POST` | `/api/connections/request` | Send a connection request |
| `GET` | `/api/connections/requests?username=` | Get pending requests |
| `POST` | `/api/connections/accept/{id}` | Accept a request |
| `POST` | `/api/connections/reject/{id}` | Reject a request |
| `GET` | `/api/connections/contacts?username=` | Get your contact list |
| `GET` | `/api/connections/messages?user1=&user2=` | Load a conversation |
| `POST` | `/api/connections/messages` | Send a message |
| `POST` | `/api/connections/messages/read` | Mark messages as read |
| `POST` | `/api/connections/keys` | Upload your public key |
| `GET` | `/api/connections/keys/{username}` | Get someone's public key |
| `POST` | `/api/connections/heartbeat` | Update your online status |
| `POST` | `/api/report` | Submit bug report / feedback with screenshots & video |

---

## 📬 Issue & Feedback Reporting (Email Setup)

Users can click the floating **"Report Issue"** button at the bottom-right of the screen to open a WhatsApp/Facebook-styled popup.

### Features
* **Auto User Detection:** Auto-fills with the logged-in username.
* **Categories:** Bug / Issue, Feature Suggestion, Chat / Realtime Issue, Security / E2EE, or Other.
* **Smart File Validation:**
  * **Images:** Up to 3 images, each strictly $\le$ 200 KB (with instant previews and ✕ remove buttons).
  * **Video:** Up to 1 screen recording video (max 25 MB).
* **Zero-Loss Storage:** Every report is backed up locally in `API/Reports/{reportId}/` on disk, so you never lose feedback even if your SMTP server is offline.
* **Instant Delivery:** When configured, an HTML summary with all attachments is emailed directly to your inbox.

### Configuring SMTP (in `API/appsettings.json`)

```json
"Smtp": {
  "Host": "smtp.gmail.com",
  "Port": 587,
  "Username": "your-email@gmail.com",
  "Password": "your-16-char-google-app-password",
  "FromName": "Self-Host Chat Reports",
  "ToEmail": "your-personal-or-dev-email@gmail.com"
}
```

> **Gmail Tip:** If using Gmail, enable 2-Step Verification and generate an **App Password** from your Google Account security settings. Use that 16-character code as the `Password`.

