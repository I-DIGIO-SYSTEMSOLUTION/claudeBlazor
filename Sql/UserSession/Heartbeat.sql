-- サーバー処理の実行中 (定期的に) と終了時に、生存通知・終了日時を記録する
UPDATE user_sessions
SET processing_heartbeat_at = @now
WHERE id IN (@ids)
  AND ended_at IS NULL
