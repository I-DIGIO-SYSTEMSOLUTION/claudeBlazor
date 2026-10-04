-- メールアドレスでユーザーを取得する (大文字小文字を区別しない。一意インデックス uq_users_email と同じ式)
SELECT id, login_id, password_hash, display_name, email, phone_number, is_active,
       failed_login_count, last_failed_login_at, locked_until, last_login_at, password_changed_at,
       mfa_enabled, mfa_method, security_stamp, created_at, updated_at
FROM users
WHERE lower(email) = lower(@email)
