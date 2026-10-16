-- 退勤 (その日の行があり、退勤時刻が空のときだけ)。コメントは退勤時の内容で上書きする
-- 更新できた行数 (0 または 1) が返り、0 は出勤前か退勤済み
UPDATE attendances
SET clock_out_at = @now,
    comment = @comment,
    updated_at = @now
WHERE user_id = @userId
  AND work_date = @workDate
  AND clock_out_at IS NULL
