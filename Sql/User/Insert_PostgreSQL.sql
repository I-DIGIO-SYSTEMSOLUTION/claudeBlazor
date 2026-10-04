-- ユーザーを登録し、採番された id を返す
-- password_history は JSON の文字列で渡すので jsonb に変換する
INSERT INTO users (login_id, password_hash, password_history, display_name, email,
                   password_changed_at, created_at, updated_at)
VALUES (@loginId, @passwordHash, CAST(@passwordHistory AS jsonb), @displayName, @email,
        @now, @now, @now)
RETURNING id AS "Value"
