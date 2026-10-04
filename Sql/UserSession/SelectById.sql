SELECT id, user_id, auth_method, mfa_method, logged_in_at, last_activity_at, processing_heartbeat_at,
       ended_at, end_reason, ip_address, user_agent
FROM user_sessions
WHERE id = @id
