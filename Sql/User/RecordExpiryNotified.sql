-- 有効期限切れの警告ダイアログを表示した日時を記録する (警告は 1 日 1 回だけ表示する)
-- ユーザー情報の変更ではないので updated_at は変えない (パスワード変更時の updated_at による重複チェックに影響させない)
UPDATE users
SET password_expiry_notified_at = @now
WHERE id = @id
