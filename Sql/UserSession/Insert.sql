-- ログインしたらセッションを 1 行作る (id は Cookie に入れる)
INSERT INTO user_sessions (id, user_id, auth_method, mfa_method, logged_in_at, last_activity_at, ip_address, user_agent)
VALUES (@id, @userId, @authMethod, @mfaMethod, @now, @now, @ipAddress, @userAgent)
