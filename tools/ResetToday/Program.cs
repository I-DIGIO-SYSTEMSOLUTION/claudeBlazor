using DatabaseLogic;
using LogicCommon;
using Microsoft.EntityFrameworkCore;
using XMLManager;

// 開発用: 日本時間の今日の打刻 (attendances の行) を全ユーザー分消す
// カレントディレクトリはリポジトリ直下 (Setting/ を相対パスで読む)。scripts/reset-today.sh から実行する
// 接続情報 (パスワードなど) は画面にもファイルにも出さない

// 日付はアプリと同じ方法 (日本時間) で求める
DateOnly today = AttendanceService.Today();

try
{
    // 設定ファイルの読み込みとパスワードの復号は、アプリ (DatabaseAccessLogicSingleton) と同じ処理を使う
    // (シングルトンは bin/ 側の設定を読み、カレントディレクトリも変えるので使わない)
    string settingPath = SettingReadWrite.DatabaseCommonConnectionSettingXMLfileName;
    if (!File.Exists(settingPath))
    {
        Console.Error.WriteLine($"接続設定ファイルがありません: {settingPath}（リポジトリ直下で実行してください）");
        return 1;
    }

    var setting = (DatabaseCommonConnectionSetting)SettingReadWrite.XMLReadInfo(settingPath, "DatabaseCommonConnectionSetting");
    string password = string.IsNullOrEmpty(setting.databasePassword)
        ? string.Empty
        : Auth.DecryptStringFromBytes_Aes(setting.databasePassword);

    string connectionString = DatabaseConnectionHelper.CreateConnectionString(
        setting.databaseUserName, password, setting.databaseConnectionString);

    DbContextOptionsBuilder<DbContextAttendance> optionsBuilder = new();
    DatabaseConnectionHelper.UseDatabase(optionsBuilder, setting.databaseType, connectionString, allowSensitiveDataLogging: false);

    using DbContextAttendance db = new(optionsBuilder.Options);
    int count = db.Database.ExecuteSqlRaw("DELETE FROM attendances WHERE work_date = {0}", today);

    Console.WriteLine($"今日（{today:yyyy/MM/dd}）の打刻を {count} 件消しました。");
    return 0;
}
catch (Exception ex)
{
    // 例外の詳細 (接続文字列が含まれうる) は出さず、種類だけ出す
    Console.Error.WriteLine($"今日（{today:yyyy/MM/dd}）の打刻を消せませんでした。（{ex.GetType().Name}）");
    return 1;
}
