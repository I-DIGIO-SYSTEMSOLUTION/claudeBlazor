-- セキュリティポリシー (id = 1 の 1 行のみ)
SELECT id, lockout_enabled, max_failed_attempts, lockout_minutes, failed_count_reset_minutes,
       ip_limit_enabled, ip_max_failed_attempts, ip_failed_window_minutes, ip_block_minutes,
       password_min_length, password_max_length, require_uppercase, require_lowercase, require_digit, require_symbol,
       min_char_types, disallow_login_id, password_history_count, password_expiry_days,
       password_reset_token_minutes, idle_timeout_minutes, timeout_warning_seconds,
       otp_code_length, otp_code_minutes, otp_max_attempts, created_at, updated_at
FROM security_policies
WHERE id = 1
