SELECT CAST(COUNT(*) AS INT) AS "Value"
FROM users
WHERE login_id = @loginId
