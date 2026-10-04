-- ユーザーを登録し、採番された id を返す
-- password_history は JSON の文字列のまま nvarchar(max) の列に入れる
INSERT INTO users (login_id, password_hash, password_history, display_name, email,
                   password_changed_at, created_at, updated_at)
OUTPUT INSERTED.id AS "Value"
VALUES (@loginId, @passwordHash, @passwordHistory, @displayName, @email,
        @now, @now, @now)
