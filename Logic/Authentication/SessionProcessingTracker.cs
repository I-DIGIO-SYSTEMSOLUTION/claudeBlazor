using System.Collections.Concurrent;

namespace LogicCommon
{
    // ユーザーの操作で始まったサーバー側の処理を、セッションごとに数える。
    // 実行中の処理がある間はログアウトにせず、アイドル時間は処理が終わった時刻から数える。
    //
    // DB への書き込みを減らすため、処理の開始・終了はメモリ上で数えるだけにし、
    //  - HeartbeatInterval 以上続いている処理は、続いている間 HeartbeatInterval ごとに processing_heartbeat_at を更新する
    //  - HeartbeatInterval 以上かかった処理は、終わったときに終了日時を processing_heartbeat_at に書く
    // 短い処理はクリックなどの操作の記録 (last_activity_at) で足りるので書かない。
    // 件数ではなく日時を書くので、アプリが落ちても「処理中」のまま残らない (更新が止まり、通常のタイムアウトに戻る)
    public sealed class SessionProcessingTracker
    {
        public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(SessionProcessingTracker));

        private sealed class Entry
        {
            public int Count;
            public DateTime StartedAt;
            // 件数が 0 になって辞書から外した Entry には、以降は加算しない
            public bool Removed;
        }

        private readonly ConcurrentDictionary<Guid, Entry> entries = new();
        private readonly IServiceProvider services;

        // SessionService は SessionProcessingTracker を使うので、循環しないよう必要になったときに取り出す
        public SessionProcessingTracker(IServiceProvider services)
        {
            this.services = services;
        }

        // 処理の開始。戻り値を Dispose すると終了になる (using で囲んで使う)
        public IDisposable Begin(Guid sessionId)
        {
            while (true)
            {
                Entry entry = entries.GetOrAdd(sessionId, _ => new Entry());
                lock (entry)
                {
                    if (entry.Removed)
                    {
                        continue;
                    }
                    if (entry.Count == 0)
                    {
                        entry.StartedAt = DateTime.UtcNow;
                    }
                    entry.Count++;
                    return new Scope(this, sessionId, entry);
                }
            }
        }

        public bool IsProcessing(Guid sessionId) =>
            entries.TryGetValue(sessionId, out Entry? entry) && Volatile.Read(ref entry.Count) > 0;

        // HeartbeatInterval 以上続いている処理のセッション (定期的な生存通知の対象)
        public List<Guid> GetLongRunningSessionIds()
        {
            DateTime threshold = DateTime.UtcNow - HeartbeatInterval;
            List<Guid> ids = new();
            foreach ((Guid id, Entry entry) in entries)
            {
                lock (entry)
                {
                    if (entry.Count > 0 && entry.StartedAt <= threshold)
                    {
                        ids.Add(id);
                    }
                }
            }
            return ids;
        }

        private void End(Guid sessionId, Entry entry)
        {
            bool writeEndTime = false;
            lock (entry)
            {
                entry.Count--;
                if (entry.Count == 0)
                {
                    writeEndTime = DateTime.UtcNow - entry.StartedAt >= HeartbeatInterval;
                    entry.Removed = true;
                    entries.TryRemove(new KeyValuePair<Guid, Entry>(sessionId, entry));
                }
            }

            if (writeEndTime)
            {
                // 処理の完了を待たせないよう、書き込みは別スレッドで行う
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await services.GetRequiredService<SessionService>().HeartbeatAsync([sessionId]);
                    }
                    catch (Exception ex)
                    {
                        logger.Error("サーバー処理の終了日時を記録できませんでした。", ex);
                    }
                });
            }
        }

        private sealed class Scope(SessionProcessingTracker tracker, Guid sessionId, Entry entry) : IDisposable
        {
            private int disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0)
                {
                    tracker.End(sessionId, entry);
                }
            }
        }
    }

    // 定期処理: 実行中の長い処理の生存通知と、残っているセッションの後始末
    public sealed class SessionMaintenanceService : BackgroundService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(SessionMaintenanceService));

        // 残っているセッションを終了する間隔
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

        private readonly SessionProcessingTracker tracker;
        private readonly SessionService sessionService;

        public SessionMaintenanceService(SessionProcessingTracker tracker, SessionService sessionService)
        {
            this.tracker = tracker;
            this.sessionService = sessionService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using PeriodicTimer timer = new(SessionProcessingTracker.HeartbeatInterval);
            DateTime nextCleanup = DateTime.UtcNow;

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await sessionService.HeartbeatAsync(tracker.GetLongRunningSessionIds());

                    if (DateTime.UtcNow >= nextCleanup)
                    {
                        nextCleanup = DateTime.UtcNow + CleanupInterval;
                        await sessionService.EndIdleSessionsAsync();
                    }
                }
                catch (Exception ex)
                {
                    // DB に接続できないときなども、次の周期で再試行する
                    logger.Error("セッションの定期処理に失敗しました。", ex);
                }
            }
        }
    }
}
