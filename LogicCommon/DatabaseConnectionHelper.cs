#nullable disable

using System.Text;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

namespace LogicCommon
{
    public class DatabaseConnectionHelper
    {

        private static readonly log4net.ILog logger =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly Microsoft.Extensions.Logging.ILoggerFactory loggerFactory = 
            LoggerFactory.Create(builder =>
                {
                    builder
                      .AddFilter(
                        category: DbLoggerCategory.Database.Command.Name,
                        level: LogLevel.Information);
                });


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
        public static DbContextOptionsBuilder UseDatabase(
            DbContextOptionsBuilder optionsBuilder,
            string databaseType,
            string connectionString)
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

            return optionsBuilder
                .LogTo(logger.Debug)
                .UseLoggerFactory(loggerFactory);
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
