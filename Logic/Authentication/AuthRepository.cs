using Microsoft.EntityFrameworkCore;

namespace LogicCommon
{
    // ログイン関係のテーブルへのアクセス。SQL 文は Sql/<テーブル>/*.sql
    // 各メソッドは呼び出し側が作った DbContextAuth を受け取る (複数の SQL を 1 つのトランザクションで実行できるように)
    // 日時は DB の now() ではなくアプリの現在日時 (UTC) を渡す (判定に使う日時をアプリ側にそろえるため)
    public sealed class AuthRepository
    {
        private readonly IDbContextFactory<DbContextAuth> dbFactory;
        private readonly SqlStatementStore sqlStore;

        // セキュリティポリシーはセッションの確認 (画面ごとに 15 秒おき) でも読むので、短時間だけメモリに持つ
        private static readonly TimeSpan PolicyCacheDuration = TimeSpan.FromSeconds(10);
        private SecurityPolicy? cachedPolicy;
        private DateTime cachedPolicyAt;

        public AuthRepository(IDbContextFactory<DbContextAuth> dbFactory, SqlStatementStore sqlStore)
        {
            this.dbFactory = dbFactory;
            this.sqlStore = sqlStore;
        }

        public Task<DbContextAuth> CreateDbContextAsync() => dbFactory.CreateDbContextAsync();

        // ---- 共通 ----

        private async Task<int> ExecuteAsync(DbContextAuth db, string name, object parameters)
        {
            SqlQuery query = sqlStore.Build(name, parameters);
            return await db.Database.ExecuteSqlRawAsync(query.Sql, query.Parameters);
        }

        // スカラー値や任意の型の結果 (列名をプロパティ名に合わせる。スカラーは "Value")
        // EF Core が SQL を副問い合わせで包まないよう、ToListAsync でそのまま実行する
        private async Task<List<T>> QueryAsync<T>(DbContextAuth db, string name, object parameters)
        {
            SqlQuery query = sqlStore.Build(name, parameters);
            return await db.Database.SqlQueryRaw<T>(query.Sql, query.Parameters).ToListAsync();
        }

        private async Task<List<T>> QueryEntitiesAsync<T>(DbSet<T> set, string name, object parameters) where T : class
        {
            SqlQuery query = sqlStore.Build(name, parameters);
            return await set.FromSqlRaw(query.Sql, query.Parameters).AsNoTracking().ToListAsync();
        }

        // ---- 設定 ----

        // useCache = false は、キャッシュを使わずに DB から読む (パスワードポリシーの変更画面で最新の値を出すため)
        public async Task<SecurityPolicy> GetSecurityPolicyAsync(bool useCache = true)
        {
            SecurityPolicy? policy = cachedPolicy;
            if (useCache && policy != null && DateTime.UtcNow - cachedPolicyAt < PolicyCacheDuration)
            {
                return policy;
            }

            await using DbContextAuth db = await CreateDbContextAsync();
            policy = (await QueryEntitiesAsync(db.SecurityPolicies, "SecurityPolicy/Select", new { })).FirstOrDefault()
                ?? throw new InvalidOperationException("security_policies に id = 1 の行がありません。");
            cachedPolicy = policy;
            cachedPolicyAt = DateTime.UtcNow;
            return policy;
        }

        // パスワードポリシーと自動ログアウトの時間を更新する。読み込んだ時点から他の人が変更していれば false
        // 更新できたらキャッシュを捨て、このサーバーでは次の読み込みから新しい値を使う (他のサーバーでは最大 10 秒遅れる)
        public async Task<bool> UpdatePasswordPolicyAsync(SecurityPolicy policy, DateTime expectedUpdatedAt, DateTime now)
        {
            await using DbContextAuth db = await CreateDbContextAsync();
            bool updated = await ExecuteAsync(db, "SecurityPolicy/UpdatePasswordPolicy", new
            {
                minLength = policy.PasswordMinLength,
                maxLength = policy.PasswordMaxLength,
                requireUppercase = policy.RequireUppercase,
                requireLowercase = policy.RequireLowercase,
                requireDigit = policy.RequireDigit,
                requireSymbol = policy.RequireSymbol,
                minCharTypes = policy.MinCharTypes,
                disallowLoginId = policy.DisallowLoginId,
                historyCount = policy.PasswordHistoryCount,
                expiryDays = policy.PasswordExpiryDays,
                expiryWarningDays = policy.PasswordExpiryWarningDays,
                idleTimeoutMinutes = policy.IdleTimeoutMinutes,
                timeoutWarningSeconds = policy.TimeoutWarningSeconds,
                expectedUpdatedAt,
                now,
            }) == 1;
            cachedPolicy = null;
            return updated;
        }

