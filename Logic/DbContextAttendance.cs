#nullable disable

using Microsoft.EntityFrameworkCore;

namespace LogicCommon
{
    // 勤怠管理のテーブル。SQL は Sql/Attendance に書き、FromSqlRaw / ExecuteSqlRaw で実行する
    public class DbContextAttendance : DbContext
    {
        public DbSet<Attendance> Attendances { get; set; }

        public DbContextAttendance(DbContextOptions<DbContextAttendance> options) : base(options) { }
    }
}
