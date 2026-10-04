namespace LogicCommon
{
    // セッションの状態 (SessionService.GetStatusAsync の結果)
    // ExpiresAt は UTC。サーバー処理の実行中は、処理が終わるまで期限が来ない
    public sealed record SessionStatus(bool IsValid, string? EndReason, DateTime ExpiresAt, int WarningSeconds, bool IsProcessing)
    {
        public static SessionStatus Ended(string reason) => new(false, reason, DateTime.MinValue, 0, false);
    }

    // user_sessions の作成・確認・更新・終了
    // ログイン状態は Blazor の接続 (circuit) ではなくこの行で判定する。そのため、プロキシのタイムアウトで接続が切れても
    // 操作が idle_timeout_minutes 分ない場合以外はログアウトにならない
    public sealed class SessionService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(SessionService));

        private readonly AuthRepository repository;
        private readonly SessionProcessingTracker processingTracker;

        public SessionService(AuthRepository repository, SessionProcessingTracker processingTracker)
        {
            this.repository = repository;
            this.processingTracker = processingTracker;
        }

        public async Task<Guid> CreateAsync(long userId, string authMethod, string? mfaMethod, string? ipAddress, string? userAgent)
        {
            Guid id = Guid.NewGuid();
            await using DbContextAuth db = await repository.CreateDbContextAsync();
            await repository.InsertSessionAsync(db, id, userId, authMethod, mfaMethod, ipAddress,
                userAgent is { Length: > 512 } ? userAgent[..512] : userAgent, DateTime.UtcNow);
            return id;
        }

        // セッションが有効かを確認する。期限切れ・ユーザーの無効化を見つけたら、その場でセッションを終了する
        public async Task<SessionStatus> GetStatusAsync(Guid sessionId)
        {
            SecurityPolicy policy = await repository.GetSecurityPolicyAsync();
            await using DbContextAuth db = await repository.CreateDbContextAsync();

            UserSession? session = await repository.GetSessionAsync(db, sessionId);
            if (session == null)
            {
                return SessionStatus.Ended(SessionEndReasons.Revoked);
            }
            if (session.EndedAt != null)
            {
                return SessionStatus.Ended(session.EndReason ?? SessionEndReasons.Revoked);
            }

            DateTime now = DateTime.UtcNow;
            User? user = await repository.GetUserByIdAsync(db, session.UserId);
            if (user == null || !user.IsActive)
            {
                await repository.EndSessionAsync(db, sessionId, SessionEndReasons.Revoked, now);
                return SessionStatus.Ended(SessionEndReasons.Revoked);
            }

            TimeSpan idleTimeout = TimeSpan.FromMinutes(policy.IdleTimeoutMinutes);

            // このサーバーで処理を実行中なら、処理が終わるまで期限を延ばし続ける
            // (他のサーバーで実行中の処理は processing_heartbeat_at の定期更新で反映される)
            bool processing = processingTracker.IsProcessing(sessionId);
            DateTime lastActive = processing ? now : Max(session.LastActivityAt, session.ProcessingHeartbeatAt);
            DateTime expiresAt = lastActive + idleTimeout;

            if (now >= expiresAt)
            {
                await repository.EndSessionAsync(db, sessionId, SessionEndReasons.IdleTimeout, now);
                return SessionStatus.Ended(SessionEndReasons.IdleTimeout);
            }

            return new SessionStatus(true, null, expiresAt, policy.TimeoutWarningSeconds, processing);
        }

        // ブラウザから届く「最後の操作から何秒前か」の上限 (ブラウザの通知の間隔 60 秒に余裕を持たせた値)
        private static readonly TimeSpan MaxReportedIdle = TimeSpan.FromMinutes(2);

        // 操作があったことを記録する (どのタブの操作でも同じ行を更新するので、1 つのタブで操作していれば他のタブも有効なまま)
        // idle は実際の操作から通知までの時間。記録するのは通知した時刻ではなく、実際に操作した時刻
        // (前の記録より古い時刻では更新しない。Sql/UserSession/Touch.sql)
        public async Task TouchAsync(Guid sessionId, TimeSpan idle = default)
        {
            if (idle < TimeSpan.Zero) idle = TimeSpan.Zero;
            if (idle > MaxReportedIdle) idle = MaxReportedIdle;
            await using DbContextAuth db = await repository.CreateDbContextAsync();
            await repository.TouchSessionAsync(db, sessionId, DateTime.UtcNow - idle);
        }

        public async Task EndAsync(Guid sessionId, string reason)
        {
            await using DbContextAuth db = await repository.CreateDbContextAsync();
            await repository.EndSessionAsync(db, sessionId, reason, DateTime.UtcNow);
        }

        // サーバー処理の生存通知・終了日時を記録する (SessionProcessingTracker から呼ぶ)
        public async Task HeartbeatAsync(IReadOnlyCollection<Guid> sessionIds)
        {
            if (sessionIds.Count == 0)
            {
                return;
            }
            await using DbContextAuth db = await repository.CreateDbContextAsync();
            await repository.HeartbeatSessionsAsync(db, sessionIds, DateTime.UtcNow);
        }

        // ブラウザを閉じたなどで残っているセッションを終了する (定期的に呼ぶ)
        public async Task EndIdleSessionsAsync()
        {
            SecurityPolicy policy = await repository.GetSecurityPolicyAsync();
            DateTime now = DateTime.UtcNow;
            await using DbContextAuth db = await repository.CreateDbContextAsync();
            int count = await repository.EndIdleSessionsAsync(db, now.AddMinutes(-policy.IdleTimeoutMinutes), now);
            if (count > 0)
            {
                logger.Info($"操作が無いセッションを {count} 件終了しました。");
            }
        }

        private static DateTime Max(DateTime a, DateTime? b) => b is DateTime value && value > a ? value : a;
    }
}
