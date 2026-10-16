-- ログインユーザーの、指定した日 (日本時間の日付) の勤怠 (無ければ 0 行)
SELECT id, user_id, work_date, clock_in_at, clock_out_at, comment, created_at, updated_at
FROM attendances
WHERE user_id = @userId
  AND work_date = @workDate
