-- トークンを使用済みにする。更新できた行数が 1 のときだけ使ってよい (同時に使われても 1 回しか通らない)
UPDATE user_tokens
SET used_at = @now
WHERE id = @id
  AND used_at IS NULL
  AND revoked_at IS NULL
  AND expires_at > @now
