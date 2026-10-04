-- ログイン ID でユーザーを取得する (大文字小文字を区別する)
-- パスワード履歴 (password_history) は取得しない。必要なときは SelectPasswordHistory を使う
SELECT id, login_id, password_hash, display_name, email, phone_number, is_active,
       failed_login_count, last_failed_login_at, locked_until, last_login_at, password_changed_at,
       mfa_enabled, mfa_method, security_stamp, created_at, updated_at
FROM users
WHERE login_id = @loginId
