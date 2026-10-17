import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { ConnectionRequest, Contact, ChatMessage } from '../../types/user';

@Injectable({
  providedIn: 'root'
})
export class ConnectionService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl;

  sendRequest(fromUsername: string, toUsername: string, message?: string) {
    return this.http.post<ConnectionRequest>(this.baseUrl + 'connections/request', {
      fromUsername,
      toUsername,
      message
    });
  }

  getRequests(username: string) {
    return this.http.get<ConnectionRequest[]>(this.baseUrl + 'connections/requests', {
      params: { username }
    });
  }

  acceptRequest(id: string) {
    return this.http.post<{ message: string; request: ConnectionRequest }>(
      this.baseUrl + `connections/accept/${id}`,
      {}
    );
  }

  rejectRequest(id: string) {
    return this.http.post<{ message: string; request: ConnectionRequest }>(
      this.baseUrl + `connections/reject/${id}`,
      {}
    );
  }

  dismissRequest(id: string) {
    return this.http.post<{ message: string }>(
      this.baseUrl + `connections/dismiss/${id}`,
      {}
    );
  }

  getContacts(username: string) {
    return this.http.get<Contact[]>(this.baseUrl + 'connections/contacts', {
      params: { username }
    });
  }

  getMessages(user1: string, user2: string) {
    return this.http.get<ChatMessage[]>(this.baseUrl + 'connections/messages', {
      params: { user1, user2 }
    });
  }

  sendMessage(
    sender: string, 
    recipient: string, 
    text: string, 
    cryptoPayload?: { 
      ciphertext: string; 
      iv: string; 
      mac: string; 
      ephemeralPublicKey?: string; 
      sequence: number; 
    }
  ) {
    return this.http.post<ChatMessage>(this.baseUrl + 'connections/messages', {
      sender,
      recipient,
      text: cryptoPayload?.ciphertext || text,
      ciphertext: cryptoPayload?.ciphertext || text,
      iv: cryptoPayload?.iv,
      mac: cryptoPayload?.mac,
      ephemeralPublicKey: cryptoPayload?.ephemeralPublicKey,
      sequence: cryptoPayload?.sequence ?? 0
    });
  }

  savePublicKey(username: string, publicKeyJwk: string, preKeyJwk?: string) {
    return this.http.post<{ message: string; key: any }>(this.baseUrl + 'connections/keys', {
      username,
      publicKeyJwk,
      preKeyJwk
    });
  }

  getPublicKey(username: string) {
    return this.http.get<{ username: string; publicKeyJwk: string; preKeyJwk?: string }>(
      this.baseUrl + `connections/keys/${username}`
    );
  }

  markMessagesAsRead(user: string, withUser: string) {
    return this.http.post<{ success: boolean; markedCount: number }>(
      this.baseUrl + 'connections/messages/read',
      { user, withUser }
    );
  }
}
