-- ロールが無ければ登録する (最初の管理者の登録で ADMIN ロールを用意するときに使う)
-- 同時に実行されても重複しないよう、存在の確認でキーの範囲をロックする (UPDLOCK, HOLDLOCK)
INSERT INTO roles (code, name, created_at, updated_at)
SELECT @code, @name, @now, @now
WHERE NOT EXISTS (SELECT 1 FROM roles WITH (UPDLOCK, HOLDLOCK) WHERE code = @code)
