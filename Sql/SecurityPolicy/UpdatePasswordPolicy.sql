-- パスワードポリシーと自動ログアウトの時間を更新する (管理者のパスワードポリシー画面)
-- 画面を開いた後に他の人が変更していないよう、読み込んだ時点の updated_at と同じ場合だけ更新する
-- 更新できた行数 (0 または 1) が返る
UPDATE security_policies
SET password_min_length = @minLength,
    password_max_length = @maxLength,
    require_uppercase = @requireUppercase,
    require_lowercase = @requireLowercase,
    require_digit = @requireDigit,
    require_symbol = @requireSymbol,
    min_char_types = @minCharTypes,
    disallow_login_id = @disallowLoginId,
    password_history_count = @historyCount,
    password_expiry_days = @expiryDays,
    password_expiry_warning_days = @expiryWarningDays,
    idle_timeout_minutes = @idleTimeoutMinutes,
    timeout_warning_seconds = @timeoutWarningSeconds,
    updated_at = @now
WHERE id = 1
  AND updated_at = @expectedUpdatedAt
