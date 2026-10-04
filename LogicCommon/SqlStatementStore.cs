#nullable disable

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace LogicCommon
{
    // Sql フォルダ配下の *.sql を起動時にメモリへ読み込み、ファイルの変更を検知したら読み直す。
    // SQL 文の取得 (Get / Build) ではファイルにアクセスしない。
    // 条件が可変な SQL は Build を使う (書き方は SqlTemplate を参照)。
    // キーは Sql フォルダからの相対パスで拡張子なし、区切りは "/" (例: "WeatherForecast/Search")。
    //
    // DB ごとに SQL 文が異なる場合は、ファイル名の末尾に "_<DB 種別>" (DB 種別は databaseType の値) を付ける。
    //   WeatherForecast/Search.sql             … 共通 (DB 用のファイルが無い場合に使う)
    //   WeatherForecast/Search_PostgreSQL.sql  … PostgreSQL 用
    //   WeatherForecast/Search_SQLServer.sql   … SQL Server 用
    // Get / Build には DB 種別を付けない名前 ("WeatherForecast/Search") を渡す。
    // "_" は DB 種別の区切り専用。"_" の後ろが DB 種別でないファイルはエラーログを出して読み込まない。
    public sealed class SqlStatementStore : IDisposable
    {
        public const string SqlFolderName = "Sql";

        // ファイル名と DB 種別の区切り文字 (例: Search_PostgreSQL.sql)
        public const char DatabaseTypeSeparator = '_';

        // 保存時は通知が複数回来たり書き込み途中だったりするため、最後の通知から少し待って読み直す
        private static readonly TimeSpan ReloadDelay = TimeSpan.FromMilliseconds(300);

        private readonly PhysicalFileProvider fileProvider;
        private readonly IDisposable changeRegistration;
        private readonly ILogger<SqlStatementStore> logger;

        // エラーはコンソール (ILogger) に加えて log4net のログファイル (yyyyMMdd_app_ERROR.log) にも残す
        private static readonly log4net.ILog fileLogger =
            log4net.LogManager.GetLogger(typeof(SqlStatementStore));
        private readonly object reloadLock = new();

        // 読み直しのたびに丸ごと差し替える (読み取り側はロック不要)
        private volatile IReadOnlyDictionary<string, string> statements =
            new Dictionary<string, string>();

        private CancellationTokenSource reloadDelayCts;

        // SQL ファイルを選ぶときの DB 種別 (DatabaseCommonConnectionSetting の databaseType)
        public string DatabaseType { get; }

        public SqlStatementStore(IHostEnvironment env, ILogger<SqlStatementStore> logger, string databaseType)
        {
            this.logger = logger;
            DatabaseType = string.IsNullOrEmpty(databaseType)
                ? DatabaseConnectionHelper.DefaultDatabaseType
                : databaseType;

            string sqlPath = Path.Combine(env.ContentRootPath, SqlFolderName);
            if (!Directory.Exists(sqlPath))
            {
                throw new DirectoryNotFoundException($"SQL フォルダがありません: {sqlPath}");
            }

            fileProvider = new PhysicalFileProvider(sqlPath);
            Reload();

            // Watch のトークンは 1 回通知すると使い終わりになるので、ChangeToken.OnChange で毎回取り直す
            changeRegistration = ChangeToken.OnChange(
                () => fileProvider.Watch("**/*.sql"),
                ScheduleReload);
        }

        public string Get(string name)
        {
            return Resolve(name).Sql;
        }

        // --#if による条件の取捨と、@名前 のパラメータ化 (IN 句の展開を含む) を行う
        // 例: SqlStore.Build("WeatherForecast/Search", new { summaries, minTemperatureC })
        public SqlQuery Build(string name, object parameters)
        {
            (string key, string sql) = Resolve(name);
            return SqlTemplate.Render(key, sql, parameters);
        }

        // DB 用のファイル (name_<DB 種別>) を優先し、無ければ共通のファイル (name) を使う
        private (string Key, string Sql) Resolve(string name)
        {
            // 2 回の検索の間に読み直しで差し替わらないよう、参照を取ってから検索する
            IReadOnlyDictionary<string, string> current = statements;

            string databaseKey = $"{name}{DatabaseTypeSeparator}{DatabaseType}";
            if (current.TryGetValue(databaseKey, out string sql))
            {
                return (databaseKey, sql);
            }
            if (current.TryGetValue(name, out sql))
            {
                return (name, sql);
            }
            throw new KeyNotFoundException(
                $"SQL ファイルが見つかりません: {SqlFolderName}/{databaseKey}.sql または {SqlFolderName}/{name}.sql");
        }

        private void ScheduleReload()
        {
            CancellationTokenSource cts = new();
            Interlocked.Exchange(ref reloadDelayCts, cts)?.Cancel();

            _ = Task.Delay(ReloadDelay, cts.Token).ContinueWith(
                _ => Reload(),
                cts.Token,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
        }

        private void Reload()
        {
            lock (reloadLock)
            {
                try
                {
                    Dictionary<string, string> loaded = new();
                    string root = fileProvider.Root;

                    foreach (string path in Directory.EnumerateFiles(root, "*.sql", SearchOption.AllDirectories))
                    {
                        string name = Path.ChangeExtension(Path.GetRelativePath(root, path), null)
                            .Replace(Path.DirectorySeparatorChar, '/');
                        if (!IsValidFileName(name))
                        {
                            continue;
                        }
                        loaded[name] = File.ReadAllText(path).Trim();
                    }

                    statements = loaded;
                    logger.LogInformation("SQL ファイルを読み込みました (DB 種別 {DatabaseType}, {Count} 件): {Names}",
                        DatabaseType, loaded.Count, string.Join(", ", loaded.Keys.Order()));
                }
                catch (Exception ex)
                {
                    // 読み込みに失敗した場合は、前回読み込んだ内容のまま動かす
                    const string message = "SQL ファイルの読み込みに失敗しました。前回の内容を使い続けます。";
                    logger.LogError(ex, message);
                    fileLogger.Error(message, ex);
                }
            }
        }

        // ファイル名に "_" を含む場合は "<名前>_<DB 種別>" の形かを確認する。
        // "Search_PostgeSQL" のような DB 種別の綴り間違いは気づきにくいので、
        // エラーログを出してそのファイルは読み込まない (ファイルを直すと読み直しのときに再チェックされる)
        private bool IsValidFileName(string key)
        {
            string fileName = key[(key.LastIndexOf('/') + 1)..];
            string[] parts = fileName.Split(DatabaseTypeSeparator);
            if (parts.Length == 1)
            {
                return true;
            }

            string message;
            if (parts.Length == 2 && parts[0].Length > 0 && DatabaseConnectionHelper.DatabaseTypes.Contains(parts[1]))
            {
                return true;
            }
            if (parts.Length == 2 && parts[0].Length > 0)
            {
                message =
                    $"{SqlFolderName}/{key}.sql の \"{parts[1]}\" は DB 種別として認識できません " +
                    $"(指定できる値: {string.Join(", ", DatabaseConnectionHelper.DatabaseTypes)})。このファイルは使われません。";
            }
            else
            {
                message =
                    $"{SqlFolderName}/{key}.sql のファイル名が正しくありません。\"{DatabaseTypeSeparator}\" は DB 種別の区切り専用で、" +
                    $"<名前>.sql または <名前>{DatabaseTypeSeparator}<DB 種別>.sql の形にしてください。このファイルは使われません。";
            }

            logger.LogError(message);
            fileLogger.Error(message);
            return false;
        }

        public void Dispose()
        {
            changeRegistration.Dispose();
            reloadDelayCts?.Cancel();
            fileProvider.Dispose();
        }
    }
}
