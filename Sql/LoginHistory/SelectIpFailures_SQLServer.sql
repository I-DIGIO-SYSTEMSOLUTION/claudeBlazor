-- IP アドレス単位の試行制限: 同じ IP から since 以降に失敗した回数と、最後に失敗した日時
-- 数えるのはパスワードの誤りと存在しないログイン ID だけ (IP_BLOCKED を数えるとブロックが延び続けるため)
-- succeeded = 0 はフィルター付きインデックス ix_login_histories_ip_failed の条件と同じ
SELECT CAST(COUNT(*) AS INT) AS "Count",
       MAX(attempted_at) AS "LastAttemptedAt"
FROM login_histories
WHERE ip_address = @ipAddress
  AND succeeded = 0
  AND failure_reason IN (@reasons)
  AND attempted_at >= @since
