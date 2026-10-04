#nullable disable

using Microsoft.EntityFrameworkCore;

namespace LogicCommon
{
    // ログイン関係のテーブル。SQL は Sql/User, Sql/UserSession などに書き、FromSqlRaw / ExecuteSqlRaw で実行する
    // パスワードハッシュやトークンを扱うので、開発時でも SQL のログにパラメータの値を出さない (Program.cs の登録を参照)
    public class DbContextAuth : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<UserToken> UserTokens { get; set; }
        public DbSet<SecurityPolicy> SecurityPolicies { get; set; }
        public DbSet<MailSetting> MailSettings { get; set; }
        public DbSet<Role> Roles { get; set; }

        public DbContextAuth(DbContextOptions<DbContextAuth> options) : base(options) { }
    }
}
