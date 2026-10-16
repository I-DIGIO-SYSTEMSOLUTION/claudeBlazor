-- 出勤 (その日の行が無いときだけ作る)。登録できた行数 (0 または 1) が返り、0 は出勤済み
INSERT INTO attendances (user_id, work_date, clock_in_at, comment, created_at, updated_at)
VALUES (@userId, @workDate, @now, @comment, @now, @now)
ON CONFLICT (user_id, work_date) DO NOTHING
