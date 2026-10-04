#nullable disable

using Microsoft.EntityFrameworkCore;
using XMLManager;
using LogicCommon;

namespace DatabaseLogic
{
    public sealed class DatabaseAccessLogicSingleton
    {

        // シングルトンフラグ (Lazy<T> は既定でスレッドセーフなので lock は不要)
        private static readonly Lazy<DatabaseAccessLogicSingleton> _instance =
            new(() => new DatabaseAccessLogicSingleton());

        private static readonly log4net.ILog logger =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private DbContextOptions<DbContext> contextOptions;

        // ユーザー名・パスワードを埋め込み済みの接続文字列
        private string connectionString;

        public DatabaseCommonConnectionSetting databaseCommonConnectionSetting;

        bool initFlg = false;

        private DatabaseAccessLogicSingleton() // privateのコンストラクタ
        {
            Init();
        }

        public static DatabaseAccessLogicSingleton GetInstance => _instance.Value;

        //初期化処理
        public void Init()
        {
            if (initFlg)
            {
                return;
            }

            try
            {
                // memo: .netCore系でShift-JISを扱うためのおまじない
                // カレントディレクトリ移動
                string currentPath = System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                System.Environment.CurrentDirectory = currentPath;

                //XMLファイル読込み
                string settingPath =
                    Path.Combine(currentPath, SettingReadWrite.DatabaseCommonConnectionSettingXMLfileName);

                if (System.IO.File.Exists(settingPath))
                {
                    databaseCommonConnectionSetting = (DatabaseCommonConnectionSetting)SettingReadWrite.XMLReadInfo(settingPath, "DatabaseCommonConnectionSetting");
                    SettingConatentOption();

                }
                else
                {
                    databaseCommonConnectionSetting = new();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                logger.Error(ex.StackTrace);
            }

            initFlg = true;

        }

        public void SettingConatentOption()
        {
            string password = String.Empty;
            try
            {
                // 設定ファイルのパスワードは Auth.EncryptStringToBytes_Aes で暗号化してあるので復号する
                if (!string.IsNullOrEmpty(databaseCommonConnectionSetting.databasePassword))
                {
                    password = Auth.DecryptStringFromBytes_Aes(databaseCommonConnectionSetting.databasePassword);
                }

                connectionString = DatabaseConnectionHelper.
                    CreateConnectionString(
                        databaseCommonConnectionSetting.databaseUserName,
                        password,
                        databaseCommonConnectionSetting.databaseConnectionString
                        );

                contextOptions = DatabaseConnectionHelper.CreateContextOption(
                    databaseCommonConnectionSetting.databaseType,
                    connectionString);

            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                logger.Error(ex.StackTrace);
            }
        }

        public DbContextOptions<DbContext> GetConnectionContext()
        {
            return contextOptions;
        }

        // AddDbContextFactory などに渡す DbContextOptionsBuilder へ、設定ファイルの DB 種別と接続文字列を適用する
        // allowSensitiveDataLogging = false にすると、開発時でも SQL のログにパラメータの値を出さない
        public void ConfigureDbContext(DbContextOptionsBuilder optionsBuilder, bool allowSensitiveDataLogging = true)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    $"DB の接続設定が読み込まれていません: {SettingReadWrite.DatabaseCommonConnectionSettingXMLfileName}");
            }

            DatabaseConnectionHelper.UseDatabase(
                optionsBuilder,
                databaseCommonConnectionSetting.databaseType,
                connectionString,
                allowSensitiveDataLogging);
        }
    }
}
