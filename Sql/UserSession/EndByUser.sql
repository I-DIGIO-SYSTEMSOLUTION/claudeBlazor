-- ユーザーのセッションをまとめて終了する (パスワード変更時など)
-- exceptId を指定すると、そのセッション (操作中の本人のセッション) は残す
UPDATE user_sessions
SET ended_at = @now,
    end_reason = @reason
WHERE user_id = @userId
  AND ended_at IS NULL
--#if exceptId
  AND id <> @exceptId
--#endif
