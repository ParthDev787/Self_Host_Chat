export interface User {
    username: string;
    token: string;
}

export interface RegisterCreds {
    username: string;
    password: string;
}

export interface LoginCreds {
    username: string;
    password: string;
}

export interface ChatMessage {
    id: string;
    senderUsername: string;
    recipientUsername?: string;
    text: string;
    timestamp: string;
    isMine: boolean;
    isDelivered?: boolean;
    isRead?: boolean;
    readAt?: string;
    ciphertext?: string;
    iv?: string;
    mac?: string;
    ephemeralPublicKey?: string;
    sequence?: number;
    isEncrypted?: boolean;
    isDecrypted?: boolean;
    verificationStatus?: 'verified' | 'unencrypted' | 'tampered';
}

export interface Contact {
    id: string;
    username: string;
    avatarLetter: string;
    avatarColor: string;
    status: 'online' | 'offline' | 'away';
    lastMessage: string;
    lastMessageTime: string;
    unreadCount: number;
    lastMessageIsMine?: boolean;
    lastMessageIsRead?: boolean;
    lastMessageIsDelivered?: boolean;
    messages: ChatMessage[];
}

export interface ConnectionRequest {
    id: string;
    fromUsername: string;
    toUsername: string;
    message?: string;
    avatarLetter: string;
    avatarColor: string;
    timestamp: string;
    displayTime: string;
    status: 'pending' | 'accepted' | 'rejected';
    isOutgoingRejection?: boolean;
    displayName?: string;
}
