-- ログインの試行を記録する (成功・失敗とも)。user_id は特定できない場合 NULL
INSERT INTO login_histories (user_id, login_id, auth_method, succeeded, failure_reason, ip_address, user_agent, attempted_at)
VALUES (@userId, @loginId, @authMethod, @succeeded, @failureReason, @ipAddress, @userAgent, @now)
