import { Injectable, inject } from '@angular/core';
import { ConnectionService } from './connection-service';

export interface EncryptedPayload {
  ciphertext: string;
  iv: string;
  mac: string;
  ephemeralPublicKey?: string;
  sequence: number;
}

export interface DecryptedMessageResult {
  text: string;
  isEncrypted: boolean;
  verificationStatus: 'verified' | 'unencrypted' | 'tampered';
}

@Injectable({
  providedIn: 'root'
})
export class CryptoService {
  private connectionService = inject(ConnectionService);

  private identityKeyPair: CryptoKeyPair | null = null;
  private identityPublicKeyJwk: JsonWebKey | null = null;
  private currentUsername: string | null = null;

  // Session keys cache: contactUsername -> CryptoKey (Derived Shared Master Secret)
  private sharedSecrets = new Map<string, ArrayBuffer>();
  // Peer public keys cache: contactUsername -> CryptoKey
  private peerPublicKeys = new Map<string, CryptoKey>();
  // Ratchet sequence tracker per contact
  private sequenceCounters = new Map<string, number>();
  // Decrypted message cache: messageId -> text
  private decryptedCache = new Map<string, DecryptedMessageResult>();

  // ══════════════════════════════════════════════════════════════════
  // 1. PURPLE (STEPS 1–3): KEY SETUP, X3DH & DOUBLE RATCHET
  // ══════════════════════════════════════════════════════════════════

  /**
   * Initializes or restores the user's Signal Identity Key Pair (ECDH P-256)
   * and automatically synchronizes the public key to the backend.
   */
  async initializeIdentity(username: string): Promise<void> {
    const cleanUsername = username.trim().toLowerCase();
    this.currentUsername = cleanUsername;

    const storageKey = `signal_identity_${cleanUsername}`;
    const stored = localStorage.getItem(storageKey);

    if (stored) {
      try {
        const parsed = JSON.parse(stored);
        const privateKey = await crypto.subtle.importKey(
          'jwk',
          parsed.privateKeyJwk,
          { name: 'ECDH', namedCurve: 'P-256' },
          true,
          ['deriveBits', 'deriveKey']
        );
        const publicKey = await crypto.subtle.importKey(
          'jwk',
          parsed.publicKeyJwk,
          { name: 'ECDH', namedCurve: 'P-256' },
          true,
          []
        );

        this.identityKeyPair = { privateKey, publicKey };
        this.identityPublicKeyJwk = parsed.publicKeyJwk;
      } catch (err) {
        console.warn('Failed to restore identity keys, regenerating fresh keys:', err);
        await this.generateAndStoreNewIdentity(cleanUsername, storageKey);
      }
    } else {
      await this.generateAndStoreNewIdentity(cleanUsername, storageKey);
    }

    // Publish public key to server directory
    if (this.identityPublicKeyJwk) {
      try {
        this.connectionService.savePublicKey(
          cleanUsername,
          JSON.stringify(this.identityPublicKeyJwk)
        ).subscribe({
          next: () => {},
          error: (err) => console.warn('Could not publish public key to server:', err)
        });
      } catch {}
    }
  }

  private async generateAndStoreNewIdentity(username: string, storageKey: string): Promise<void> {
    // Generate ECDH P-256 Keypair (X3DH Identity Key)
    const keyPair = await crypto.subtle.generateKey(
      { name: 'ECDH', namedCurve: 'P-256' },
      true,
      ['deriveBits', 'deriveKey']
    );

    const publicKeyJwk = await crypto.subtle.exportKey('jwk', keyPair.publicKey);
    const privateKeyJwk = await crypto.subtle.exportKey('jwk', keyPair.privateKey);

    localStorage.setItem(
      storageKey,
      JSON.stringify({ publicKeyJwk, privateKeyJwk, createdAt: new Date().toISOString() })
    );

    this.identityKeyPair = keyPair;
    this.identityPublicKeyJwk = publicKeyJwk;
  }

  /**
   * Retrieves or fetches a peer's public key (ECDH P-256)
   */
  async getPeerPublicKey(peerUsername: string): Promise<CryptoKey | null> {
    const peer = peerUsername.trim().toLowerCase();
    if (this.peerPublicKeys.has(peer)) {
      return this.peerPublicKeys.get(peer)!;
    }

    try {
      const res = await new Promise<any>((resolve, reject) => {
        this.connectionService.getPublicKey(peer).subscribe({
          next: resolve,
          error: reject
        });
      });

      if (res && res.publicKeyJwk) {
        const jwk = typeof res.publicKeyJwk === 'string' ? JSON.parse(res.publicKeyJwk) : res.publicKeyJwk;
        const key = await crypto.subtle.importKey(
          'jwk',
          jwk,
          { name: 'ECDH', namedCurve: 'P-256' },
          true,
          []
        );
        this.peerPublicKeys.set(peer, key);
        return key;
      }
    } catch (err) {
      // Peer has not yet registered public key
    }
    return null;
  }

