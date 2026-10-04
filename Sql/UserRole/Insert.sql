-- 外部キーは無いので、user_id と role_id が存在することは呼び出し側で確認する
INSERT INTO user_roles (user_id, role_id, created_at)
VALUES (@userId, @roleId, @now)