        public async Task<MailSetting?> GetMailSettingAsync(DbContextAuth db) =>
            (await QueryEntitiesAsync(db.MailSettings, "MailSetting/Select", new { })).FirstOrDefault();

        // ---- ユーザー ----

        public async Task<User?> GetUserByLoginIdAsync(DbContextAuth db, string loginId) =>
            (await QueryEntitiesAsync(db.Users, "User/SelectByLoginId", new { loginId })).FirstOrDefault();

        public async Task<User?> GetUserByIdAsync(DbContextAuth db, long id) =>
            (await QueryEntitiesAsync(db.Users, "User/SelectById", new { id })).FirstOrDefault();

        public async Task<User?> GetUserByEmailAsync(DbContextAuth db, string email) =>
            (await QueryEntitiesAsync(db.Users, "User/SelectByEmail", new { email })).FirstOrDefault();

        public Task<List<User>> GetUsersAsync(DbContextAuth db) =>
            QueryEntitiesAsync(db.Users, "User/Search", new { });

        public async Task<int> CountUsersAsync(DbContextAuth db) =>
            (await QueryAsync<int>(db, "User/Count", new { })).Single();

        public async Task<bool> LoginIdExistsAsync(DbContextAuth db, string loginId) =>
            (await QueryAsync<int>(db, "User/CountByLoginId", new { loginId })).Single() > 0;

        public async Task<bool> EmailExistsAsync(DbContextAuth db, string email) =>
            (await QueryAsync<int>(db, "User/CountByEmail", new { email })).Single() > 0;

        public async Task<string> GetPasswordHistoryJsonAsync(DbContextAuth db, long id) =>
            (await QueryAsync<string>(db, "User/SelectPasswordHistory", new { id })).SingleOrDefault() ?? "[]";

        public async Task<long> InsertUserAsync(DbContextAuth db, string loginId, string passwordHash, string passwordHistoryJson,
            string displayName, string email, DateTime now) =>
            (await QueryAsync<long>(db, "User/Insert", new
            {
                loginId,
                passwordHash,
                passwordHistory = passwordHistoryJson,
                displayName,
                email,
                now,
            })).Single();

        // パスワードの誤りを記録し、ロックしたら true を返す
        public async Task<bool> RecordLoginFailureAsync(DbContextAuth db, long id, SecurityPolicy policy, DateTime now) =>
            (await QueryAsync<bool>(db, "User/RecordLoginFailure", new
            {
                id,
                resetEnabled = policy.FailedCountResetMinutes > 0,
                resetSince = now.AddMinutes(-policy.FailedCountResetMinutes),
                lockoutEnabled = policy.LockoutEnabled,
                maxFailedAttempts = policy.MaxFailedAttempts,
                lockedUntil = now.AddMinutes(policy.LockoutMinutes),
                now,
            })).SingleOrDefault();

        public Task<int> RecordLoginSuccessAsync(DbContextAuth db, long id, DateTime now) =>
            ExecuteAsync(db, "User/RecordLoginSuccess", new { id, now });

        public Task<int> RecordExpiryNotifiedAsync(DbContextAuth db, long id, DateTime now) =>
            ExecuteAsync(db, "User/RecordExpiryNotified", new { id, now });

        // 読み込んだ時点から他の変更が入っていれば false
        public async Task<bool> UpdatePasswordAsync(DbContextAuth db, long id, string passwordHash, string passwordHistoryJson,
            string securityStamp, DateTime expectedUpdatedAt, DateTime now) =>
            await ExecuteAsync(db, "User/UpdatePassword", new
            {
                id,
                passwordHash,
                passwordHistory = passwordHistoryJson,
                securityStamp,
                expectedUpdatedAt,
                now,
            }) == 1;

        // ---- ロール ----

        public Task<List<Role>> GetRolesAsync(DbContextAuth db) =>
            QueryEntitiesAsync(db.Roles, "Role/Search", new { });

        public Task<List<string>> GetRoleCodesAsync(DbContextAuth db, long userId) =>
            QueryAsync<string>(db, "Role/SelectCodesByUserId", new { userId });

        public Task<List<long>> GetRoleIdsAsync(DbContextAuth db, IReadOnlyCollection<string> codes) =>
            QueryAsync<long>(db, "Role/SelectIdsByCodes", new { codes });

