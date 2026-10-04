-- パスワードを間違えたときに失敗回数を 1 増やし、規定回数に達したらロックする。ロックしたかどうかを返す
--   最後の失敗から failed_count_reset_minutes 分たっていれば、失敗回数を数え直す (resetEnabled / resetSince)
--   ロックしたら失敗回数は 0 に戻す (ロックが解けた後は、また規定回数まで試せる)
-- FOR UPDATE で行をロックしてから数えるので、同時にログインを試みられても失敗回数を数え落とさない
WITH target AS (
    SELECT id,
           CASE WHEN @resetEnabled AND (last_failed_login_at IS NULL OR last_failed_login_at < @resetSince)
                THEN 1
                ELSE failed_login_count + 1
           END AS new_count
    FROM users
    WHERE id = @id
    FOR UPDATE
)
UPDATE users AS u
SET failed_login_count   = CASE WHEN @lockoutEnabled AND t.new_count >= @maxFailedAttempts THEN 0 ELSE t.new_count END,
    locked_until         = CASE WHEN @lockoutEnabled AND t.new_count >= @maxFailedAttempts THEN @lockedUntil ELSE u.locked_until END,
    last_failed_login_at = @now,
    updated_at           = @now
FROM target AS t
WHERE u.id = t.id
RETURNING (@lockoutEnabled AND t.new_count >= @maxFailedAttempts) AS "Value"
