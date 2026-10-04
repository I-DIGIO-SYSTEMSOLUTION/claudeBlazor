-- 未使用・未失効で有効期限内のトークンを取得する
SELECT id, user_id, purpose, token_hash, expires_at, used_at, revoked_at, attempt_count, ip_address, created_at
FROM user_tokens
WHERE token_hash = @tokenHash
  AND purpose = @purpose
  AND used_at IS NULL
  AND revoked_at IS NULL
  AND expires_at > @now