        public Task<int> InsertRoleIfMissingAsync(DbContextAuth db, string code, string name, DateTime now) =>
            ExecuteAsync(db, "Role/InsertIfMissing", new { code, name, now });

        public Task<int> InsertUserRoleAsync(DbContextAuth db, long userId, long roleId, DateTime now) =>
            ExecuteAsync(db, "UserRole/Insert", new { userId, roleId, now });

        // ---- セッション ----

        public Task<int> InsertSessionAsync(DbContextAuth db, Guid id, long userId, string authMethod, string? mfaMethod,
            string? ipAddress, string? userAgent, DateTime now) =>
            ExecuteAsync(db, "UserSession/Insert", new { id, userId, authMethod, mfaMethod, ipAddress, userAgent, now });

        public async Task<UserSession?> GetSessionAsync(DbContextAuth db, Guid id) =>
            (await QueryEntitiesAsync(db.UserSessions, "UserSession/SelectById", new { id })).FirstOrDefault();

        public Task<int> TouchSessionAsync(DbContextAuth db, Guid id, DateTime now) =>
            ExecuteAsync(db, "UserSession/Touch", new { id, now });

        public Task<int> HeartbeatSessionsAsync(DbContextAuth db, IReadOnlyCollection<Guid> ids, DateTime now) =>
            ExecuteAsync(db, "UserSession/Heartbeat", new { ids, now });

        public Task<int> EndSessionAsync(DbContextAuth db, Guid id, string reason, DateTime now) =>
            ExecuteAsync(db, "UserSession/End", new { id, reason, now });

        public Task<int> EndSessionsByUserAsync(DbContextAuth db, long userId, string reason, Guid? exceptId, DateTime now) =>
            ExecuteAsync(db, "UserSession/EndByUser", new { userId, reason, exceptId, now });

        public Task<int> EndIdleSessionsAsync(DbContextAuth db, DateTime threshold, DateTime now) =>
            ExecuteAsync(db, "UserSession/EndIdle", new { threshold, reason = SessionEndReasons.IdleTimeout, now });

        // ---- トークン ----

        public Task<int> InsertTokenAsync(DbContextAuth db, long userId, string purpose, string tokenHash, DateTime expiresAt,
            string? ipAddress, DateTime now) =>
            ExecuteAsync(db, "UserToken/Insert", new { userId, purpose, tokenHash, expiresAt, ipAddress, now });

        public async Task<UserToken?> GetValidTokenAsync(DbContextAuth db, string tokenHash, string purpose, DateTime now) =>
            (await QueryEntitiesAsync(db.UserTokens, "UserToken/SelectValidByHash", new { tokenHash, purpose, now })).FirstOrDefault();

        // 使用済みにできたら true (同時に使われた場合、片方だけが true になる)
        public async Task<bool> MarkTokenUsedAsync(DbContextAuth db, long id, DateTime now) =>
            await ExecuteAsync(db, "UserToken/MarkUsed", new { id, now }) == 1;

        public Task<int> RevokeTokensAsync(DbContextAuth db, long userId, string purpose, DateTime now) =>
            ExecuteAsync(db, "UserToken/RevokeByUser", new { userId, purpose, now });

        // ---- ログイン履歴 ----

        public Task<int> InsertLoginHistoryAsync(DbContextAuth db, long? userId, string loginId, string authMethod,
            bool succeeded, string? failureReason, string? ipAddress, string? userAgent, DateTime now) =>
            ExecuteAsync(db, "LoginHistory/Insert", new
            {
                userId,
                // 列の長さ (256 文字) を超える入力でも記録できるよう切り詰める
                loginId = loginId.Length > 256 ? loginId[..256] : loginId,
                authMethod,
                succeeded,
                failureReason,
                ipAddress,
                userAgent = userAgent is { Length: > 512 } ? userAgent[..512] : userAgent,
                now,
            });

        public sealed class IpFailureStat
        {
            public int Count { get; set; }
            public DateTime? LastAttemptedAt { get; set; }
        }

        public async Task<IpFailureStat> GetIpFailuresAsync(DbContextAuth db, string ipAddress, DateTime since) =>
            (await QueryAsync<IpFailureStat>(db, "LoginHistory/SelectIpFailures", new
            {
                ipAddress,
                reasons = new[] { LoginFailureReasons.InvalidPassword, LoginFailureReasons.NotFound },
                since,
            })).Single();
    }
}
