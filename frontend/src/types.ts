export type Role = "client" | "admin";

export type TelegramUser = {
  id: number;
  username?: string;
  first_name?: string;
  last_name?: string;
  photo_url?: string;
};

export type AppUser = TelegramUser & {
  role: Role;
  balance: number;
};

export type AuthResponse = {
  token: string;
  expiresAt: string;
  user: TelegramUser;
  csrfToken?: string;
};

export type ApiConfig = {
  tokenTtlSeconds?: number;
};

export type CodeHistory = {
  id: string;
  value: string;
  points: number;
  createdAt: string;
  expiresAt: string;
  used: boolean;
  usedBy?: number;
  usedAt?: string;
};

export type LeaderboardItem = {
  telegramId: number;
  username?: string;
  firstName?: string;
  lastName?: string;
  photoUrl?: string;
  balance: number;
};
