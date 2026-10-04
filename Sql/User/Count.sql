-- 登録済みのユーザー数 (0 人のときだけ最初の管理者を登録できる)
SELECT CAST(COUNT(*) AS INT) AS "Value"
FROM users
