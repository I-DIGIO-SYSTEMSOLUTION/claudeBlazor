-- トークンを発行する (token_hash はトークンの SHA-256。平文は保存しない)
INSERT INTO user_tokens (user_id, purpose, token_hash, expires_at, ip_address, created_at)
VALUES (@userId, @purpose, @tokenHash, @expiresAt, @ipAddress, @now)
