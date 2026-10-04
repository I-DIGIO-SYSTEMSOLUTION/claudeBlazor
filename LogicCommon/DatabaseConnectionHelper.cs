#nullable disable

using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.ComponentModel;

namespace LogicCommon
{
    public class DatabaseConnectionHelper
    {

        private static readonly log4net.ILog logger =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // true のとき、SQL のログにパラメータの値も出す (値に個人情報などが含まれうるので Development のみで有効にする)
        public static bool EnableSensitiveDataLogging { get; set; }


        // databaseType に指定できる値。未指定の場合は DefaultDatabaseType として扱う
        public const string DefaultDatabaseType = "SQLServer";
        public static readonly IReadOnlyList<string> DatabaseTypes = ["SQLServer", "PostgreSQL"];

        public enum DATABASE_TYPE
        {
            [Description("SQLServer")]
            SQLServer = 100,
        }

        public static DbContextOptions<DbContext> CreateContextOption(
            string databaseType,
            string userName,
            string password,
            string connectionString
            )
        {
            connectionString = CreateConnectionString(
                userName,
                password,
                connectionString
                );

            return CreateContextOption(
                databaseType,
                connectionString
                );
        }


        public static DbContextOptions<DbContext> CreateContextOption(string databaseType, string connectionString)
        {
            DbContextOptionsBuilder<DbContext> optionsBuilder = new();
            UseDatabase(optionsBuilder, databaseType, connectionString);
            return optionsBuilder.Options;
        }

        // databaseType に応じて DB プロバイダーを設定する (未指定は SQLServer)
        // DI 登録 (AddDbContextFactory) と CreateContextOption の両方から使う
        // allowSensitiveDataLogging = false にすると、EnableSensitiveDataLogging が true でもパラメータの値をログに出さない
        // (パスワードハッシュやトークンを扱う認証関係の DbContext で使う)
        public static DbContextOptionsBuilder UseDatabase(
            DbContextOptionsBuilder optionsBuilder,
            string databaseType,
            string connectionString,
            bool allowSensitiveDataLogging = true)
        {
            if (string.IsNullOrEmpty(databaseType) ||
                "SQLServer".Equals(databaseType))
            {
                optionsBuilder.UseSqlServer(connectionString);
            }
            else if ("PostgreSQL".Equals(databaseType))
            {
                optionsBuilder.UseNpgsql(connectionString);
            }
            else
            {
                throw new ArgumentException($"未対応の databaseType です: {databaseType}", nameof(databaseType));
            }

            // 実行した SQL (Database.Command カテゴリ) だけを log4net に出す
            // 正常に実行した SQL は DEBUG、失敗した SQL (Failed executing DbCommand) は ERROR、警告は WARN
            optionsBuilder.LogTo(
                (eventId, level) =>
                    level >= LogLevel.Information &&
                    eventId.Name != null &&
                    eventId.Name.StartsWith(DbLoggerCategory.Database.Command.Name + ".", StringComparison.Ordinal),
                LogDatabaseCommand);

            if (EnableSensitiveDataLogging && allowSensitiveDataLogging)
            {
                optionsBuilder.EnableSensitiveDataLogging();
            }

            return optionsBuilder;
        }

        private static void LogDatabaseCommand(EventData eventData)
        {
            // 日時などは log4net 側で付けるので、EF Core のメッセージ本文だけを出す
            string message = eventData.ToString();

            if (eventData.LogLevel >= LogLevel.Error)
            {
                logger.Error(message);
            }
            else if (eventData.LogLevel == LogLevel.Warning)
            {
                logger.Warn(message);
            }
            else
            {
                logger.Debug(message);
            }
        }

        public static string CreateConnectionString(
            string userName,
            string password,
            string connectionString)
        {
            // 接続文字列
            connectionString = connectionString.Replace("${UserName}", userName);
            connectionString = connectionString.Replace("${Password}", password);

            return connectionString;
        }

        public static string CheckDatabaseConnection(
            string databaseType,
            string userName,
            string password,
            string connectionString)
        {
            // 接続文字列
            connectionString = CreateConnectionString(userName, password, connectionString);

            DbContextOptions<DbContext> localContextOptions =
                CreateContextOption(
                    databaseType,
                    connectionString);


            using (DbContext context = new DbContext(localContextOptions))
            {
                try
                {
                    context.Database.BeginTransaction();
                    context.Database.RollbackTransaction();
                }
                catch (Exception ex)
                {
                    logger.Error(ex.Message);
                    logger.Error(ex.StackTrace);
                    return new StringBuilder()
                        .Append("接続時にエラーが発生しました。\r\n")
                        .Append("エラーは以下の通りです。\r\n")
                        .Append(ex.Message).ToString();
                }
            }

            return "true";
        }
    }
}
