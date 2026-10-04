-- メール設定 (id = 1 の 1 行のみ。未登録の場合は 0 行)
SELECT id, smtp_host, smtp_port, smtp_security, smtp_user, smtp_password_encrypted,
       from_address, from_name, public_base_url, created_at, updated_at
FROM mail_settings
WHERE id = 1
