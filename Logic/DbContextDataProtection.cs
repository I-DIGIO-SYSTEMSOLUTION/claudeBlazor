#nullable disable

using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LogicCommon
{
    // ASP.NET Core Data Protection の鍵を data_protection_keys テーブルに保存する
    // (ログイン Cookie と TOTP シークレットの暗号化に使う。鍵を消すとどちらも復号できなくなるので、古い鍵も削除しない)
    public class DbContextDataProtection : DbContext, IDataProtectionKeyContext
    {
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

        public DbContextDataProtection(DbContextOptions<DbContextDataProtection> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // パッケージのクラスは Id / FriendlyName / Xml なので、snake_case の列名に対応させる
            modelBuilder.Entity<DataProtectionKey>(entity =>
            {
                entity.ToTable("data_protection_keys");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.FriendlyName).HasColumnName("friendly_name");
                entity.Property(e => e.Xml).HasColumnName("xml");
            });
        }
    }
}
