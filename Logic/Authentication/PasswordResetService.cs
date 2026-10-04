using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LogicCommon
{
    // メールによるパスワードリセット
    //  - リンクの有効期限は security_policies.password_reset_token_minutes。リンクは 1 回だけ使える
    //  - 新しく申請したら、それまでのリンクは無効にする。申請回数は制限しない
    //  - 未登録のメールアドレスでも、画面には同じ表示をする (メールは送らない)
    //  - リセット後は、ロックの解除・失敗回数 0・security_stamp の更新・全セッションの終了を行う
    //  - ロック中のアカウントや、パスワード未設定 (SSO のみ) のユーザーもリセットできる
    public sealed class PasswordResetService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(PasswordResetService));

        private readonly AuthRepository repository;
        private readonly UserAccountService accountService;
        private readonly MailService mailService;

        public PasswordResetService(AuthRepository repository, UserAccountService accountService, MailService mailService)
        {
            this.repository = repository;
            this.accountService = accountService;
            this.mailService = mailService;
        }

        // リセットを申請する。登録の有無で応答時間が変わらないよう、ユーザーの検索とメールの送信は別スレッドで行い、すぐに戻る
        public void Request(string email, string? ipAddress)
        {
            email = email.Trim();
            _ = Task.Run(async () =>
            {
                try
                {
                    await SendResetMailAsync(email, ipAddress);
                }
                catch (Exception ex)
                {
                    logger.Error("パスワードリセットのメールを送信できませんでした。", ex);
                }
            });
        }

        private async Task SendResetMailAsync(string email, string? ipAddress)
        {
            SecurityPolicy policy = await repository.GetSecurityPolicyAsync();
            await using DbContextAuth db = await repository.CreateDbContextAsync();

            User? user = await repository.GetUserByEmailAsync(db, email);
            if (user == null || !user.IsActive)
            {
                logger.Info("パスワードリセットの申請がありましたが、有効なユーザーに該当しないためメールは送りません。");
                return;
            }

            MailSetting? setting = await repository.GetMailSettingAsync(db);
            if (setting == null)
            {
                logger.Error("mail_settings が登録されていないため、パスワードリセットのメールを送れません。");
                return;
            }

            string token = await IssueTokenAsync(db, user.Id, policy, ipAddress);
            string link = $"{setting.PublicBaseUrl.TrimEnd('/')}/{AuthConstants.ResetPasswordPath}?token={Uri.EscapeDataString(token)}";
            string body =
                $"""
                {user.DisplayName} 様

                パスワード再設定の申請を受け付けました。
                次のリンクを開き、新しいパスワードを設定してください。

                {link}

                このリンクの有効期限は {policy.PasswordResetTokenMinutes} 分です。リンクは 1 回だけ使えます。
                心当たりがない場合は、このメールを破棄してください。パスワードは変更されません。
                """;

            await mailService.SendAsync(setting, user.Email, "パスワード再設定のご案内", body);
            logger.Info($"パスワードリセットのメールを送信しました (ユーザーID {user.Id})。");
        }

        // リセット用のトークンを発行する (それまでの未使用のトークンは無効にする)
        // パスワードの有効期限が切れたときのログイン (LoginService) からも使う
        public async Task<string> IssueTokenAsync(DbContextAuth db, long userId, SecurityPolicy policy, string? ipAddress)
        {
            string token = TokenGenerator.Create();
            DateTime now = DateTime.UtcNow;
            await repository.RevokeTokensAsync(db, userId, UserTokenPurposes.PasswordReset, now);
            await repository.InsertTokenAsync(db, userId, UserTokenPurposes.PasswordReset, TokenGenerator.Hash(token),
                now.AddMinutes(policy.PasswordResetTokenMinutes), ipAddress, now);
            return token;
        }

        // リンクがまだ使えるか (再設定画面を開いたときの確認)
        public async Task<bool> IsValidTokenAsync(string? token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }
            await using DbContextAuth db = await repository.CreateDbContextAsync();
            return await repository.GetValidTokenAsync(db, TokenGenerator.Hash(token), UserTokenPurposes.PasswordReset, DateTime.UtcNow) != null;
        }

        // 新しいパスワードを設定する。パスワードがポリシーを満たさない場合はリンクを使用済みにしない (直して再度送れる)
        // expired = true はパスワードの有効期限切れによる変更 (履歴の reason が EXPIRED になる)
        public async Task<AccountResult> ResetAsync(string? token, string newPassword, string newPasswordConfirm, bool expired)
        {
            const string InvalidLink = "このリンクは無効か、有効期限が切れています。もう一度パスワードの再設定を申請してください。";
            if (string.IsNullOrEmpty(token))
            {
                return AccountResult.Fail(InvalidLink);
            }
            if (newPassword != newPasswordConfirm)
            {
                return AccountResult.Fail("新しいパスワードと確認用のパスワードが一致しません。");
            }

            DateTime now = DateTime.UtcNow;
            await using DbContextAuth db = await repository.CreateDbContextAsync();
            await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync();

            UserToken? userToken = await repository.GetValidTokenAsync(db, TokenGenerator.Hash(token), UserTokenPurposes.PasswordReset, now);
            User? user = userToken == null ? null : await repository.GetUserByIdAsync(db, userToken.UserId);
            if (userToken == null || user == null || !user.IsActive)
            {
                return AccountResult.Fail(InvalidLink);
            }

            (List<string> errors, List<PasswordHistoryEntry> history) = await accountService.ValidateNewPasswordAsync(db, user, newPassword);
            if (errors.Count > 0)
            {
                return AccountResult.Fail(errors);
            }

            // 同時に同じリンクが使われても、使用済みにできた方だけが進む
            if (!await repository.MarkTokenUsedAsync(db, userToken.Id, now))
            {
                return AccountResult.Fail(InvalidLink);
            }

            string reason = expired ? PasswordChangeReasons.Expired : PasswordChangeReasons.PasswordReset;
            AccountResult result = await accountService.ApplyNewPasswordAsync(db, user, newPassword, history, reason, user.Id, null, now);
            if (result.Succeeded)
            {
                await tx.CommitAsync();
            }
            return result;
        }
    }
}
