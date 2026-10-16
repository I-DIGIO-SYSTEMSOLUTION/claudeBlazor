-- ログインユーザーの、指定した期間 (日本時間の日付。月の初日〜末日) の勤怠を日付順に取得する
SELECT id, user_id, work_date, clock_in_at, clock_out_at, comment, created_at, updated_at
FROM attendances
WHERE user_id = @userId
  AND work_date BETWEEN @firstDate AND @lastDate
ORDER BY work_date