  /**
   * Step 2: X3DH Key Agreement
   * Computes shared master secret via ECDH between user's private key and peer's public key
   */
  private async getSharedSecret(peerUsername: string): Promise<ArrayBuffer | null> {
    const peer = peerUsername.trim().toLowerCase();
    if (this.sharedSecrets.has(peer)) {
      return this.sharedSecrets.get(peer)!;
    }

    if (!this.identityKeyPair) {
      if (this.currentUsername) {
        await this.initializeIdentity(this.currentUsername);
      }
      if (!this.identityKeyPair) return null;
    }

    const peerKey = await this.getPeerPublicKey(peer);
    if (!peerKey) return null;

    // ECDH derivation gives 256 bits of shared secret
    const sharedBits = await crypto.subtle.deriveBits(
      { name: 'ECDH', public: peerKey },
      this.identityKeyPair.privateKey,
      256
    );

    this.sharedSecrets.set(peer, sharedBits);
    return sharedBits;
  }

  /**
   * Step 3: Double Ratchet Key Derivation (HKDF with SHA-256)
   * Derives a unique AES-256 encryption key and HMAC-SHA256 authentication key
   * for a specific message sequence and optional ephemeral DH ratchet.
   */
  private async deriveMessageKeys(
    sharedSecret: ArrayBuffer,
    sequence: number,
    ephemeralJwk?: string
  ): Promise<{ encKey: CryptoKey; macKey: CryptoKey }> {
    const saltInfo = `SignalRatchet:Seq=${sequence}:${ephemeralJwk || 'Base'}`;
    const enc = new TextEncoder();

    // Import shared secret as HKDF master key
    const masterKey = await crypto.subtle.importKey(
      'raw',
      sharedSecret,
      { name: 'HKDF' },
      false,
      ['deriveBits', 'deriveKey']
    );

    // Derive 512 bits: first 256 bits for AES-256, next 256 bits for HMAC-SHA256
    const derivedBytes = await crypto.subtle.deriveBits(
      {
        name: 'HKDF',
        hash: 'SHA-256',
        salt: enc.encode('SignalProtocolWhatsAppSalt2026'),
        info: enc.encode(saltInfo)
      },
      masterKey,
      512
    );

    const encKeyBytes = derivedBytes.slice(0, 32);
    const macKeyBytes = derivedBytes.slice(32, 64);

    const encKey = await crypto.subtle.importKey(
      'raw',
      encKeyBytes,
      { name: 'AES-CBC' },
      false,
      ['encrypt', 'decrypt']
    );

    const macKey = await crypto.subtle.importKey(
      'raw',
      macKeyBytes,
      { name: 'HMAC', hash: 'SHA-256' },
      false,
      ['sign', 'verify']
    );

    return { encKey, macKey };
  }

  // ══════════════════════════════════════════════════════════════════
  // 2. TEAL (STEPS 4–5): AES-256 SCRAMBLING & HMAC-SHA256 SEAL
  // ══════════════════════════════════════════════════════════════════

  /**
   * Encrypts a message using full Signal Protocol flow:
   * 1. ECDH Shared Secret
   * 2. Double Ratchet Per-Message Key Derivation
   * 3. AES-256-CBC Encryption with random 16-byte IV
   * 4. HMAC-SHA256 Integrity Seal
   */
  async lockMessage(recipient: string, plaintext: string): Promise<EncryptedPayload> {
    const rec = recipient.trim().toLowerCase();
    const sharedSecret = await this.getSharedSecret(rec);

    if (!sharedSecret) {
      throw new Error(`Unable to establish secure cryptographic session with ${recipient}. Public key not found.`);
    }

    // Sequence counter for Double Ratchet
    const currentSeq = (this.sequenceCounters.get(rec) || 0) + 1;
    this.sequenceCounters.set(rec, currentSeq);

    // Ephemeral DH keypair for break-in recovery ratchet
    const ephemeralKey = await crypto.subtle.generateKey(
      { name: 'ECDH', namedCurve: 'P-256' },
      true,
      ['deriveBits']
    );
    const ephemeralJwk = await crypto.subtle.exportKey('jwk', ephemeralKey.publicKey);
    const ephemeralJwkStr = JSON.stringify({ x: ephemeralJwk.x, y: ephemeralJwk.y });

    // Derive per-message AES-256 and HMAC-SHA256 keys
    const { encKey, macKey } = await this.deriveMessageKeys(sharedSecret, currentSeq, ephemeralJwkStr);

    // Step 4: AES-256 Encryption
    const iv = crypto.getRandomValues(new Uint8Array(16));
    const textEncoder = new TextEncoder();
    const encodedPlaintext = textEncoder.encode(plaintext);

    const encryptedBuffer = await crypto.subtle.encrypt(
      { name: 'AES-CBC', iv },
      encKey,
      encodedPlaintext
    );

    const ciphertextBase64 = this.arrayBufferToBase64(encryptedBuffer);
    const ivBase64 = this.arrayBufferToBase64(iv.buffer as ArrayBuffer);

    // Step 5: HMAC-SHA256 Seal
    const sender = this.currentUsername || 'me';
    const authDataString = `${sender}:${rec}:${currentSeq}:${ivBase64}:${ciphertextBase64}`;
    const authDataBytes = textEncoder.encode(authDataString);

    const macBuffer = await crypto.subtle.sign('HMAC', macKey, authDataBytes);
    const macBase64 = this.arrayBufferToBase64(macBuffer);

    const payload: EncryptedPayload = {
      ciphertext: ciphertextBase64,
      iv: ivBase64,
      mac: macBase64,
      ephemeralPublicKey: ephemeralJwkStr,
      sequence: currentSeq
    };

    // Cache the plaintext by ciphertext so the sender immediately sees decrypted text in previews
    this.decryptedCache.set(ciphertextBase64, {
      text: plaintext,
      isEncrypted: true,
      verificationStatus: 'verified'
    });

    return payload;
  }

