-- セッションを終了する (既に終了している場合は何もしない)
UPDATE user_sessions
SET ended_at = @now,
    end_reason = @reason
WHERE id = @id
  AND ended_at IS NULL
