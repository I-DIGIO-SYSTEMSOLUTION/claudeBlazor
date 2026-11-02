-- 出勤 (その日の行を作る)。登録・更新できた行数が返る
-- 同時に実行されても重複しないよう、キーの範囲をロックする (UPDLOCK, HOLDLOCK)
UPDATE attendances WITH (UPDLOCK, HOLDLOCK)
SET clock_in_at = @now,
    updated_at = @now
WHERE user_id = @userId
  AND work_date = @workDate;

IF @@ROWCOUNT = 0
    INSERT INTO attendances (user_id, work_date, clock_in_at, created_at, updated_at)
    VALUES (@userId, @workDate, @now, @now, @now);
