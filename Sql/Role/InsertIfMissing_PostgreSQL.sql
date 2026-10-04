-- ロールが無ければ登録する (最初の管理者の登録で ADMIN ロールを用意するときに使う)
INSERT INTO roles (code, name, created_at, updated_at)
VALUES (@code, @name, @now, @now)
ON CONFLICT (code) DO NOTHING
