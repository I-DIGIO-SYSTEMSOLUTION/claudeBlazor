-- ロール一覧 (ユーザー作成画面の選択肢)
SELECT id, code, name, created_at, updated_at
FROM roles
ORDER BY code