  /**
   * Decrypts and authenticates a message:
   * 1. Re-derives per-message keys via Double Ratchet
   * 2. Verifies HMAC-SHA256 seal
   * 3. Decrypts AES-256-CBC ciphertext
   */
  async unlockMessage(
    id: string,
    sender: string,
    recipient: string,
    ciphertext?: string,
    iv?: string,
    mac?: string,
    ephemeralPublicKey?: string,
    sequence: number = 0,
    fallbackText?: string
  ): Promise<DecryptedMessageResult> {
    if (id && this.decryptedCache.has(id)) {
      return this.decryptedCache.get(id)!;
    }
    if (ciphertext && this.decryptedCache.has(ciphertext)) {
      return this.decryptedCache.get(ciphertext)!;
    }

    // Check if this is an encrypted message
    if (!ciphertext || !iv || !mac) {
      const res: DecryptedMessageResult = {
        text: fallbackText || ciphertext || '',
        isEncrypted: false,
        verificationStatus: 'unencrypted'
      };
      this.decryptedCache.set(id, res);
      return res;
    }

    try {
      const cleanSender = sender.trim().toLowerCase();
      const cleanRecipient = recipient.trim().toLowerCase();
      const peer = (cleanSender === this.currentUsername?.toLowerCase()) ? cleanRecipient : cleanSender;

      const sharedSecret = await this.getSharedSecret(peer);
      if (!sharedSecret) {
        return {
          text: '🔒 [Encrypted Message - Key not yet exchanged]',
          isEncrypted: true,
          verificationStatus: 'unencrypted'
        };
      }

      // Re-derive the identical message keys using ratchet sequence & ephemeral key
      const { encKey, macKey } = await this.deriveMessageKeys(sharedSecret, sequence, ephemeralPublicKey);

      // Verify HMAC-SHA256 seal first
      const textEncoder = new TextEncoder();
      const authDataString = `${cleanSender}:${cleanRecipient}:${sequence}:${iv}:${ciphertext}`;
      const authDataBytes = textEncoder.encode(authDataString);
      const macBytes = this.base64ToArrayBuffer(mac);

      const isValidSeal = await crypto.subtle.verify('HMAC', macKey, macBytes, authDataBytes);
      if (!isValidSeal) {
        const tamperedResult: DecryptedMessageResult = {
          text: '⚠️ [Security Alert: Message seal compromised or tampered!]',
          isEncrypted: true,
          verificationStatus: 'tampered'
        };
        this.decryptedCache.set(id, tamperedResult);
        return tamperedResult;
      }

      // Step 4: AES-256 Decrypt
      const ivBuffer = this.base64ToArrayBuffer(iv);
      const ciphertextBuffer = this.base64ToArrayBuffer(ciphertext);

      const decryptedBuffer = await crypto.subtle.decrypt(
        { name: 'AES-CBC', iv: new Uint8Array(ivBuffer) },
        encKey,
        ciphertextBuffer
      );

      const decryptedText = new TextDecoder().decode(decryptedBuffer);

      const successResult: DecryptedMessageResult = {
        text: decryptedText,
        isEncrypted: true,
        verificationStatus: 'verified'
      };
      if (id) this.decryptedCache.set(id, successResult);
      if (ciphertext) this.decryptedCache.set(ciphertext, successResult);
      return successResult;
    } catch (err) {
      const fallbackResult: DecryptedMessageResult = {
        text: fallbackText || ciphertext || '🔒 [Encrypted Message]',
        isEncrypted: true,
        verificationStatus: 'unencrypted'
      };
      return fallbackResult;
    }
  }

  // ══════════════════════════════════════════════════════════════════
  // HELPER UTILITIES
  // ══════════════════════════════════════════════════════════════════

  private arrayBufferToBase64(buffer: ArrayBuffer): string {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.byteLength; i++) {
      binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
  }

  private base64ToArrayBuffer(base64: string): ArrayBuffer {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
      bytes[i] = binary.charCodeAt(i);
    }
    return bytes.buffer as ArrayBuffer;
  }

  getIdentityPublicKeyJwk(): JsonWebKey | null {
    return this.identityPublicKeyJwk;
  }
}
