-- 出勤 (その日の行を作る)。登録・更新できた行数が返る
INSERT INTO attendances (user_id, work_date, clock_in_at, created_at, updated_at)
VALUES (@userId, @workDate, @now, @now, @now)
ON CONFLICT (user_id, work_date) DO UPDATE
SET clock_in_at = EXCLUDED.clock_in_at,
    updated_at = EXCLUDED.updated_at
