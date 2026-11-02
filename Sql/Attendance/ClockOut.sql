-- 退勤 (その日の行があり、退勤時刻が空のときだけ)
-- 更新できた行数 (0 または 1) が返り、0 は出勤前か退勤済み
UPDATE attendances
SET clock_out_at = @now,
    updated_at = @now
WHERE user_id = @userId
  AND work_date = @workDate
  AND clock_out_at IS NULL
