-- 出勤 (その日の行が無いときだけ作る)。登録できた行数 (0 または 1) が返り、0 は出勤済み
-- 同時に実行されても重複しないよう、存在の確認でキーの範囲をロックする (UPDLOCK, HOLDLOCK)
INSERT INTO attendances (user_id, work_date, clock_in_at, comment, created_at, updated_at)
SELECT @userId, @workDate, @now, @comment, @now, @now
WHERE NOT EXISTS (SELECT 1 FROM attendances WITH (UPDLOCK, HOLDLOCK) WHERE user_id = @userId AND work_date = @workDate)
