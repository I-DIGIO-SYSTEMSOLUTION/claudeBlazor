-- パスワードを間違えたときに失敗回数を 1 増やし、規定回数に達したらロックする。ロックしたかどうかを返す
--   最後の失敗から failed_count_reset_minutes 分たっていれば、失敗回数を数え直す (resetEnabled / resetSince)
--   ロックしたら失敗回数は 0 に戻す (ロックが解けた後は、また規定回数まで試せる)
-- 1 つの UPDATE 文で読んで書くので (行には更新ロックがかかる)、同時にログインを試みられても失敗回数を数え落とさない
-- bit のパラメータは = 1 で比べる (SQL Server には boolean の式が無いため)
UPDATE u
SET failed_login_count   = CASE WHEN @lockoutEnabled = 1 AND c.new_count >= @maxFailedAttempts THEN 0 ELSE c.new_count END,
    locked_until         = CASE WHEN @lockoutEnabled = 1 AND c.new_count >= @maxFailedAttempts THEN @lockedUntil ELSE u.locked_until END,
    last_failed_login_at = @now,
    updated_at           = @now
-- ロックした場合だけ locked_until が今回の @lockedUntil になる
OUTPUT CAST(CASE WHEN INSERTED.locked_until = @lockedUntil THEN 1 ELSE 0 END AS bit) AS "Value"
FROM users AS u
CROSS APPLY (
    SELECT CASE WHEN @resetEnabled = 1 AND (u.last_failed_login_at IS NULL OR u.last_failed_login_at < @resetSince)
                THEN 1
                ELSE u.failed_login_count + 1
           END AS new_count
) AS c
WHERE u.id = @id
