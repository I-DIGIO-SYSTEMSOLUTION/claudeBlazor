SELECT CAST(COUNT(*) AS INT) AS "Value"
FROM users
WHERE lower(email) = lower(@email)
