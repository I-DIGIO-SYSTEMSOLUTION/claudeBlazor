-- ログインに成功したら、失敗回数とロックを解除する
UPDATE users
SET failed_login_count = 0,
    last_failed_login_at = NULL,
    locked_until = NULL,
    last_login_at = @now,
    updated_at = @now
WHERE id = @id
