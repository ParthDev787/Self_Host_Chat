# End-to-End Encryption & Security Architecture (Signal Protocol)

This document contains the complete cryptographic architecture, specifications, and security policies implemented in the **Self_Host_Chat / Real_DatingApp** system.

---

## 1. Security Philosophy: Zero-Knowledge Architecture

The messaging system is designed following the **Signal Protocol** principles. 
* All messages are cryptographically sealed, authenticated, and scrambled directly on the sender's device using the browser's native **Web Crypto API**.
* The backend server acts exclusively as a **blind transport and storage relay**. It holds only encrypted ciphertext blobs, initialization vectors (IVs), and HMAC signatures.
* The server possesses **no private keys**, **no pre-shared secret keys**, and **no cryptographic ability** to decrypt or inspect the content of messages.

```
+-------------+              +-----------------------+              +---------------+
| Sender Device|              |     Backend Server    |              |Receiver Device|
|  (Client A) |              |  (Blind Storage Relay)|              |  (Client B)   |
+-------------+              +-----------------------+              +---------------+
       |                                 |                                  |
       |-- 1. X3DH Key Agreement --------|--------------------------------->| (Secret Shared)
       |-- 2. Double Ratchet Derivation -|--------------------------------->| (Per-Message Keys)
       |-- 3. AES-256 Encrypt + HMAC --->|                                  |
       |                                 |-- 4. Route Locked Ciphertext --->|
       |                                 |                                  |-- 5. Verify HMAC
       |                                 |                                  |-- 6. Decrypt AES-256
```

---

## 2. Cryptographic Algorithms & Primitives

| Purpose | Algorithm | Key Size / Spec | Description |
| :--- | :--- | :--- | :--- |
| **Identity & Key Exchange** | **ECDH** (X3DH) | NIST P-256 (secp256r1) | Elliptic Curve Diffie-Hellman key agreement for deriving initial shared master secrets without over-the-network exposure. |
| **Key Derivation Function** | **HKDF-SHA256** | 256-bit | Derives independent ephemeral keys for every conversation and message sequence (Double Ratchet). |
| **Payload Encryption** | **AES-256-CBC** | 256-bit symmetric | Scrambles message plaintext using 256-bit AES encryption with a unique, cryptographically random 16-byte IV per message. |
| **Data Integrity & Authentication** | **HMAC-SHA256** | 256-bit digest | Cryptographic seal over `(sender + recipient + sequence + ciphertext + iv)`. Guarantees tamper-proofing. |
| **Network Transport** | **HTTPS / TLS 1.3** | Standard TLS | Encrypted tunneling between client apps and the server API. |

---

## 3. The 6-Step Cryptographic Workflow

### Phase 1: Key Setup & Double Ratchet (Steps 1–3)

1. **Step 1 (Key Generation):**
   - Each registered user generates an ECDH P-256 **Identity Key Pair** upon registration/login.
   - The **Public Key** (`publicKeyJwk`) is published to the directory endpoint `POST /api/connections/keys`.
   - The **Private Key** is AES-GCM-256 encrypted (`sealedKey`) and stored strictly in per-tab `sessionStorage` (purged on tab close/logout), never in plaintext `localStorage`.

2. **Step 2 (X3DH Key Agreement):**
   - When User A wants to talk to User B, User A fetches User B's public key from `GET /api/connections/keys/{username}`.
   - Using ECDH P-256, User A and User B compute an identical **Shared Secret (Master Key)** locally without ever transmitting it over the wire.

3. **Step 3 (Double Ratchet):**
   - The master key is fed into **HKDF-SHA256** along with the message sequence number to derive brand new, single-use ephemeral keys for every message.
   - **Perfect Forward Secrecy (PFS):** Even if an attacker compromises a key for message $N$, they cannot decrypt message $N-1$ or message $N+1$.

---

### Phase 2: Message Protection (Steps 4–5)

4. **Step 4 (AES-256-CBC Encryption):**
   - The message text is scrambled with AES-256-CBC using the message's ratchet key and a cryptographically random 16-byte Initialization Vector (`crypto.getRandomValues(new Uint8Array(16))`).
   - The ciphertext is encoded in base64.

5. **Step 5 (HMAC-SHA256 Authentication Seal):**
   - An HMAC-SHA256 signature is calculated over the concatenation of `sender`, `recipient`, `sequence`, `ciphertext`, and `iv`.
   - The recipient verifies this seal before attempting decryption. If even a single bit was modified in transit, verification fails and the message is rejected with status `'tampered'`.

---

### Phase 3: Zero-Knowledge Relay (Step 6)

6. **Step 6 (Transport & Storage):**
   - The payload containing `{ sender, recipient, ciphertext, iv, mac, ephemeralPublicKey, sequence }` is submitted to `POST /api/connections/messages`.
   - The database persists only these encrypted blobs. Plaintext message content never reaches the database or server logs.

---

## 4. Delivery & Read Receipts Flow

* **Sent (Single Gray Checkmark):** Message accepted by backend database (`IsDelivered = false`, `IsRead = false`).
* **Delivered (Double Gray Checkmarks):** Recipient client has received the message payload (`IsDelivered = true`, `IsRead = false`).
* **Read (Double Blue Checkmarks):** Recipient has actively opened the chat view (`IsRead = true`, `ReadAt = <timestamp>`).

---

## 5. Source Code References

* **Cryptographic Service:** [`Client/src/core/services/crypto-service.ts`](Client/src/core/services/crypto-service.ts)
* **Backend Message & Key Relay:** [`API/Controllers/ConnectionsController.cs`](API/Controllers/ConnectionsController.cs)
* **Public Key Entity:** [`API/Entities/UserPublicKey.cs`](API/Entities/UserPublicKey.cs)
* **Message Entity:** [`API/Entities/ChatMessageEntity.cs`](API/Entities/ChatMessageEntity.cs)
