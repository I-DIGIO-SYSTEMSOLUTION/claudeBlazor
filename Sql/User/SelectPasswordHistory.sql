-- パスワード設定履歴 (JSON の配列。新しい順で、先頭が現在のパスワード)
SELECT password_history AS "Value"
FROM users
WHERE id = @id
