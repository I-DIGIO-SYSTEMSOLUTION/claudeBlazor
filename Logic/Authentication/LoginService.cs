using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace LogicCommon
{
    public enum LoginStatus
    {
        // 本人確認に成功した。Ticket を付けてサインイン用のエンドポイントへ遷移する
        Succeeded,
        // ログインできない (理由は画面に出さず、login_histories に記録する)
        Failed,
        // パスワードの有効期限切れ。ResetToken を付けてパスワードの再設定画面へ遷移する
        PasswordExpired,
        // 追加の認証 (ワンタイムパスワード) が必要 (未実装)
        MfaRequired,
        // 本人確認に成功したが、パスワードの有効期限が近い。画面で変更するかを確認し、
        // CompleteSignInAsync でチケットを発行してからサインイン用のエンドポイントへ遷移する
        PasswordExpiringSoon,
    }

    // PasswordExpiresAt は UTC。PasswordExpiringSoon のときだけ PendingSignIn が入る
    public sealed record LoginResult(LoginStatus Status, string? Ticket = null, string? ResetToken = null,
        DateTime? PasswordExpiresAt = null, int? DaysUntilExpiry = null, PendingSignIn? PendingSignIn = null);

    // 本人確認が済み、サインイン用チケットの発行を待っている状態 (サーバーのメモリ上だけに持つ。1 回だけ使える)
    // 確認ダイアログを開いている間にチケットの期限 (1 分) が切れないよう、ユーザーが選んだ時点でチケットを発行する
    public sealed class PendingSignIn
    {
        internal PendingSignIn(long userId, string authMethod, string? mfaMethod)
        {
            UserId = userId;
            AuthMethod = authMethod;
            MfaMethod = mfaMethod;
        }

        internal long UserId { get; }
        internal string AuthMethod { get; }
        internal string? MfaMethod { get; }
        internal bool Used { get; set; }
    }

    // サインイン用チケットを使ったときに分かる内容
    public sealed record SignInTicketInfo(User User, string AuthMethod, string? MfaMethod);

    // ログインの処理。「本人確認 (パスワード / 将来は OIDC) → 追加の認証 (将来) → セッションの発行」の段階に分けている。
    // ログイン Cookie は Interactive Server の画面 (SignalR の通信) からは発行できないので、
    // 本人確認が済んだら 1 回限りのサインイン用チケットを発行し、画面はそのチケットを付けて
    // サインイン用のエンドポイント (AuthEndpoints の account/signin) へ forceLoad で遷移する
    public sealed class LoginService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(LoginService));

        // サインイン用チケットの有効期限 (画面からエンドポイントへ遷移するまでの時間なので短くする)
        private static readonly TimeSpan SignInTicketLifetime = TimeSpan.FromMinutes(1);

        private readonly AuthRepository repository;
        private readonly PasswordPolicy passwordPolicy;
        private readonly PasswordResetService passwordResetService;
        // チケットの中身 (本人確認の方式など) を改ざんできないようにする
        private readonly IDataProtector ticketProtector;

        public LoginService(AuthRepository repository, PasswordPolicy passwordPolicy, PasswordResetService passwordResetService,
            IDataProtectionProvider dataProtectionProvider)
        {
            this.repository = repository;
            this.passwordPolicy = passwordPolicy;
            this.passwordResetService = passwordResetService;
            ticketProtector = dataProtectionProvider.CreateProtector("MyBlazorApp.SignInTicket");
        }

        // ID・パスワードによる本人確認
        public async Task<LoginResult> PasswordSignInAsync(string loginId, string password, ClientConnectionInfo client)
        {
            loginId = loginId.Trim();
            SecurityPolicy policy = await repository.GetSecurityPolicyAsync();
            DateTime now = DateTime.UtcNow;
            await using DbContextAuth db = await repository.CreateDbContextAsync();

            async Task<LoginResult> FailAsync(long? userId, string reason)
            {
                await repository.InsertLoginHistoryAsync(db, userId, loginId, AuthMethods.Password, false, reason,
                    client.IpAddress, client.UserAgent, now);
                return new LoginResult(LoginStatus.Failed);
            }

            // 1. IP アドレス単位の試行制限
            if (await IsIpBlockedAsync(db, policy, client.IpAddress, now))
            {
                passwordPolicy.VerifyDummy(password);
                return await FailAsync(null, LoginFailureReasons.IpBlocked);
            }

            // 2. ユーザーの確認 (存在しない・無効・ロック中は、パスワードを照合せずに失敗にする)
            User? user = await repository.GetUserByLoginIdAsync(db, loginId);
            if (user == null)
            {
                passwordPolicy.VerifyDummy(password);
                return await FailAsync(null, LoginFailureReasons.NotFound);
            }
            if (!user.IsActive)
            {
                passwordPolicy.VerifyDummy(password);
                return await FailAsync(user.Id, LoginFailureReasons.Inactive);
            }
            if (user.LockedUntil > now)
            {
                passwordPolicy.VerifyDummy(password);
                return await FailAsync(user.Id, LoginFailureReasons.Locked);
            }

            // 3. パスワードの照合 (パスワード未設定の SSO ユーザーも誤りとして扱う)
            if (!passwordPolicy.Verify(user.PasswordHash, password))
            {
                bool locked = await repository.RecordLoginFailureAsync(db, user.Id, policy, now);
                if (locked)
                {
                    logger.Warn($"パスワードを規定回数間違えたため、アカウントをロックしました (ユーザーID {user.Id}, {policy.LockoutMinutes} 分)。");
                }
                return await FailAsync(user.Id, LoginFailureReasons.InvalidPassword);
            }

            await repository.RecordLoginSuccessAsync(db, user.Id, now);
            await repository.InsertLoginHistoryAsync(db, user.Id, loginId, AuthMethods.Password, true, null,
                client.IpAddress, client.UserAgent, now);

            // 4. パスワードの有効期限
            if (policy.PasswordExpiryDays > 0 &&
                (user.PasswordChangedAt ?? user.CreatedAt).AddDays(policy.PasswordExpiryDays) <= now)
            {
                string resetToken = await passwordResetService.IssueTokenAsync(db, user.Id, policy, client.IpAddress);
                return new LoginResult(LoginStatus.PasswordExpired, ResetToken: resetToken);
            }

            // 5. 追加の認証 (ワンタイムパスワード)。画面と検証は未実装のため、有効にしているユーザーはここで止める
            if (user.MfaEnabled)
            {
                return new LoginResult(LoginStatus.MfaRequired);
            }

            // 6. パスワードの有効期限が近い場合は、変更するかを画面で確認する
            //    確認は 1 日 1 回 (日本時間の日付が変わるまで) だけ出す。出したら、その日時を記録する
            if (policy.PasswordExpiryDays > 0 && policy.PasswordExpiryWarningDays > 0)
            {
                DateTime expiresAt = (user.PasswordChangedAt ?? user.CreatedAt).AddDays(policy.PasswordExpiryDays);
                int daysLeft = (int)Math.Ceiling((expiresAt - now).TotalDays);
                bool notifiedToday = user.PasswordExpiryNotifiedAt is DateTime notifiedAt &&
                    AppTime.ToDisplayDate(notifiedAt) == AppTime.ToDisplayDate(now);
                if (daysLeft <= policy.PasswordExpiryWarningDays && !notifiedToday)
                {
                    await repository.RecordExpiryNotifiedAsync(db, user.Id, now);
                    return new LoginResult(LoginStatus.PasswordExpiringSoon, PasswordExpiresAt: expiresAt, DaysUntilExpiry: daysLeft,
                        PendingSignIn: new PendingSignIn(user.Id, AuthMethods.Password, null));
                }
            }

            string ticket = await IssueSignInTicketAsync(db, user.Id, AuthMethods.Password, null, client.IpAddress);
            return new LoginResult(LoginStatus.Succeeded, Ticket: ticket);
        }

        // 確認の後でサインイン用チケットを発行する (PasswordExpiringSoon のとき)。2 回目以降は null
        public async Task<string?> CompleteSignInAsync(PendingSignIn pending, ClientConnectionInfo client)
        {
            if (pending.Used)
            {
                return null;
            }
            pending.Used = true;
            await using DbContextAuth db = await repository.CreateDbContextAsync();
            return await IssueSignInTicketAsync(db, pending.UserId, pending.AuthMethod, pending.MfaMethod, client.IpAddress);
        }

        private async Task<bool> IsIpBlockedAsync(DbContextAuth db, SecurityPolicy policy, string? ipAddress, DateTime now)
        {
            if (!policy.IpLimitEnabled || string.IsNullOrEmpty(ipAddress))
            {
                return false;
            }

            AuthRepository.IpFailureStat stat =
                await repository.GetIpFailuresAsync(db, ipAddress, now.AddMinutes(-policy.IpFailedWindowMinutes));
            return stat.Count >= policy.IpMaxFailedAttempts &&
                stat.LastAttemptedAt is DateTime last &&
                last.AddMinutes(policy.IpBlockMinutes) > now;
        }

        // ---- サインイン用チケット ----

        private sealed record TicketPayload(string Token, string AuthMethod, string? MfaMethod);

        // 本人確認 (と追加の認証) が済んだユーザーに、1 回限りのサインイン用チケットを発行する
        // DB には使用済みの管理用にトークンのハッシュだけを保存し、本人確認の方式はチケットに暗号化して入れる
        public async Task<string> IssueSignInTicketAsync(DbContextAuth db, long userId, string authMethod, string? mfaMethod, string? ipAddress)
        {
            string token = TokenGenerator.Create();
            DateTime now = DateTime.UtcNow;
            await repository.InsertTokenAsync(db, userId, UserTokenPurposes.SignInTicket, TokenGenerator.Hash(token),
                now + SignInTicketLifetime, ipAddress, now);
            return ticketProtector.Protect(JsonSerializer.Serialize(new TicketPayload(token, authMethod, mfaMethod)));
        }

        // サインイン用チケットを使う。使えない (期限切れ・使用済み・改ざん) 場合は null
        public async Task<SignInTicketInfo?> RedeemSignInTicketAsync(string? ticket)
        {
            if (string.IsNullOrEmpty(ticket))
            {
                return null;
            }

            TicketPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<TicketPayload>(ticketProtector.Unprotect(ticket));
            }
            catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException or FormatException)
            {
                logger.Warn("サインイン用チケットを復号できませんでした。", ex);
                return null;
            }
            if (payload == null)
            {
                return null;
            }

            DateTime now = DateTime.UtcNow;
            await using DbContextAuth db = await repository.CreateDbContextAsync();
            UserToken? token = await repository.GetValidTokenAsync(db, TokenGenerator.Hash(payload.Token), UserTokenPurposes.SignInTicket, now);
            if (token == null || !await repository.MarkTokenUsedAsync(db, token.Id, now))
            {
                return null;
            }

            User? user = await repository.GetUserByIdAsync(db, token.UserId);
            if (user == null || !user.IsActive)
            {
                return null;
            }
            return new SignInTicketInfo(user, payload.AuthMethod, payload.MfaMethod);
        }

        public async Task<List<string>> GetRoleCodesAsync(long userId)
        {
            await using DbContextAuth db = await repository.CreateDbContextAsync();
            return await repository.GetRoleCodesAsync(db, userId);
        }
    }

    // 画面を操作しているブラウザの接続元の情報 (circuit ごと)。
    // Interactive Server の画面では HttpContext を使えないので、最初の HTTP リクエスト (プリレンダリング) のときに
    // MainLayout が取得して [PersistentState] で引き継ぎ、ここに入れる。
    // 接続元 IP は UseForwardedHeaders の設定 (appsettings.json の ForwardedHeaders) を反映した値
    public sealed class ClientConnectionInfo
    {
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }
}
