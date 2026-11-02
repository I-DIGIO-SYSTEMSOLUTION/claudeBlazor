using Microsoft.EntityFrameworkCore;

namespace LogicCommon
{
    // 勤怠画面に出す、その日の打刻の状態 (行が無い日は ClockInAt が null)
    // プリレンダリングで読んだ値を対話モードに引き継ぐ ([PersistentState]) ので、JSON にできる形にしている
    public sealed class AttendanceStatus
    {
        public DateOnly WorkDate { get; set; }
        public DateTime? ClockInAt { get; set; }
        public DateTime? ClockOutAt { get; set; }

        // 出勤・退勤できるかの判定
        public bool CanClockIn => ClockInAt == null || ClockOutAt != null;
        public bool CanClockOut => ClockInAt != null && ClockOutAt == null;
    }

    // 勤怠の打刻 (出勤・退勤)。データはログインユーザー単位で、日付は日本時間で判定する
    // 日付をまたぐ勤務は扱わない (退勤を押し忘れた日は退勤時刻なしのまま残る)。打刻の訂正も扱わない
    public sealed class AttendanceService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(AttendanceService));

        private readonly IDbContextFactory<DbContextAttendance> dbFactory;
        private readonly SqlStatementStore sqlStore;

        public AttendanceService(IDbContextFactory<DbContextAttendance> dbFactory, SqlStatementStore sqlStore)
        {
            this.dbFactory = dbFactory;
            this.sqlStore = sqlStore;
        }

        // 現在の日本時間の日付
        public static DateOnly Today() => AppTime.ToDisplayDate(DateTime.UtcNow);

        public async Task<AttendanceStatus> GetTodayAsync(long userId)
        {
            await using DbContextAttendance db = await dbFactory.CreateDbContextAsync();
            return await GetStatusAsync(db, userId, Today());
        }

        // 指定した日 (日本時間の日付) の打刻の状態
        private async Task<AttendanceStatus> GetStatusAsync(DbContextAttendance db, long userId, DateOnly workDate)
        {
            SqlQuery query = sqlStore.Build("Attendance/SelectByDate", new { userId, workDate });
            Attendance? row = await db.Attendances.FromSqlRaw(query.Sql, query.Parameters).AsNoTracking().FirstOrDefaultAsync();
            return new AttendanceStatus
            {
                WorkDate = workDate,
                ClockInAt = row?.ClockInAt,
                ClockOutAt = row?.ClockOutAt,
            };
        }

        // 休憩時間 (分)。今は 1 時間固定
        public const int BreakMinutes = 60;

        // 当月 (日本時間) の勤怠。月の全日を返し、打刻の無い日は時刻が空の行になる
        public async Task<AttendanceMonth> GetCurrentMonthAsync(long userId)
        {
            DateOnly today = Today();
            DateOnly firstDate = new(today.Year, today.Month, 1);
            DateOnly lastDate = firstDate.AddMonths(1).AddDays(-1);

            await using DbContextAttendance db = await dbFactory.CreateDbContextAsync();
            SqlQuery query = sqlStore.Build("Attendance/SelectByMonth", new { userId, firstDate, lastDate });
            Dictionary<DateOnly, Attendance> rows = await db.Attendances.FromSqlRaw(query.Sql, query.Parameters)
                .AsNoTracking()
                .ToDictionaryAsync(a => a.WorkDate);

            AttendanceMonth month = new() { Year = today.Year, Month = today.Month, Today = today };
            for (DateOnly date = firstDate; date <= lastDate; date = date.AddDays(1))
            {
                rows.TryGetValue(date, out Attendance? row);
                month.Days.Add(new AttendanceDay
                {
                    Date = date,
                    ClockInAt = row?.ClockInAt,
                    ClockOutAt = row?.ClockOutAt,
                    WorkMinutes = row?.ClockOutAt is DateTime clockOutAt ? CalculateWorkMinutes(row.ClockInAt, clockOutAt) : null,
                });
            }
            return month;
        }

        // 画面には分単位 (HH:mm) で出すので、出勤・退勤の時刻の秒を切り捨ててから差を取る (表示した時刻の差と合うように)
        private static int CalculateWorkMinutes(DateTime clockInAt, DateTime clockOutAt)
        {
            long inMinutes = clockInAt.Ticks / TimeSpan.TicksPerMinute;
            long outMinutes = clockOutAt.Ticks / TimeSpan.TicksPerMinute;
            return (int)Math.Max(0, outMinutes - inMinutes);
        }

        // 出勤。日付はボタンを押した時点の日本時間で判定する。その日の状態を読み、出勤できるか (AttendanceStatus.CanClockIn) を判定してから打刻する
        public async Task<AccountResult> ClockInAsync(long userId)
        {
            DateTime now = DateTime.UtcNow;
            DateOnly workDate = AppTime.ToDisplayDate(now);
            await using DbContextAttendance db = await dbFactory.CreateDbContextAsync();
            AttendanceStatus status = await GetStatusAsync(db, userId, workDate);
            if (!status.CanClockIn)
            {
                return AccountResult.Fail("本日は出勤済みです。");
            }
            int count = await ExecuteAsync(db, "Attendance/ClockIn", new { userId, workDate, now });
            if (count == 0)
            {
                return AccountResult.Fail("本日は出勤済みです。");
            }
            logger.Info($"出勤しました (ユーザーID {userId}、勤務日 {workDate:yyyy/MM/dd})。");
            return AccountResult.Ok();
        }

        // 退勤。日付はボタンを押した時点の日本時間で判定する。その日の状態を読み、退勤できるか (AttendanceStatus.CanClockOut) を判定してから打刻する
        // SQL 側にも「退勤時刻が空のときだけ」の条件があり、同時に押された場合はそちらで防ぐ
        public async Task<AccountResult> ClockOutAsync(long userId)
        {
            DateTime now = DateTime.UtcNow;
            DateOnly workDate = AppTime.ToDisplayDate(now);
            await using DbContextAttendance db = await dbFactory.CreateDbContextAsync();
            AttendanceStatus status = await GetStatusAsync(db, userId, workDate);
            if (!status.CanClockOut)
            {
                // 出勤前か退勤済み (前日から画面を開いたままで、日付が変わってから押した場合も含む)
                return AccountResult.Fail("本日の出勤記録が無いか、退勤済みです。");
            }
            int count = await ExecuteAsync(db, "Attendance/ClockOut", new { userId, workDate, now });
            if (count == 0)
            {
                // 判定の後に、別の画面から退勤された場合
                return AccountResult.Fail("本日の出勤記録が無いか、退勤済みです。");
            }
            logger.Info($"退勤しました (ユーザーID {userId}、勤務日 {workDate:yyyy/MM/dd})。");
            return AccountResult.Ok();
        }

        private async Task<int> ExecuteAsync(DbContextAttendance db, string name, object parameters)
        {
            SqlQuery query = sqlStore.Build(name, parameters);
            return await db.Database.ExecuteSqlRawAsync(query.Sql, query.Parameters);
        }
    }
}
