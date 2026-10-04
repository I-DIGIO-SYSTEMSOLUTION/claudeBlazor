-- ログアウトせずにブラウザを閉じたなどで残っているセッションを、アイドルタイムアウトとして終了する
-- threshold = 現在日時 - idle_timeout_minutes。最後の操作とサーバー処理の終了の両方がそれより前のものが対象
UPDATE user_sessions
SET ended_at = @now,
    end_reason = @reason
WHERE ended_at IS NULL
  AND last_activity_at < @threshold
  AND (processing_heartbeat_at IS NULL OR processing_heartbeat_at < @threshold)
