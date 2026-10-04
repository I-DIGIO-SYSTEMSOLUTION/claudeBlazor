-- パスワードを変更する (変更・リセット・期限切れの共通)
--   password_history には新しいパスワードを先頭に追加した JSON を渡す
--   security_stamp を変え、ロックと失敗回数も解除する
-- 履歴を読んでから書き戻すまでに他の変更が入っていないよう、読み込んだ時点の updated_at と同じ場合だけ更新する
-- 更新できた行数 (0 または 1) が返る
UPDATE users
SET password_hash = @passwordHash,
    password_history = CAST(@passwordHistory AS jsonb),
    password_changed_at = @now,
    security_stamp = @securityStamp,
    failed_login_count = 0,
    last_failed_login_at = NULL,
    locked_until = NULL,
    updated_at = @now
WHERE id = @id
  AND updated_at = @expectedUpdatedAt
