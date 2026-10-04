-- 操作があったことを記録する (どのタブの操作でも同じ行を更新する)
UPDATE user_sessions
SET last_activity_at = @now
WHERE id = @id
  AND ended_at IS NULL
  AND last_activity_at < @now
