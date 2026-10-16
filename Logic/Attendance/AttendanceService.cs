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
        public string? Comment { get; set; }

        // 出勤はその日の行が無いときだけ、退勤はその日の行があり退勤時刻が空のときだけ押せる
        public bool CanClockIn => ClockInAt == null;
        public bool CanClockOut => ClockInAt != null && ClockOutAt == null;
    }

    // 勤怠の打刻 (出勤・退勤)。データはログインユーザー単位で、日付は日本時間で判定する
    // 日付をまたぐ勤務は扱わない (退勤を押し忘れた日は退勤時刻なしのまま残る)。打刻の訂正も扱わない
    public sealed class AttendanceService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(AttendanceService));

        // コメントの最大文字数 (attendances.comment の列の長さと合わせる)
        public const int MaxCommentLength = 1024;

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
            DateOnly workDate = Today();
            await using DbContextAttendance db = await dbFactory.CreateDbContextAsync();
            SqlQuery query = sqlStore.Build("Attendance/SelectByDate", new { userId, workDate });
            Attendance? row = await db.Attendances.FromSqlRaw(query.Sql, query.Parameters).AsNoTracking().FirstOrDefaultAsync();
            return new AttendanceStatus
            {
                WorkDate = workDate,
                ClockInAt = row?.ClockInAt,
                ClockOutAt = row?.ClockOutAt,
                Comment = row?.Comment,
            };
        }

        // 休憩時間 (分)。今は 1 時間固定で、勤務時間からそのまま差し引く (勤務時間が休憩時間より短い日は 0 分)
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
                    Comment = row?.Comment,
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
            return (int)Math.Max(0, outMinutes - inMinutes - BreakMinutes);
        }

        // 出勤。日付はボタンを押した時点の日本時間で判定する。その日の行が既にあれば打刻しない
        public async Task<AccountResult> ClockInAsync(long userId, string? comment)
        {
            if (ValidateComment(comment) is string error)
            {
                return AccountResult.Fail(error);
            }

            DateTime now = DateTime.UtcNow;
            DateOnly workDate = AppTime.ToDisplayDate(now);
            await using DbContextAttendance db = await dbFactory.CreateDbContextAsync();
            int count = await ExecuteAsync(db, "Attendance/ClockIn", new { userId, workDate, now, comment = NormalizeComment(comment) });
            if (count == 0)
            {
                return AccountResult.Fail("本日は出勤済みです。");
            }
            logger.Info($"出勤しました (ユーザーID {userId}、勤務日 {workDate:yyyy/MM/dd})。");
            return AccountResult.Ok();
        }

        // 退勤。日付はボタンを押した時点の日本時間で判定する。コメントは退勤時の内容で上書きする (空欄なら空になる)
        public async Task<AccountResult> ClockOutAsync(long userId, string? comment)
        {
            if (ValidateComment(comment) is string error)
            {
                return AccountResult.Fail(error);
            }

            DateTime now = DateTime.UtcNow;
            DateOnly workDate = AppTime.ToDisplayDate(now);
            await using DbContextAttendance db = await dbFactory.CreateDbContextAsync();
            int count = await ExecuteAsync(db, "Attendance/ClockOut", new { userId, workDate, now, comment = NormalizeComment(comment) });
            if (count == 0)
            {
                // 出勤前か退勤済み (前日から画面を開いたままで、日付が変わってから押した場合も含む)
                return AccountResult.Fail("本日の出勤記録が無いか、退勤済みです。");
            }
            logger.Info($"退勤しました (ユーザーID {userId}、勤務日 {workDate:yyyy/MM/dd})。");
            return AccountResult.Ok();
        }

        // 文字数は .NET の文字数 (UTF-16) で数える。画面の maxlength と SQL Server の nvarchar と同じ数え方で、
        // PostgreSQL の varchar (コードポイント単位) でもこれ以下になる
        private static string? ValidateComment(string? comment) =>
            comment != null && comment.Length > MaxCommentLength
                ? $"コメントは {MaxCommentLength} 文字以内で入力してください。"
                : null;

        // 空欄 (空白だけを含む) は NULL にする
        private static string? NormalizeComment(string? comment) =>
            string.IsNullOrWhiteSpace(comment) ? null : comment;

        private async Task<int> ExecuteAsync(DbContextAttendance db, string name, object parameters)
        {
            SqlQuery query = sqlStore.Build(name, parameters);
            return await db.Database.ExecuteSqlRawAsync(query.Sql, query.Parameters);
        }
    }
}
