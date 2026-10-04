-- ユーザーに付与されているロールのコード (ログイン時に Cookie のロールに入れる)
SELECT r.code AS "Value"
FROM user_roles AS ur
INNER JOIN roles AS r ON r.id = ur.role_id
WHERE ur.user_id = @userId
ORDER BY r.code
