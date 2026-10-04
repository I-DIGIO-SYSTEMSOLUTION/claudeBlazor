-- ユーザーの未使用のトークンを無効にする (新しいリセットリンクを発行したら、前のリンクは使えなくする)
UPDATE user_tokens
SET revoked_at = @now
WHERE user_id = @userId
  AND purpose = @purpose
  AND used_at IS NULL
  AND revoked_at IS NULL
